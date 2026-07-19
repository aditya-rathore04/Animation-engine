# Animation Engine — Agent Build Guide

## Purpose of this document

This is a build guide for a coding agent implementing the **Animation Engine** — the
first component of a larger decoupled reminder system. Build **only** what is
described here. Calendar and task sources are separate future projects and are
explicitly out of scope.

Follow the milestones in order. Each milestone has a definition of done. Do not
skip ahead — later milestones depend on scaffolding built in earlier ones.

---

## 1. What we're building

A lightweight Windows tray application that:

1. Runs silently in the system tray, using near-zero RAM/CPU when idle.
2. Listens on a local-only HTTP endpoint for trigger requests.
3. On a trigger, plays a short animated overlay (e.g. a plane flying across
   the screen with a message banner) that never blocks or interrupts the
   user's work.
4. Is built to make adding new animation types trivial later — the trigger
   server, tray shell, and window plumbing must never need to change when a
   new animation is added.

This app has **no knowledge of calendars, tasks, or any external data
source**. It only knows how to receive a message and play an animation. Any
other process (curl, a script, another app) is a valid caller.

---

## 2. Tech stack (do not substitute)

| Concern | Choice | Why |
|---|---|---|
| Language/runtime | C#, .NET 8, `net8.0-windows` | Native Windows, low memory footprint when compiled |
| UI framework | WinForms | Lighter than WPF/Electron; sufficient for owner-drawn GDI+ animations |
| Local HTTP server | `System.Net.HttpListener` | Built into .NET, no ASP.NET Core dependency, minimal RAM |
| Tray presence | `NotifyIcon` + `ContextMenuStrip` | Standard, no visible main window |
| Notifications | Windows toast notification API (Action Center) | Persists the message after the animation ends |
| JSON | `System.Text.Json` | Built-in, no Newtonsoft dependency |
| Animation rendering | GDI+ via `OnPaint` + `System.Windows.Forms.Timer` | No external animation library needed |
| Publish target | `dotnet publish -r win-x64 --self-contained false -p:PublishTrimmed=true` | Keeps binary and idle memory small |

Do not introduce ASP.NET Core, Electron, WPF, or any animation/game engine
library. If a milestone seems to require one, stop and flag it instead of
substituting silently.

---

## 3. Project structure

```
AnimationEngine/
  AnimationEngine.csproj
  Program.cs                 # entry point
  TrayContext.cs              # ApplicationContext, NotifyIcon, wiring
  TriggerServer.cs            # HttpListener, request parsing, marshalling
  TriggerRequest.cs           # the JSON contract model
  Animations/
    IAnimation.cs              # contract every animation implements
    AnimationRegistry.cs       # style name -> IAnimation lookup, fallback logic
    AnimationTiming.cs         # shared speed/duration/easing helpers
    OverlayFormBase.cs         # shared window plumbing (click-through, topmost, multi-monitor)
    PlaneAnimation.cs
    CometAnimation.cs
  Notifications/
    ToastNotifier.cs           # wraps the native notification call
```

Every new animation added in the future is a new file under `Animations/`
plus one line in `AnimationRegistry.cs`. No other file should need to change
to add an animation.

---

## 4. The trigger contract

This is the entire integration surface of the system. Keep it stable.

```
POST http://127.0.0.1:5057/trigger
Content-Type: application/json

{
  "message": "string, required",
  "style": "plane" | "comet" | <future values>,   // optional, default "plane"
  "speed": "slow" | "normal" | "fast"               // optional, default "normal"
}

Response: 200 "triggered" | 400 <error> | 404 | 500
```

```
GET http://127.0.0.1:5057/ping
Response: 200 "ok"
```

Rules:
- Bind to `127.0.0.1` only. Never `0.0.0.0`. This must not be reachable from
  the network, only from other processes on the same machine.
- Unknown or missing `style` → falls back to `"plane"`. Do not error on
  unrecognized style values — log and fall back instead, since a future
  source might send a style this build doesn't know about yet.
- Malformed JSON → `400`, do not crash the listener loop.
- The HTTP callback thread is **not** the UI thread. Every trigger must be
  marshalled onto the UI thread before touching any WinForms object.

---

## 5. Milestone 1 — Trigger server skeleton

**Build:**
- `TriggerRequest.cs`: model with `Message` (string), `Style` (string,
  default `"plane"`), `Speed` (string, default `"normal"`).
- `TriggerServer.cs`: `HttpListener` on `127.0.0.1:5057`, async listen loop,
  handles `GET /ping` and `POST /trigger`, deserializes JSON
  case-insensitively, invokes an `Action<TriggerRequest>` callback on
  success.
- Wrap all per-request handling in try/catch so one bad request never kills
  the listen loop.
- No UI yet — for this milestone, the callback can just print the parsed
  trigger to the console.

**Definition of done:**
- App runs as a console app (temporarily) and stays alive.
- `curl http://127.0.0.1:5057/ping` returns `ok`.
- `curl -X POST http://127.0.0.1:5057/trigger -d "{\"message\":\"test\"}"`
  returns `triggered` and the trigger is printed with `style` defaulted to
  `plane` and `speed` defaulted to `normal`.
- Sending malformed JSON returns `400` and the app keeps running.
- Confirm via `netstat` or similar that the port is bound to `127.0.0.1`
  only, not all interfaces.

---

## 6. Milestone 2 — Tray shell

**Build:**
- `TrayContext.cs`: `ApplicationContext` subclass owning a `NotifyIcon`
  with a `ContextMenuStrip` containing: `Test Plane`, `Test Comet` (stub
  until milestone 5, fine if it no-ops or prints for now), and `Exit`.
- `Program.cs`: `[STAThread] Main()` calls
  `ApplicationConfiguration.Initialize()` and runs `TrayContext` via
  `Application.Run(...)` — no visible main window at all.
- Wire `TriggerServer` into `TrayContext`. Marshal every trigger callback
  onto the UI thread using `BeginInvoke` off a control handle owned by the
  tray context (e.g. the `ContextMenuStrip`).

**Definition of done:**
- Running the app shows only a tray icon, no window, no taskbar entry.
- `Exit` from the tray menu cleanly shuts down the listener and the app.
- POSTing a trigger from curl while the app is running successfully
  reaches the UI thread (verify with a `MessageBox.Show` or debug print
  temporarily — remove before milestone 3).
- Idle RAM usage is checked in Task Manager and noted (should be well under
  50 MB).

---

## 7. Milestone 3 — Extensibility scaffolding (build before any real animation)

This is the most important milestone for long-term maintainability. Do not
write the plane or comet animation until this exists.

**Build:**

`Animations/IAnimation.cs`
```csharp
public interface IAnimation
{
    /// Unique style key this animation registers under, e.g. "plane".
    string StyleKey { get; }

    /// Produce a self-contained overlay Form. The form must show itself,
    /// run its own animation loop, and Close()+Dispose() itself when done.
    /// Must not block the calling thread.
    Form CreateOverlay(string message, double speedMultiplier, Rectangle screenBounds);
}
```

`Animations/AnimationRegistry.cs`
- A dictionary of `styleKey -> IAnimation`, populated at startup.
- `Resolve(string? styleKey)` returns the matching animation, or the
  `"plane"` animation if the key is null, empty, or unrecognized.
- Registering a new animation must be a single line here.

`Animations/AnimationTiming.cs`
- A shared helper that converts the `speed` string into a multiplier:
  `slow = 1.4`, `normal = 1.0`, `fast = 0.7`.
- Shared easing functions (ease-out cubic, ease-in cubic) so both current
  and future animations reuse the same math instead of duplicating it.

`Animations/OverlayFormBase.cs`
- Shared base class or set of static helpers for:
  - Borderless, `TopMost`, `ShowInTaskbar = false`.
  - **True click-through**: set both `WS_EX_LAYERED` and
    `WS_EX_TRANSPARENT` extended window styles via `CreateParams` (not just
    a `TransparencyKey`, which only affects hit-testing on the color-keyed
    region, not the whole window). This matters especially for the comet's
    full-screen black frame in milestone 5 — a `TransparencyKey` trick
    alone will not make an opaque black overlay click-through.
  - `WS_EX_NOACTIVATE` so the overlay never steals keyboard focus.
  - Multi-monitor targeting: default to `Screen.PrimaryScreen`, but accept
    a target screen so this is easy to change later (see milestone 8).
  - A self-dispose pattern: the form owns its own `Timer`, stops it and
    calls `Close()` when its animation timeline completes.

**Definition of done:**
- `AnimationRegistry.Resolve("nonsense")` returns the plane animation
  without throwing.
- A trivial test `IAnimation` (e.g. a plain colored rectangle that fades in
  and out) can be dropped in, registered, and triggered end-to-end through
  the full pipeline (HTTP → tray context → registry → overlay shown) with
  zero changes to `TriggerServer.cs` or `TrayContext.cs`.
- Confirm this trivial overlay is genuinely click-through: click and type
  through it into another window while it's on screen.

---

## 8. Milestone 4 — Plane animation (default)

**Build:** `Animations/PlaneAnimation.cs` implementing `IAnimation`,
`StyleKey = "plane"`.

- Horizontal flight, **left to right**, across the screen.
- A banner trails behind the plane displaying the message text.
- Simple vector/GDI+ shapes are fine for the plane and banner at this
  stage — do not spend time sourcing art assets. Shape and readability
  matter more than fidelity.
- Text: large enough to read comfortably in a few seconds, fixed
  reasonable font size regardless of message length (do not shrink to fit
  — the guide assumes callers keep messages short).
- Base duration ~4–5 seconds at `speedMultiplier = 1.0`, scaled by the
  multiplier from `AnimationTiming`.
- Use ease-in/ease-out from the shared timing helper for smooth motion,
  not linear movement.
- Click-through and non-blocking at all times — no exceptions for this
  animation.
- Auto-closes and disposes when the flight path completes.

**Definition of done:**
- `Test Plane` from the tray menu and a curl POST with no `style` field
  both play the plane animation.
- Message is legible throughout the flight at normal speed.
- Confirm click-through holds for the entire animation, not just at
  certain frames.
- Confirm the overlay fully disposes after finishing (check for leaked
  forms/handles if triggering repeatedly in quick succession).

---

## 9. Milestone 5 — Comet animation (explicit trigger only)

**Build:** `Animations/CometAnimation.cs` implementing `IAnimation`,
`StyleKey = "comet"`.

- Comet streaks diagonally across the screen toward a crash point at the
  bottom.
- At the crash moment, transition to a full-screen black overlay with the
  message centered, large and legible, movie-title-card style.
- Faster/punchier pacing than the plane — shorter streak phase, brief hold
  on the black card, then fade out on its own. No click-to-dismiss, ever.
- **This is the milestone where click-through must be verified hardest.**
  A full-screen, fully opaque black `Form` is the most likely place for a
  naive implementation to accidentally block input. Use the
  `WS_EX_LAYERED | WS_EX_TRANSPARENT` approach from `OverlayFormBase`, not
  a `TransparencyKey`-only approach — confirm explicitly that mouse clicks
  and keyboard input pass through even while the screen is fully black.
- Also scaled by `speedMultiplier` via the shared timing helper.

**Definition of done:**
- `Test Comet` from the tray menu and a curl POST with `"style":"comet"`
  both play correctly.
- Full black screen phase is confirmed click-through (click and type into
  another app while it's showing).
- Animation ends on its own within a few seconds with no lingering
  overlay, no visible flash/artifact after disposal.

---

## 10. Milestone 6 — Speed control

**Build:**
- Confirm both `PlaneAnimation` and `CometAnimation` consume
  `speedMultiplier` purely through `AnimationTiming` — there should be no
  separate, animation-specific speed logic.
- Test all three speed values against both animations (6 combinations
  total).

**Definition of done:**
- `slow`, `normal`, and `fast` all produce visibly different, correctly
  proportioned timing for both animations.
- No per-animation special-casing of speed exists outside the shared
  timing helper.

---

## 11. Milestone 7 — Notification Center integration

**Build:** `Notifications/ToastNotifier.cs` — fires a native Windows
notification (Action Center) containing the trigger's message, alongside
every animation, regardless of which style played.

**Definition of done:**
- Every trigger produces both the visual animation and an Action Center
  entry with the same message.
- Notification firing must not throw or crash the app if notifications are
  disabled at the OS level — fail silently/log, don't let it take down the
  overlay or the listener.

---

## 12. Milestone 8 — Polish pass

- **Multi-monitor**: decide and implement a clear default (e.g. primary
  monitor) using the hook already built into `OverlayFormBase`.
- **Overlapping triggers**: if two triggers arrive close together, each
  gets its own independent overlay instance (stacking), rather than one
  interrupting or being dropped. Verify this doesn't leak forms or crash
  under rapid repeated triggers.
- **Tray icon**: replace the placeholder `SystemIcons.Application` with a
  real `.ico`.
- **Config**: make the default speed configurable (simple local config
  file is fine — no need for a settings UI).

**Definition of done:**
- Sending 3 triggers within one second each produces 3 independent,
  correctly animating overlays with no crashes or leaked handles.
- App survives being left running idle for an extended period with no
  memory growth (spot-check Task Manager before/after a stress test of ~50
  triggers).

---

## 13. Explicit non-goals

Do not build any of the following as part of this guide — they are
separate future projects that will only ever talk to this engine over the
HTTP contract in section 4:

- Calendar polling / ICS parsing
- Microsoft To Do / Graph API integration
- Any OAuth or account sign-in flow
- Any UI for configuring sources
- Any animation styles beyond plane and comet

---

## 14. Final acceptance checklist

- [ ] `dotnet publish -r win-x64 --self-contained false -p:PublishTrimmed=true` succeeds
- [ ] Idle RAM usage is well under 50 MB
- [ ] `/ping` and `/trigger` work exactly as specified in section 4
- [ ] Missing/unknown `style` always falls back to plane without error
- [ ] Plane animation: left-to-right, banner message, click-through, ease-in/out timing
- [ ] Comet animation: diagonal streak, full black crash card, click-through even when fully opaque
- [ ] Speed multiplier correctly scales both animations via one shared helper
- [ ] Action Center notification fires alongside every animation
- [ ] Adding a hypothetical third animation requires touching only `Animations/` — verified in milestone 3
- [ ] Overlapping triggers stack cleanly with no leaks or crashes
