# Deployment Guide — From Scratch

End-to-end setup for the Door Controller on a fresh Raspberry Pi Zero 2 W.

## Table of Contents
- [Hardware](#hardware)
- [1. Flash & Configure Raspberry Pi OS](#1-flash--configure-raspberry-pi-os)
- [2. First Boot Setup](#2-first-boot-setup)
- [3. Enable I2C (PN532 Reader)](#3-enable-i2c-pn532-reader)
- [4. Install System Dependencies](#4-install-system-dependencies)
- [5. Create Deployment Directory](#5-create-deployment-directory)
- [6. Install Cloudflare Tunnel](#6-install-cloudflare-tunnel)
- [7. Install GitHub Actions Self-Hosted Runner](#7-install-github-actions-self-hosted-runner)
- [8. Configure GitHub Repository Secrets & Variables](#8-configure-github-repository-secrets--variables)
- [9. Run the Deploy Workflow](#9-run-the-deploy-workflow)
- [10. Verify the Service](#10-verify-the-service)
- [Directory Structure](#directory-structure)

---

## Hardware

- Raspberry Pi Zero 2 W
- PN532 NFC/RFID Reader (I2C)
- Relay module (GPIO pin 17)
- 2× push buttons (GPIO pins 27 and 22)
- 12V door latch + relay + buck converter
- MicroSD card (8GB+)
- Micro USB power supply (5V 2.5A+)

---

## 1. Flash & Configure Raspberry Pi OS

Use [Raspberry Pi Imager](https://www.raspberrypi.com/software/) to flash **Raspberry Pi OS Lite (64-bit)** (Debian Bookworm) to the SD card.

Before writing, open **OS Customisation** in the imager and set:

| Setting | Value |
|---|---|
| Hostname | `doorpi` (or your choice) |
| Username | `pi` |
| Password | *(strong password)* |
| SSH | Enable (password or key auth) |
| Wi-Fi | Your network SSID + password |

Write the image, insert the card, and power on the Pi.

---

## 2. First Boot Setup

SSH into the Pi once it appears on the network:

```bash
ssh pi@doorpi.local
```

Update the system:

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y git unzip curl wget
```

---

## 3. Enable I2C (PN532 Reader)

```bash
sudo raspi-config
```

Navigate to **Interface Options → I2C → Enable**. Reboot:

```bash
sudo reboot
```

Confirm I2C is active after reboot:

```bash
ls /dev/i2c-*
```

---

## 4. Install System Dependencies

```bash
sudo apt install -y python3 python3-pip python3-venv python3-rpi-lgpio
```

> `python3-rpi-lgpio` provides the GPIO library required at runtime — it must be installed system-wide before the venv is created.

---

## 5. Create Deployment Directory

The deploy workflow creates `DEPLOY_DIR` and its sibling `logs/`, `metrics/`, and `data/` directories automatically, then sets ownership to `pi`. No manual setup needed.

---

## 6. Install Cloudflare Tunnel

Cloudflare Tunnel exposes the health dashboard externally without opening firewall ports.

### Install cloudflared

```bash
curl -L https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-arm64.deb -o cloudflared.deb
sudo dpkg -i cloudflared.deb
```

> For Pi Zero 2 W (ARM64). Verify the architecture with `uname -m` — should show `aarch64`.

### Authenticate and create a tunnel

```bash
cloudflared tunnel login
```

### Run as a systemd service

```bash
sudo cloudflared service install
sudo systemctl enable cloudflared
sudo systemctl start cloudflared
```

Verify: `sudo systemctl status cloudflared`

---

## 7. Install GitHub Actions Self-Hosted Runner

The deploy workflow runs on `self-hosted` runners — the Pi must register itself with the repository.

Go to your GitHub repository → **Settings → Actions → Runners → New self-hosted runner**.
Select **Linux / ARM64** and follow the displayed commands. They look like:

```bash
mkdir actions-runner && cd actions-runner
curl -o actions-runner-linux-arm64-<VERSION>.tar.gz -L \
  https://github.com/actions/runner/releases/download/v<VERSION>/actions-runner-linux-arm64-<VERSION>.tar.gz
tar xzf actions-runner-linux-arm64-<VERSION>.tar.gz
./config.sh --url https://github.com/<org>/<repo> --token <TOKEN>
```

> Copy the exact commands from the GitHub UI — they include a short-lived registration token.

### Run the runner as a systemd service

```bash
sudo ./svc.sh install
sudo ./svc.sh start
```

The runner will now be available to pick up deploy jobs automatically.

---

## 8. Configure GitHub Repository Secrets & Variables

Go to **Settings → Secrets and variables → Actions**.

### Secrets (sensitive values)

| Secret name | Description |
|---|---|
| `CREDS_JSON` | Full contents of the Google service account JSON file |
| `DOOR_HEALTH_USERNAME` | Username for the health dashboard (default: `admin`) |
| `DOOR_HEALTH_PASSWORD` | Password for the health dashboard |

### Variables (non-sensitive)

| Variable name | Example value | Description |
|---|---|---|
| `DEPLOY_DIR` | `/opt/door` | Absolute path to the deployment directory on the Pi |
| `DOOR_HEALTH_PORT` | `3667` | Port the health server listens on |

---

## 9. Run the Deploy Workflow

The workflow is triggered manually:

1. Go to your repository → **Actions → Deploy**
2. Click **Run workflow** → select the branch → **Run workflow**

What happens automatically:

1. **Build** — calculates version via GitVersion, zips `start.py`, `src_service/`, `requirements.txt`, service files, and docs into `deploy.zip`
2. **Deploy** (on the Pi self-hosted runner):
   - Stops the running service
   - Archives the old deployment
   - Unpacks the new zip into `DEPLOY_DIR`
   - Creates a Python venv and installs dependencies
   - Writes `creds.json` from the `CREDS_JSON` secret
   - Installs the systemd service and drop-in with all environment variables
   - Restarts `door-app.service`

---

## 10. Verify the Service

```bash
sudo systemctl status door-app.service
journalctl -u door-app.service -f
```

Health page (on the Pi):

```
http://localhost:3667/health
```

Or via Cloudflare Tunnel:

```
https://door.yourdomain.com/health
```

Log in with the credentials set in `DOOR_HEALTH_USERNAME` / `DOOR_HEALTH_PASSWORD`.

---

## Directory Structure

After a successful deploy:

```
/opt/
├── door/                  ← DEPLOY_DIR
│   ├── start.py
│   ├── src_service/
│   ├── requirements.txt
│   ├── door-app.service
│   ├── creds.json         ← written from CREDS_JSON secret
│   ├── local.env          ← environment variables snapshot
│   └── venv/              ← Python virtual environment
├── logs/
│   ├── door_controller.log
│   └── door_controller_watchdog_heartbeat.log
├── metrics/
│   └── metrics.db
└── data/
    └── google_sheet_data.csv
```

Environment variables are injected into the service via `/etc/systemd/system/door-app.service.d/override.conf` — edit that file (then `sudo systemctl daemon-reload && sudo systemctl restart door-app.service`) if you need to change a value between deploys.
