# High-Level Design (HLD)

## 1. Architecture at a glance

```text
                ┌──────────────────────────────┐
                │ App host / hidden message HWND │
                └───────┬───────────────┬──────┘
                        │ WM_HOTKEY     │ tray/settings
                        v               v
              ┌────────────────┐  ┌───────────────┐
              │ CaptureCoordinator│ │ SettingsStore │
              └───┬────────┬───┘  └───────────────┘
                  │        │
           show overlay    │ completed rectangle
                  v        v
     ┌────────────────┐  ┌─────────────────────┐
     │ CaptureOverlay │  │ DesktopCaptureService│
     │ WinUI 3 + HWND │  └──────┬──────────────┘
     └──────┬─────────┘         │ Bitmap
            │ copy/save          v
            │          ┌────────────────────────────┐
            ├─────────>│ ClipboardService / FileSaver│
            │          └─────────────┬──────────────┘
            v                        v
       NotificationService        Clipboard / PNG file
```

There is one process. The app host owns application lifetime, the global-hotkey message sink, and the tray icon. The overlay is created only during a capture. Services contain platform-sensitive code behind small interfaces so capture geometry, state transitions, and naming rules can be unit-tested.

## 2. Component responsibilities

| Component | Responsibility | Key implementation choice |
|---|---|---|
| `AppHost` | Startup, single-instance guard, tray, hidden Win32 message HWND, clean shutdown | WinUI 3 app plus thin P/Invoke layer. |
| `HotkeyService` | Register/unregister and report `WM_HOTKEY` | `RegisterHotKey` with `MOD_NOREPEAT`; validates failure. |
| `CaptureCoordinator` | Serializes capture lifecycle and commands | Explicit state machine; ignores repeated hotkeys while active. |
| `CaptureOverlayWindow` | Draws dimming/selection UI and collects pointer/keyboard input | Borderless topmost WinUI window sized to virtual desktop; per-monitor DPI aware. |
| `SelectionGeometry` | Normalizes drag points and maps logical input to physical pixels | Pure C# value types and tests for negative coordinates. |
| `DesktopCaptureService` | Captures physical pixels from virtual desktop and crops selection | Win32 GDI `BitBlt` / DIB section for v1. |
| `ClipboardService` | Publishes a bitmap to the Windows clipboard | Windows clipboard API with retry for transient locks. |
| `PngFileSaver` | Creates directories and encodes PNG | `Windows.Graphics.Imaging.BitmapEncoder` or a small, maintained .NET encoder abstraction. |
| `SettingsStore` | Shortcut and save preferences | JSON in `%LocalAppData%\\Cachr\\settings.json`; no cloud sync. |

## 3. Key technical decisions

### Capture mechanism: Win32 GDI for v1

Use `GetDC(NULL)` + compatible memory DC/DIB + `BitBlt` with `CAPTUREBLT`, then crop the requested physical-pixel rectangle. This matches the task: an immediate static desktop-region snapshot. It avoids the secure picker and continuous-frame lifecycle of `Windows.Graphics.Capture`, which is intended for a user-selected display/window capture session.

Known limitation: like other normal Windows capture techniques, protected/DRM content may be blank or unavailable. The app does not attempt to defeat this.

### Overlay strategy: one virtual-desktop window

Create a borderless, topmost, tool-window overlay that covers `SM_XVIRTUALSCREEN`, `SM_YVIRTUALSCREEN`, `SM_CXVIRTUALSCREEN`, and `SM_CYVIRTUALSCREEN`. It owns pointer capture after mouse-down and draws the selection in its client coordinates. The process declares Per-Monitor V2 DPI awareness before any window is created so physical screen coordinates remain authoritative.

### Bitmap canonical form

Keep one in-memory `CapturedImage` after the user completes selection:

```text
CapturedImage = BGRA8 pixel buffer + physical width + physical height + 96-DPI metadata
```

Copy and save both consume that immutable object. The overlay is not part of the screenshot because capture happens after hiding it and allowing composition to settle for one dispatcher turn.

### Clipboard and file output

Copy uses the Windows clipboard's bitmap path, including PNG where supported, plus a device-independent bitmap fallback for broad paste compatibility. Save encodes the same canonical buffer as PNG. A save only reports success after its stream has been closed successfully.

## 4. Control flow

```text
WM_HOTKEY
  → CaptureCoordinator.BeginAsync()
  → CaptureOverlayWindow.Show(virtual desktop bounds)
  → user drags rectangle
  → SelectionGeometry.Normalize()
  → user chooses Copy or Save
  → overlay.Hide()
  → DesktopCaptureService.CaptureAsync(rectangle)
  → ClipboardService.CopyAsync(image) OR PngFileSaver.SaveAsync(image)
  → notification
  → dispose image + overlay; return Idle
```

The coordinator owns cancellation. All UI work runs on the overlay dispatcher; blocking bitmap work and PNG encoding run off the UI thread. The implementation does not start a second capture until the first completes or cancels.

## 5. Proposed solution layout

```text
src/
  Cachr.App/                 WinUI 3 executable, XAML, composition root
  Cachr.Core/                state machine, geometry, contracts, settings model
  Cachr.Platform.Windows/    P/Invoke, GDI capture, clipboard, tray integration
tests/
  Cachr.Core.Tests/          geometry/state/naming tests
  Cachr.Integration.Tests/   manual-assisted Windows integration checks
docs/
```

`Cachr.Core` must not reference WinUI or P/Invoke assemblies. `Cachr.App` composes the services; `Cachr.Platform.Windows` is the only layer allowed to call the Windows APIs.

## 6. Failure handling and lifecycle

| Condition | Handling |
|---|---|
| Shortcut cannot register | Keep app running; tray/settings state explains that the selected shortcut is in use. |
| User cancels | Dispose overlay; do not capture. |
| Clipboard temporarily busy | Retry briefly with bounded backoff, then show a failure notice and preserve the captured image only until the notice is dismissed. |
| Save dialog cancelled | Return to Idle silently. |
| File write fails | Show actionable error; keep action bar available for retry/copy. |
| Capture fails | Show error, dispose capture state, return Idle. |
| Display configuration changes mid-capture | Cancel safely; a subsequent shortcut uses refreshed virtual-screen bounds. |

## 7. Security and privacy

- No networking code, analytics SDK, account, or telemetry.
- Screenshot pixels live only in memory until copied or saved.
- No automatic persistence except a user-chosen Save action or configured default folder.
- Settings contain only local preferences; do not store image content or clipboard history.
- Use standard Windows save UI when a destination is not configured.

## 8. Platform references

`RegisterHotKey` posts `WM_HOTKEY` to the registering window/thread and rejects unavailable combinations; see [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) and [WM_HOTKEY](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey). For a future capture backend evaluation, Microsoft documents `Windows.Graphics.Capture` and its WinUI 3 requirements in [Screen capture](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture).
