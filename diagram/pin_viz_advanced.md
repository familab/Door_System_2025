# PinViz Device Templates — Adding and Validating (Local dev)

This document explains how to create device templates (single device JSONs) and register them with PinViz so you can reference them via `type: "<your_device>"` in your YAML diagrams.

## Quick summary ✅
- Per-device: use `pinviz add-device` (interactive wizard) or create a single JSON file and place it under `pinviz/device_configs/`.
- Validate templates: `pinviz validate-devices` (reports errors/warnings per JSON).
- After templates are registered you can reference them from YAML with `type: "your_type"`, e.g. `type: "relay_module"`.

---

## 1) Interactive (recommended for a single device)

1. Activate the diagram venv and run:

   ```powershell
   .\diagram\venv\Scripts\pinviz add-device
   ```

2. Follow the prompts (id, name, category, pins, layout, display). The wizard saves the JSON into the package `pinviz/device_configs/` tree.
3. Validate all device configs:

   ```powershell
   .\diagram\venv\Scripts\pinviz validate-devices
   ```

4. Use it in your YAML diagram:

   ```yaml
   devices:
     - type: "relay_module"
       name: "Relay Module"
   ```

5. Validate your diagram:

   ```powershell
   .\diagram\venv\Scripts\pinviz validate diagram/door_controller.yaml
   ```

---

## 2) Manual per-device JSON (quick, reproducible)

Create a JSON file for the device and place it in `device_configs/`. Required fields:
- `id` (string) — unique device id (filename fallback)
- `name` (string)
- `category` (one of: `actuators`, `displays`, `generic`, `io`, `leds`, `sensors`)
- `pins` — array of `{ name, role }` where role must be one of PinViz pin roles (e.g., `GPIO`, `3V3`, `5V`, `GND`, `I2C_SDA`, `I2C_SCL`).

Example `relay_module.json`:

```json
{
  "id": "relay_module",
  "name": "Relay Module",
  "description": "General-purpose relay module",
  "category": "actuators",
  "pins": [
    { "name": "VCC", "role": "5V" },
    { "name": "GND", "role": "GND" },
    { "name": "IN", "role": "GPIO" },
    { "name": "COM", "role": "5V" },
    { "name": "NO", "role": "5V" }
  ]
}
```

Where to place the JSON:
- For development, add to the package `diagram\venv\Lib\site-packages\pinviz\device_configs\` (or one of its subfolders). The loader scans that directory.

Then run:

```powershell
.\diagram\venv\Scripts\pinviz validate-devices
.\diagram\venv\Scripts\pinviz validate diagram/door_controller.yaml
```

---

## 3) Add multiple templates at once

- Put multiple JSON files inside `pinviz/device_configs/` (subdirectories allowed).
- Run `pinviz validate-devices` to catch issues across all templates.
- Fix schema warnings/errors and re-run until it completes cleanly.

---

## 4) Upstream contribution (recommended for long term)

If you want these device templates to be generally available:
1. Fork `nordstad/PinViz`
2. Add JSONs under `pinviz/device_configs/` following the repo conventions
3. Run `pinviz validate-devices` and project tests locally
4. Submit a PR with description and example YAML showing usage

---

## 5) Notes about this repository (what I changed locally)

- I added local JSON templates for `relay_module`, `power_supply`, `buck_converter`, and `door_latch`.
- I ran `pinviz validate-devices` and fixed the JSONs to match the device schema.
- To let the diagram YAML use `type: "relay_module"` I added a small local enhancement to the installed `pinviz` `schemas.py` (it now auto-discovers JSON `id`s and extends the set of valid device types). This is a pragmatic local dev change; upstream we should either:
  - include device discovery in the package, or
  - provide a documented mechanism (CLI command) that registers new types before config validation.

---

If you'd like, I can:
- Prepare a PR that adds the JSON templates and the `schemas.py` discovery change upstream (I can draft the PR and run tests), or
- Revert the local patch and keep everything as inline `pins` devices instead (if you prefer not to modify venv package files).

Which next step do you want? Reply with **PR** (prepare a PR) or **revert** (keep inline devices).