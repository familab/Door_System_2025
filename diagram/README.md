# Wiring Diagram

This folder contains a [PinViz](https://pypi.org/project/pinviz/) diagram-as-code definition for the FamiLAB Door Controller wiring on a Raspberry Pi Zero W.

## Pin Mapping

| Physical Pin | GPIO   | Function                |
|--------------|--------|-------------------------|
| 1            | 3V3    | PN532 NFC Reader VCC    |
| 3            | GPIO2  | PN532 I2C SDA           |
| 5            | GPIO3  | PN532 I2C SCL           |
| 6            | GND    | PN532 GND               |
| 9            | GND    | Relay Module GND        |
| 11           | GPIO17 | Relay control signal    |
| 13           | GPIO27 | Unlock button signal    |
| 14           | GND    | Unlock button GND       |
| 15           | GPIO22 | Lock button signal      |
| 20           | GND    | Lock button GND         |

> **Note:** The Raspberry Pi Zero W shares the same 40-pin GPIO header layout as the Raspberry Pi 4, so the diagram uses the `raspberry_pi_4` board template.

## Prerequisites

Install PinViz (requires **Python 3.12+**):

```bash
py -3.12 -m venv diagram/venv
source diagram/venv/bin/activate  # On Linux/macOS
# or
diagram\venv\Scripts\activate  # On Windows

pip install -r diagram/requirements.txt
```

Install pinviz (via venv or pipx):

```bash
pip install pinviz
```

Or with [pipx](https://pipx.pypa.io/) for a global CLI tool:

```bash
pipx install pinviz
```

## Usage

### Validate the diagram

```bash
pinviz validate diagram/door_controller.yaml
```

### Generate an SVG wiring diagram

```bash
pinviz render diagram/door_controller.yaml -o diagram/door_controller.svg
```

Open `door_controller.svg` in any browser or image viewer to see the wiring diagram.

### Dark mode

```bash
pinviz render diagram/door_controller.yaml --theme dark -o diagram/door_controller_dark.svg
```

## Editing the Diagram

Edit `door_controller.yaml` to update the wiring. The YAML file defines:

- **board** – the Raspberry Pi board template (`raspberry_pi_4` for the 40-pin header)
- **devices** – the connected peripherals (PN532 NFC reader, relay module, buttons)
- **connections** – wires between board pins and device pins

See the [PinViz YAML configuration guide](https://nordstad.github.io/PinViz/guide/yaml-config/) and [PinViz documentation](https://nordstad.github.io/PinViz/) for the full reference.
