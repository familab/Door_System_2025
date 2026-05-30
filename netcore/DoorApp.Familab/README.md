# DoorApp.Familab — NFC Door Access (.NET 8)

A production-ready .NET 8 port of the Familab Python NFC Door Access system. It runs on a
Raspberry Pi Zero W, reads badges from a PN532 NFC reader over I²C, drives a door-latch
relay over GPIO, and exposes a lightweight ASP.NET Core web server for health monitoring
and administration (manual door override, analytics, current state) protected by Google
Sign-In and a hashed master-password fallback.

The project is a **single application project** (`src/DoorApp.Familab.csproj`) with
clean-architecture folder boundaries, plus a single test project (`tests/`).

```
netcore/DoorApp.Familab/
├── DoorApp.Familab.sln
├── README.md
├── appsettings.Production.example.json
├── doorapp-netcore.service                # systemd unit for the Pi
├── scripts/
│   ├── updateVersionNetcore.sh            # CI version stamping (bash)
│   └── updateVersionNetcore.ps1           # CI version stamping (PowerShell)
├── src/
│   ├── DoorApp.Familab.csproj
│   ├── Program.cs                         # composition root + auth wiring
│   ├── AssemblyInfo.cs                    # embedded version (updated by CI)
│   ├── appsettings.json                   # all defaults incl. GPIO pins
│   ├── Domain/                            # interfaces + models (no dependencies)
│   │   ├── INfcReader.cs  IDoorRelay.cs  IBadgeStore.cs
│   │   ├── IAccessLogStore.cs  IVersionProvider.cs  ISystemClock.cs
│   │   └── Models/  (DoorState, AccessEvent, HealthSnapshot, AnalyticsSummary, ...)
│   ├── Application/                       # business logic
│   │   ├── Options/DoorOptions.cs
│   │   ├── Abstractions/  (service interfaces)
│   │   └── Services/  (DoorControl, BadgeValidation, AccessRules, Analytics, Health,
│   │                    ActionLog, RuntimeStatus, NfcMonitor hosted service)
│   ├── Infrastructure/                    # adapters
│   │   ├── Hardware/  (RaspberryPiNfcReader, Pn532I2c, GpioDoorRelay, stubs)
│   │   ├── Storage/   (Sqlite + Json stores)
│   │   ├── Auth/MasterPasswordHasher.cs
│   │   ├── Versioning/AssemblyVersionProvider.cs
│   │   └── DependencyInjection.cs
│   └── Api/                               # HTTP layer
│       ├── DoorEndpoints.cs  HtmlTemplates.cs  AuthThrottle.cs
└── tests/
    ├── DomainTests/  ApplicationTests/  InfrastructureTests/  ApiTests/
```

---

## Hardware wiring diagram (Raspberry Pi Zero W)

Pin numbers below are **BCM/GPIO numbers** (the same numbers used in `appsettings.json`
and in the original Python project). The physical header pin is shown in parentheses.

```
                    Raspberry Pi Zero W (40-pin header)
        +-----------------------------------------------------------+
 3V3 ---|(1) 3V3        (2) 5V                                       |
 SDA ---|(3) GPIO2/SDA  (4) 5V                                       |
 SCL ---|(5) GPIO3/SCL  (6) GND ---+                                 |
        |(7) GPIO4      (8) GPIO14 |                                 |
 GND ---|(9) GND        (10) GPIO15|                                 |
RELAY --|(11) GPIO17    (12) GPIO18|                                 |
UNLK ---|(13) GPIO27    (14) GND --+                                 |
LOCK ---|(15) GPIO22    (16) GPIO23|                                 |
        +-----------------------------------------------------------+

  PN532 NFC reader (I²C mode — set both DIP/jumpers to I2C):
     PN532 VCC  ──────────────► 3V3   (pin 1)
     PN532 GND  ──────────────► GND   (pin 6/9)
     PN532 SDA  ──────────────► GPIO2 / SDA (pin 3)
     PN532 SCL  ──────────────► GPIO3 / SCL (pin 5)
            (default I²C address 0x24 / 36 decimal)

  Door latch relay module (active-HIGH):
     Relay VCC  ──────────────► 5V    (pin 2)   *power the coil from 5V*
     Relay GND  ──────────────► GND
     Relay IN   ──────────────► GPIO17 (pin 11)  ── RELAY_PIN
        Relay COM/NO ─► electric strike / maglock supply (see strike datasheet)

  Optional physical buttons (momentary, to GND — internal pull-ups enabled):
     Unlock button ──► GPIO27 (pin 13) ──► GND     ── BUTTON_UNLOCK_PIN
     Lock   button ──► GPIO22 (pin 15) ──► GND     ── BUTTON_LOCK_PIN
```

> Power the relay/strike from a supply appropriate for your lock. Use a flyback diode
> across inductive loads. Energising the relay (`GPIO17` HIGH) **unlocks** the door;
> de-energising (LOW) **locks** it — identical semantics to the Python project.

Enable I²C on the Pi once: `sudo raspi-config` → *Interface Options* → *I2C* → *Enable*
(or add `dtparam=i2c_arm=on` to `/boot/config.txt`). Verify the reader with
`sudo i2cdetect -y 1` (you should see `24`).

---

## Configuring `appsettings.json`

All settings live under the `Door` section. Defaults ship in
[`src/appsettings.json`](src/appsettings.json). Override per environment with
`appsettings.Production.json`, environment variables, or `dotnet user-secrets`.

| Setting | Default | Meaning |
|---|---|---|
| `Hardware:UseRealHardware` | `false` | `true` on the Pi → uses GPIO + PN532; `false` → in-memory stubs |
| `Hardware:RelayPin` | `17` | BCM pin driving the relay (Python `RELAY_PIN`) |
| `Hardware:ButtonUnlockPin` | `27` | BCM pin for the unlock button |
| `Hardware:ButtonLockPin` | `22` | BCM pin for the lock button |
| `Hardware:I2cBusId` | `1` | I²C bus the PN532 is on (`/dev/i2c-1`) |
| `Hardware:Pn532I2cAddress` | `36` | PN532 I²C address (0x24) |
| `Timing:UnlockDurationSeconds` | `3600` | Manual unlock auto-relock window (Python `UNLOCK_DURATION`) |
| `Timing:BadgeUnlockDurationSeconds` | `5` | Badge-triggered unlock window (Python `DOOR_UNLOCK_BADGE_DURATION`) |
| `Timing:NfcPollIntervalMilliseconds` | `100` | NFC poll cadence |
| `Storage:Provider` | `Sqlite` | `Sqlite` or `Json` badge/event store |
| `Storage:SqlitePath` | `data/door.db` | SQLite database path |
| `Storage:BadgeJsonPath` | `data/badges.json` | JSON badge file (when `Provider=Json`) |
| `Health:RefreshIntervalSeconds` | `300` | Auto-refresh interval for the health page |
| `Auth:MasterUsername` | `admin` | Fallback login username |
| `Auth:MasterPasswordHash` | *(hash of `changeme`)* | PBKDF2 hash — see below |
| `Auth:SessionTtlHours` | `8` | Session cookie lifetime |
| `Auth:WhitelistEmails` | `[]` | Allowed Google emails (empty = allow any verified email) |
| `Auth:WhitelistDomains` | `[]` | Allowed domains; supports `*.example.com` / `.example.com` |
| `Auth:Google:Enabled` | `false` | Enable Google Sign-In |
| `Auth:Google:ClientId` / `ClientSecret` | `""` | Google OAuth2 credentials |

### Generating a master-password hash

The hash format is `pbkdf2_sha256$<iterations>$<saltBase64>$<hashBase64>`. The shipped
default verifies the password **`changeme`** — change it before going to production.
Generate a new hash with a one-liner (the same algorithm `MasterPasswordHasher` uses):

```bash
python3 - <<'PY'
import hashlib, base64, os
salt = os.urandom(16); it = 100000
dk = hashlib.pbkdf2_hmac("sha256", b"YOUR-NEW-PASSWORD", salt, it)
print(f"pbkdf2_sha256${it}${base64.b64encode(salt).decode()}${base64.b64encode(dk).decode()}")
PY
```

Paste the result into `Auth:MasterPasswordHash`.

---

## Running locally (dev)

Requires the .NET 8 SDK.

```bash
cd netcore/DoorApp.Familab
dotnet run --project src
# → browse http://localhost:3667/health
```

In Development the app uses the hardware **stubs** (no Pi needed). The NFC stub never
reports a card, but you can exercise the door manually from `/admin`.

---

## Running on a Raspberry Pi Zero W

1. **Enable I²C** (see hardware section) and add the service user to the `gpio`/`i2c` groups.
2. **Publish for 32-bit ARM** (Pi Zero W is ARMv6 32-bit) from your dev machine:
   ```bash
   dotnet publish src/DoorApp.Familab.csproj -c Release -r linux-arm --self-contained true -o publish
   ```
3. **Copy** the `publish/` folder to the Pi (e.g. `/opt/doorapp`) and add an
   `appsettings.Production.json` with `Door:Hardware:UseRealHardware = true`.
4. **Install the service**:
   ```bash
   sudo useradd -r -s /usr/sbin/nologin doorapp || true
   sudo usermod -aG gpio,i2c doorapp
   sudo mkdir -p /var/lib/doorapp && sudo chown doorapp:doorapp /var/lib/doorapp
   sudo cp doorapp-netcore.service /etc/systemd/system/
   sudo systemctl daemon-reload
   sudo systemctl enable --now doorapp-netcore
   sudo journalctl -u doorapp-netcore -f
   ```

> The .NET 8 runtime requires a 32-bit ARM build (`linux-arm`) for the Pi Zero W.
> For a Pi 3/4/Zero 2 W running 64-bit OS, use `-r linux-arm64`.

---

## Deploying via CI

Two GitHub Actions workflows are included at the repo root:

* **`.github/workflows/netcore.yml`** — on push/PR touching `netcore/**`: restore → build →
  run unit tests (TRX published) → `dotnet publish` → upload artifact.
* **`.github/workflows/deploynetcore.yml`** — manual (`workflow_dispatch`, pick the RID) or
  on a `v*` tag: computes the version with GitVersion, stamps `AssemblyInfo.cs` via
  `scripts/updateVersionNetcore.sh`, builds a self-contained single-file release, uploads
  the zip, and (on a tag) creates a GitHub Release.

To cut a release: `git tag v1.2.3 && git push origin v1.2.3`.

---

## Running tests

```bash
cd netcore/DoorApp.Familab
dotnet test
```

The suite (xUnit) covers all four layers:

* **DomainTests** — domain models (door state, access status).
* **ApplicationTests** — business logic: door control + auto-relock, badge validation,
  access rules (email/domain whitelist), analytics aggregation, event normalization,
  uptime formatting.
* **InfrastructureTests** — SQLite + JSON stores against a real temp database, the master
  password hasher, the hardware **stubs** (mock NFC/relay), and the version provider.
* **ApiTests** — full HTTP stack via `WebApplicationFactory<Program>`: public `/health`,
  `/display`, `/api/health`; admin auth redirect; master-password login; door toggle.

---

## Authentication

The admin area (`/admin`, `/admin/analytics`, door controls) requires a session cookie.
Two ways to obtain one:

### Master password (fallback)

Always available. Browse to `/login`, enter the `Auth:MasterUsername` (default `admin`)
and the password whose hash is in `Auth:MasterPasswordHash` (default `changeme`).
Repeated failures from one IP are throttled for `Auth:FailThrottleSeconds`.

### Google Sign-In

1. In the [Google Cloud Console](https://console.cloud.google.com/) create an **OAuth 2.0
   Client ID** (type *Web application*).
2. Add the redirect URI: `https://YOUR-HOST/signin-google`
   (for local dev: `http://localhost:3667/signin-google`).
3. Set in config:
   ```json
   "Auth": {
     "Google": { "Enabled": true, "ClientId": "...", "ClientSecret": "..." },
     "WhitelistDomains": ["*.yourdomain.com"],
     "WhitelistEmails": ["you@example.com"]
   }
   ```
4. The **Sign in with Google** button appears on `/login`. After Google authenticates the
   user, the email is checked against the whitelist (empty whitelist = allow any verified
   email, matching the Python behaviour); disallowed emails are bounced back to `/login`.

---

## Manually opening / closing the door

From the **Admin dashboard** (`/admin`) click **Unlock Door** / **Lock Door** (toggle).
Or call the JSON API with a valid session cookie:

| Method | Route | Action |
|---|---|---|
| `POST` | `/admin/door/toggle` | Toggle lock state, returns `{ "state": "unlocked\|locked" }` |
| `POST` | `/admin/door/open`   | Manual unlock for `UnlockDurationSeconds`, then auto-relock |
| `POST` | `/admin/door/close`  | Lock immediately |
| `GET`  | `/admin/state`       | Current door state JSON |

```bash
# log in (stores the session cookie), then toggle
curl -c cookies.txt -d 'username=admin&password=changeme&next=/admin' http://localhost:3667/login
curl -b cookies.txt -X POST http://localhost:3667/admin/door/toggle
```

A badge scan unlocks the door for `BadgeUnlockDurationSeconds`; a manual unlock holds it
for `UnlockDurationSeconds`. Both auto-relock unless refreshed, exactly like the Python
controller.

---

## Endpoints summary

| Route | Auth | Purpose |
|---|---|---|
| `GET /health` | public | HTML system-status page (door state, uptime, disk, NFC status) |
| `GET /display` | public | Public health display page |
| `GET /api/health` | public | JSON system status |
| `GET /login`, `POST /login` | public | Master-password login |
| `GET /login/google` | public | Start Google OAuth |
| `GET /logout` | public | Clear session |
| `GET /admin` | **admin** | Dashboard with door override + current state |
| `GET /admin/analytics` | **admin** | Badge scans, grants/denials, top users, uptime, errors |
| `GET /admin/analytics.csv` | **admin** | CSV export of events |
| `POST /admin/door/{toggle,open,close}` | **admin** | Manual override |

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `/admin` redirects to `/login` forever | Cookies disabled, or wrong master password. Check `Auth:MasterPasswordHash`. |
| Google button missing on `/login` | `Auth:Google:Enabled` is `false` or `ClientId` empty. |
| Google login returns "Email not allowed" | Add the email/domain to `WhitelistEmails`/`WhitelistDomains` (or clear both to allow all). |
| `redirect_uri_mismatch` from Google | The redirect URI in Google Console must be `<scheme>://<host>/signin-google`. |
| Badges never grant access | Reader not detected. Run `i2cdetect -y 1` (expect `24`); confirm `UseRealHardware=true` and `I2cBusId`. |
| `Access to the path '/dev/gpiomem' is denied` | Add the service user to the `gpio` group; for I²C add to `i2c`. |
| App exits immediately on the Pi | Wrong RID. Pi Zero W needs `linux-arm` (32-bit), not `linux-arm64`. |
| Port 3667 already in use | Change `Urls`/`ASPNETCORE_URLS` or stop the conflicting process. |
| SQLite "unable to open database file" | Ensure `Storage:SqlitePath`'s directory exists and is writable by the service user. |
| Door stays unlocked | A manual unlock holds for `UnlockDurationSeconds`; click **Lock Door** or wait for auto-relock. |
| Version shows `1.0.0` in CI builds | Expected until `deploynetcore.yml` stamps `AssemblyInfo.cs` from GitVersion. |
