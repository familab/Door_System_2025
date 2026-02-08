import RPi.GPIO as GPIO
import time
import threading
import gspread
import csv
from oauth2client.service_account import ServiceAccountCredentials
import board
import busio
from adafruit_pn532.i2c import PN532_I2C

# GPIO Pin Definitions
RELAY_PIN = 17  # Relay control pin for the door latch
BUTTON_UNLOCK_PIN = 27  # Unlock button pin
BUTTON_LOCK_PIN = 22  # Lock button pin

# Time for unlocking (1 hour = 3600 seconds)
UNLOCK_DURATION = 3600

# Local CSV backup file
CSV_FILE = 'google_sheet_data.csv'

# Google Sheets Setup
scope = ["https://spreadsheets.google.com/feeds", "https://www.googleapis.com/auth/drive"]
creds = ServiceAccountCredentials.from_json_keyfile_name('creds.json', scope)
client = gspread.authorize(creds)

# RFID Sheets
sheet = client.open("Badge List - Access Control").sheet1  # Change to your Google Sheet name
log_sheet = client.open("Access Door Log").sheet1  # Log Sheet to record scans

# Lock object for managing GPIO access between threads
gpio_lock = threading.Lock()

# Setup GPIO
GPIO.setmode(GPIO.BCM)  # Ensure BCM mode is set once at the start
GPIO.setup(RELAY_PIN, GPIO.OUT)
GPIO.setup(BUTTON_UNLOCK_PIN, GPIO.IN, pull_up_down=GPIO.PUD_UP)
GPIO.setup(BUTTON_LOCK_PIN, GPIO.IN, pull_up_down=GPIO.PUD_UP)

# I2C setup for PN532
i2c = busio.I2C(board.SCL, board.SDA)
pn532 = PN532_I2C(i2c, debug=False)

# Configure PN532 to read RFID tags
pn532.SAM_configuration()

# Initially, keep the relay off (door locked)
GPIO.output(RELAY_PIN, GPIO.LOW)

# Flag to keep track of the current door state
door_unlocked = False
unlock_timer = None

# Button state tracking
last_unlock_time = 0
last_lock_time = 0
debounce_time = 0.5  # Half a second debounce time

def unlock_door():
    global door_unlocked, unlock_timer
    with gpio_lock:  # Lock GPIO access to ensure thread safety
        if not door_unlocked:  # Check if door is already unlocked
            door_unlocked = True
            GPIO.output(RELAY_PIN, GPIO.HIGH)  # Turn relay on (unlock the door)
            print("Door unlocked for 1 hour")

            # Log this event in Google Sheets
            log_access("Manual Unlock (1 hour)", "Success")

            # Set a timer to lock the door after 1 hour
            unlock_timer = threading.Timer(UNLOCK_DURATION, lock_door)
            unlock_timer.start()

def lock_door():
    global door_unlocked, unlock_timer
    with gpio_lock:  # Lock GPIO access to ensure thread safety
        if unlock_timer is not None:
            unlock_timer.cancel()  # Cancel any ongoing unlock timer
        if door_unlocked:  # Only lock if the door is currently unlocked
            door_unlocked = False
            GPIO.output(RELAY_PIN, GPIO.LOW)  # Turn relay off (lock the door)
            print("Door locked")

            # Log the manual lock override in Google Sheets
            log_access("Manual Lock", "Success")

# Add RFID log function to Google Sheets
def log_access(uid, status):
    # Get current time
    timestamp = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime())
    log_sheet.append_row([timestamp, uid, status])

# Fallback to CSV if Google Sheets is unavailable
def check_local_csv(uid):
    try:
        with open(CSV_FILE, mode='r') as file:
            reader = csv.reader(file)
            # Assume UID is in the first column
            for row in reader:
                if row and row[0].strip().lower() == uid.lower():
                    return True
    except FileNotFoundError:
        print(f"Local CSV file '{CSV_FILE}' not found.")
    return False

# Manual polling function for buttons
def monitor_buttons():
    global last_unlock_time, last_lock_time, door_unlocked
    while True:
        unlock_button_state = GPIO.input(BUTTON_UNLOCK_PIN)
        lock_button_state = GPIO.input(BUTTON_LOCK_PIN)

        current_time = time.time()

        # Unlock button check
        if unlock_button_state == GPIO.LOW:
            time.sleep(0.05)  # Check again after 50ms
            if GPIO.input(BUTTON_UNLOCK_PIN) == GPIO.LOW:  # Confirm it's still pressed
                if not door_unlocked:
                    unlock_door()

        # Lock button check
        if lock_button_state == GPIO.LOW and (current_time - last_lock_time > debounce_time):
            if door_unlocked:
                lock_door()
            last_lock_time = current_time

        time.sleep(0.1)  # Adjust polling rate if needed

# RFID reading and authentication logic
def check_rfid():
    global door_unlocked
    while True:
        unlock_button_state = GPIO.input(BUTTON_UNLOCK_PIN)
        #print(f"Unlock Button State: {unlock_button_state}")

	# Read the UID from the RFID card
        uid = pn532.read_passive_target(timeout=0.1)

        if uid:
            # Convert the UID to a hex string, remove '0x' and spaces
            uid_hex = ''.join(format(x, '02X') for x in uid).lower()  # Lowercase for case insensitivity
            print(f"Card scanned with UID: {uid_hex}")
            try:
                # Try looking up in Google Sheets
                uids = [cell.lower() for cell in sheet.col_values(1)]  # Case insensitive lookup
                if uid_hex in uids:
                    print(f"Access granted for UID: {uid_hex}")
                    #log_access(uid_hex, "Granted")

                    # If the door is already unlocked, only log the scan
                    if not door_unlocked:
                        with gpio_lock:  # Lock GPIO access
                            GPIO.output(RELAY_PIN, GPIO.HIGH)  # Unlock door
                            door_unlocked = True  # Mark door as unlocked
                            time.sleep(5)  # Keep door unlocked for 5 seconds
                            GPIO.output(RELAY_PIN, GPIO.LOW)  # Lock door
                            door_unlocked = False  # Mark door as locked
                    log_access(uid_hex, "Granted")
                else:
                    print(f"Access denied for UID: {uid_hex}")
                    log_access(uid_hex, "Denied")
            except gspread.exceptions.GSpreadException as e:
                print(f"Google Sheets Error: {e}")
                # Fallback to local CSV if Google Sheets is inaccessible
                if check_local_csv(uid_hex):
                    print(f"Access granted for UID: {uid_hex} from local CSV")
                    log_access(uid_hex, "Granted (from CSV)")

                    # If the door is already unlocked, only log the scan
                    if not door_unlocked:
                        with gpio_lock:  # Lock GPIO access
                            GPIO.output(RELAY_PIN, GPIO.HIGH)  # Unlock door
                            door_unlocked = True  # Mark door as unlocked
                            time.sleep(5)  # Keep door unlocked for 5 seconds
                            GPIO.output(RELAY_PIN, GPIO.LOW)  # Lock door
                            door_unlocked = False  # Mark door as locked
                else:
                    print(f"Access denied for UID: {uid_hex} from local CSV")
                    log_access(uid_hex, "Denied (from CSV)")
            time.sleep(1)  # Prevent multiple immediate reads
        else:
            time.sleep(1)  # Poll for RFID cards

# Main loop
try:
    # Create separate threads for button monitoring and RFID checking
    button_thread = threading.Thread(target=monitor_buttons)
    rfid_thread = threading.Thread(target=check_rfid)

    # Start both threads
    button_thread.start()
    rfid_thread.start()

    # Wait for both threads to finish (they won't in normal operation)
    button_thread.join()
    rfid_thread.join()

except KeyboardInterrupt:
    print("Exiting program...")
finally:
    GPIO.cleanup()
