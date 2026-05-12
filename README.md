# ESP32 Monitor — ASP.NET Core + Blazor + SQLite

A monitoring dashboard and REST API for controlling an ESP32-based LED strip device.
Polls the device every 5 seconds, logs all parameter changes to a local SQLite database,
and serves a live web dashboard built with Blazor Server.

---

## Architecture

```
Browser
  │  (SignalR + HTTP)
  ▼
Blazor Server Dashboard  (localhost:5000)
  │
  ├── every 5 s: GET /status ──────────────────────► ESP32 (192.168.x.x:80)
  │   detects changes → saves ParameterLog rows        │
  │                                                     │
  └── on user action: POST /effect, /reset ────────────►│
              logs with Source = "user"
  │
  ▼
SQLite  (esp32monitor.db)
```

---

## Simulation mode and fake monitored server

When **`Esp32:SimulationMode`** is `true` (default in `appsettings.Development.json`), the app does not call a real ESP32. Instead:

1. **`FakeMonitoredServerService`** alternates a simulated upstream path between **Healthy** and **Outage** using tick counts from **`MonitoredServerSimulation`** in appsettings (`HealthyDurationTicks`, `OutageMinTicks`, `OutageMaxTicks`).
2. The dashboard **`Internet` / "Upstream (sim.)"`** field and logs follow that health (same as a monitored link going up/down).
3. With **automatic LED monitoring** (default), the in-memory **effect** matches the ESP32 firmware idea: **`breathe_green`** when healthy, **`blink_red`** during outage. Choosing a decorative effect on the Effects page switches to **manual** mode until you use **Return to Monitoring** (or pick `breathe_green` / `blink_red` / `monitoring` again).
4. **`GET /api/monitored-service/health`** returns **204** when the simulated path is healthy and **503** during outage — same state as the poller; useful for demos or, later, pointing the ESP32 HTTP check at your PC.

With **`SimulationMode: false`**, the fake server is not advanced; real polling uses the ESP32 only. The health endpoint then stays at its initial state unless you extend the project.

---

## Project Structure

```
ESP32Monitor/
  Controllers/
    StatusController.cs         GET /api/status, GET /api/status/logs
    EffectController.cs         POST /api/effect, /api/effect/reset, /api/effect/monitoring
    MonitoredServiceController.cs  GET /api/monitored-service/health (204 / 503)
  Data/
    AppDbContext.cs           EF Core DbContext
    Migrations/               initial SQLite migration (auto-applied on startup)
  Models/
    DeviceStatus.cs           mirrors ESP32 /status JSON response
    ParameterLog.cs           log entry: Timestamp, ParameterName, OldValue, NewValue, Source
  Pages/
    Dashboard.razor           live status cards + filterable paginated log table
    Effects.razor             7 effect buttons, color pickers, device control
    MainLayout.razor          dark sidebar layout
    _Host.cshtml              Blazor Server host page
  Services/
    Esp32Client.cs             HTTP wrapper for all ESP32 endpoints
    DeviceStateHolder.cs       thread-safe in-memory cache of last device state
    FakeMonitoredServerService.cs  simulated upstream (simulation only)
    PollingService.cs            BackgroundService — polls, diffs, logs
  wwwroot/css/app.css         full dark-theme stylesheet
  App.razor
  Program.cs
  appsettings.json
  appsettings.Development.json
```

---

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 8.0 or later |
| ESP32 firmware | [ESP32_LED_Monitoring](https://github.com/mikroffarad/ESP32_LED_Monitoring) flashed and running |
| Network | PC and ESP32 on the same WiFi |

---

## Setup

### 1. Flash the ESP32

1. Open the `ESP32_LED_Monitoring` project in VS Code with the PlatformIO extension.
2. Connect the ESP32 via USB and click **Upload**.
3. Open the **Serial Monitor** — on first boot it starts in factory mode.

### 2. Connect the ESP32 to WiFi (first time only)

1. On any device, connect to WiFi network `ESP32-WiFi-Monitor` (password: `12345678`).
2. Open `http://192.168.4.1` in a browser.
3. Scan for networks, select your WiFi, enter the password, click **Connect**.
4. The ESP32 restarts and prints its assigned IP in the Serial Monitor, e.g.:
   ```
   IP address: 192.168.1.55
   ```

### 3. Configure the .NET app

Edit `appsettings.json` and set the ESP32's IP under `Esp32:BaseUrl`:

```json
{
  "App": {
    "BaseUrl": "http://localhost:5000"
  },
  "Esp32": {
    "BaseUrl": "http://192.168.1.55",
    "PollingIntervalMs": 5000,
    "SimulationMode": false
  },
  "MonitoredServerSimulation": {
    "Name": "Simulated DC uplink / Ethernet path",
    "HealthyDurationTicks": 8,
    "OutageMinTicks": 2,
    "OutageMaxTicks": 5,
    "RandomSeed": null
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=esp32monitor.db"
  }
}
```

For local development you can also set the IP in `appsettings.Development.json` to keep
`appsettings.json` clean.

### 4. Run the app

```powershell
cd ESP32Monitor
dotnet run
```

The SQLite database is created automatically on first run. Open your browser at:

```
http://localhost:5000
```

---

## REST API

All endpoints are also browseable via Swagger UI at `http://localhost:5000/swagger`
(Development environment only).

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/status` | Last polled device status + reachability flag |
| `GET` | `/api/status/logs` | Paginated change log (`?page=1&pageSize=50&parameter=internet&source=auto`) |
| `POST` | `/api/effect` | Set LED effect — body: `{ "effect": "rainbow", "color": "#FF0000" }` |
| `POST` | `/api/effect/monitoring` | Return device to automatic monitoring mode |
| `POST` | `/api/effect/reset` | Trigger factory reset on the ESP32 |
| `GET` | `/api/monitored-service/health` | **204** if simulated upstream healthy, **503** during outage (same state as dashboard in simulation) |

### Available effect values

| Value | Description |
|---|---|
| `rainbow` | Smooth rainbow across strip (HSV) |
| `fill_rainbow` | Fill strip with rainbow colors |
| `static` | Solid color — requires `color` field (`#RRGGBB`) |
| `snake` | Moving snake — requires `snakeColor` field |
| `waiting` | Soft rainbow wave |
| `breathe_green` | Pulsing green (internet available) |
| `blink_red` | Blinking red (internet unavailable) |

---

## Parameter Log

Every time a value changes on the device it is recorded automatically (`Source = "auto"`).
Every time a user sends a command via the dashboard or API it is also recorded (`Source = "user"`).

Tracked parameters:

| Parameter | Description |
|---|---|
| `wifi_connected` | WiFi connection state changed |
| `internet` | Internet / upstream availability changed (in simulation: follows fake monitored server) |
| `effect` | Active LED effect changed |
| `ssid` | Connected network name changed |
| `ip` | Device IP address changed |
| `static_color` | Static color was set by user |
| `snake_color` | Snake color was set by user |
| `device_mode` | Factory reset triggered |

---

## Dashboard Pages

### Dashboard (`/`)

- Status cards: Device, WiFi, upstream/Internet, SSID, IP, active effect
- In **simulation mode**: extra cards for **Monitored object** (Healthy / Outage), **LED mode** (auto green/red vs manual), and a short panel describing the fake server and `/api/monitored-service/health`
- Auto-refreshes every 5 seconds
- Filterable log table — filter by parameter name and/or source
- Pagination (20 entries per page)

### Effects (`/effects`)

- Select one of 7 LED effects
- Color pickers for static and snake effects
- Apply button sends command to ESP32 and logs the change
- Return to Monitoring and Factory Reset buttons with confirmation dialog

---

## Hardware Reference

| Component | GPIO |
|---|---|
| WS2812B LED strip data | GPIO 4 |
| Factory reset button | GPIO 14 |

Default LED count: 300. To change it, edit `#define NUM_LEDS` in the ESP32 firmware
`src/main.cpp` and re-flash.
