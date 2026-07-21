# Animation Engine — Agent Build & Architecture Guide

## Purpose of this document

This is the canonical build and architecture guide for the **Animation Engine** — a lightweight, decoupled Windows desktop visual reminder engine. Calendar and task sources are separate external integration callers and are explicitly out of scope for this codebase.

This document serves as both the architectural specification and implementation checklist.

---

## 1. What we're building

A lightweight Windows system tray application that:

1. Runs silently in the system tray, using near-zero RAM/CPU when idle. (target under 50MB)
2. Listens on a local-only HTTP endpoint (`http://127.0.0.1:5057`) for trigger requests.
3. On a trigger, plays a short animated GDI+ visual overlay (e.g., a plane flying across the screen with a message banner or a diagonal comet with a crash card) that never blocks or interrupts user work.
4. Is built to make adding new animation types trivial — the trigger server, tray shell, and window plumbing never need to change when a new animation is added.
5. Loads configurable defaults (e.g., speed settings) from a local `config.json` file on startup.

This application has **no knowledge of calendars, tasks, or external data sources**. It only knows how to receive a message payload and play an animation. Any process (`curl`, scripts, third-party apps) is a valid caller.

---

## 2. Tech stack

| Concern | Choice | Why |
|---|---|---|
| Language/runtime | C#, .NET 8, `net8.0-windows` | Native Windows performance, low memory footprint when compiled |
| UI framework | WinForms | Lighter than WPF/Electron; ideal for owner-drawn GDI+ animations |
| Local HTTP server | `System.Net.HttpListener` | Built into .NET, zero ASP.NET Core dependency, minimal RAM |
| Tray presence | `NotifyIcon` + `ContextMenuStrip` | Standard Windows system tray integration, no visible main window |
| Notifications | Windows toast notification API (`Microsoft.Toolkit.Uwp.Notifications`) | Persists the message in Action Center after the animation ends |
| JSON | `System.Text.Json` | Built-in, zero external dependencies |
| Animation rendering | GDI+ via `OnPaint` + `System.Windows.Forms.Timer` | High performance, zero external animation libraries needed |
| Configuration | Local `config.json` via `AppConfig` | Persistent app defaults |
| Publish target | `dotnet publish -r win-x64 --self-contained false -p:PublishTrimmed=true` | Keeps binary size and idle memory low |

---

## 3. Project structure

```
AnimationEngine/
├── AnimationEngine.csproj
├── AppConfig.cs               # JSON config loader (config.json)
├── Program.cs                 # STA entry point and application lifecycle
├── TrayContext.cs             # ApplicationContext, NotifyIcon, context menu wiring
├── TriggerServer.cs           # HttpListener loop, request parsing, UI thread marshalling
├── TriggerRequest.cs          # JSON request payload contract model
├── Animations/
│   ├── IAnimation.cs          # Interface contract implemented by every animation
│   ├── AnimationRegistry.cs   # Style key -> IAnimation registry lookup & fallback logic
│   ├── AnimationTiming.cs     # Shared speed/duration multipliers and easing math
│   ├── OverlayFormBase.cs     # Borderless, click-through (WS_EX_TRANSPARENT/LAYERED), topmost window base
│   ├── PlaneAnimation.cs      # Left-to-right plane banner animation
│   └── CometAnimation.cs      # Diagonal comet streak & full-screen title card animation
├── Assets/
│   ├── app.ico                # System tray & app icon
│   └── plane.png              # Vector asset for plane animation
└── Notifications/
    └── ToastNotifier.cs       # Native Action Center toast notification wrapper
```

Every new animation added in the future is a single file under `Animations/` plus one registration line in `AnimationRegistry.cs`.

---

## 4. The trigger contract

This is the entire integration surface of the system.

```http
POST http://127.0.0.1:5057/trigger
Content-Type: application/json

{
  "message": "string, required",
  "style": "plane" | "comet" | <future values>,   // optional, default "plane"
  "speed": "slow" | "normal" | "fast" | "dynamic"   // optional, default "dynamic" / "normal"
}

Response: 200 "triggered" | 400 <error> | 404 | 500
```

```http
GET http://127.0.0.1:5057/ping
Response: 200 "ok"
```

### Rules & Error Handling:
- Bind to `127.0.0.1` strictly. Never `0.0.0.0`.
- Unknown or missing `style` falls back safely to `"plane"`.
- Malformed JSON returns `400` status and preserves the listener loop.
- Incoming HTTP requests are marshalled onto the main UI thread before instantiating WinForms forms.

---

## 5. Built Components & Milestones Summary

### Milestone 1 — Trigger Server Skeleton (`TriggerServer.cs`, `TriggerRequest.cs`)
- Listens asynchronously on `http://127.0.0.1:5057/`.
- Routes `GET /ping` and `POST /trigger`.
- Parses JSON case-insensitively using `System.Text.Json`.

### Milestone 2 — System Tray Shell (`TrayContext.cs`, `Program.cs`)
- Runs headless via `ApplicationContext`.
- Context menu options: `Test Plane`, `Test Comet`, and `Exit`.
- UI thread marshalling using `SynchronizationContext` or WinForms `Invoke`.

### Milestone 3 — Extensibility Scaffolding (`Animations/`)
- `IAnimation`: Standard interface defining `StyleKey` and `CreateOverlay()`.
- `AnimationRegistry`: Resolves style strings to implementation instances with fallback to `"plane"`.
- `AnimationTiming`: Calculates speed multipliers (`slow = 1.4`, `normal = 1.0`, `fast = 0.7`, `dynamic` based on message length) and easing curves.
- `OverlayFormBase`: Enforces click-through (`WS_EX_TRANSPARENT | WS_EX_LAYERED`), non-activating (`WS_EX_NOACTIVATE`), borderless, `TopMost` window behavior.

### Milestone 4 — Plane Animation (`PlaneAnimation.cs`)
- Left-to-right horizontal flight across primary/target monitor.
- Trailing message banner with custom typography and smooth ease-in/ease-out motion.
- Automatic self-disposal upon animation completion.

### Milestone 5 — Comet Animation (`CometAnimation.cs`)
- Diagonal streak ending in a crash point.
- Transitions to full-screen title-card overlay displaying message.
- Enforces click-through even during full-screen black canvas display.

### Milestone 6 — Speed & Dynamic Timing Control (`AppConfig.cs`, `AnimationTiming.cs`)
- Configurable default speed loaded from `config.json` (`"dynamic"` dynamically scales flight time based on message word count).

### Milestone 7 — Action Center Notification Integration (`ToastNotifier.cs`)
- Fires Windows toast notifications alongside every triggered visual animation.

### Milestone 8 — Polish & Robustness
- Multi-monitor screen bounds target support.
- Overlapping animation triggers create non-interfering stacked overlay windows.
- Memory leak protection via strict form and timer lifecycle management.

---

## 6. Verification & Acceptance Checklist

- [x] Build target `dotnet publish -r win-x64 --self-contained false` builds cleanly.
- [x] Idle memory consumption remains under 50 MB.
- [x] Local HTTP endpoints `/ping` and `/trigger` behave strictly according to spec.
- [x] Click-through inputs work unhindered during both plane banner and full-screen comet overlays.
- [x] Local `config.json` allows customizing engine defaults.
- [x] System tray context menu enables instant manual testing of registered animation styles.
