# Application Product & Design Document (APDD)

## 1. Product summary

**Working name:** Cachr for Windows  
**Product type:** Free, open-source, native Windows desktop utility  
**Technology:** C# / .NET / WinUI 3 / Windows App SDK  
**Primary job:** Let a user capture any rectangular screen region with one shortcut, then copy it or save it as a PNG.

Cachr takes inspiration from Shottr's calm, immediate capture interaction. It is not a feature-by-feature clone and must not use Shottr branding, assets, or source code.

## 2. Product intent

The app exists to make a two-step task feel instant:

1. Press a shortcut and drag a rectangle.
2. Copy the capture or save it as a file.

The capture surface should be quiet, deliberate, and visually polished: a dimmed desktop, a precise selection rectangle, an unobtrusive size readout, and a small action bar next to the completed selection.

## 3. Audience and use cases

| User | Need | Success condition |
|---|---|---|
| Developer / support worker | Share a screen detail in chat | Region is copied as an image in a few seconds. |
| Writer / designer | Save a visual reference | Region is saved as a clearly named PNG. |
| Everyday Windows user | Replace a heavier capture flow | Shortcut works globally and never requires a main app window. |

## 4. Scope: v1

### Included

- One global keyboard shortcut that opens capture mode.
- Rectangular region selection across all connected monitors.
- Pixel dimensions displayed while selecting.
- Escape to cancel; right-click also cancels.
- A small post-capture action bar with exactly two actions:
  - **Copy**: put the image on the Windows clipboard.
  - **Save**: open the standard save dialog and write a PNG.
- A brief success/failure notification for each action.
- Tray-resident lifetime: close the settings window without ending the utility; tray Exit ends it.
- One minimal settings page: shortcut and default save folder / filename pattern.

### Explicitly excluded

- Annotation, arrows, text, blur, crop editor, OCR, scrolling capture, delayed capture, video/GIF capture, upload/share links, cloud sync, history/gallery, account, telemetry, ads, subscriptions, or monetization.
- Window/object snapping, magnifier, color picker, rulers, and multi-step editing.
- Capture of protected content or attempts to bypass Windows security boundaries.

## 5. Interaction design

### Shortcut

The default shortcut is **Ctrl+Alt+Shift+4**. It is unusual enough to minimize collisions and can be registered globally through Win32.

Do not make `Win+Shift+4` the promised default. Windows-logo combinations are reserved by the operating system, so their availability is not reliable. `Win+Shift+S` remains owned by the Windows Snipping Tool. The settings screen may later validate non-Windows-key alternatives, but v1 does not claim support for OS-reserved shortcuts.

### Capture state machine

```text
Idle → Selecting → Selection complete → {Copy | Save | Cancel} → Idle
```

| State | Visible UI | Input and outcome |
|---|---|---|
| Idle | No foreground window; tray icon only | Registered shortcut enters Selecting. |
| Selecting | Full virtual-desktop overlay, dim layer, pointer crosshair | Drag left button to define a rectangle; Escape/right-click cancels. |
| Selection complete | Selection remains bright; compact action bar at its lower-right edge | Copy copies PNG-compatible image data; Save opens save dialog; Escape cancels. |
| Completing | Overlay remains until action returns | On success or handled failure, return to Idle. |

### Overlay visual rules

- One borderless, topmost overlay spans the virtual desktop, including negative monitor coordinates.
- Desktop dims at roughly 45–55% opacity; selected pixels are not dimmed.
- Selection has a 1 px high-contrast border and a dark translucent size label (`1240 × 720`).
- The action bar contains only `Copy`, `Save`, and a close/cancel glyph. It stays within the active monitor work area and flips above/left when necessary.
- Copy is the primary, keyboard-default action: `Enter` copies after selection; `Ctrl+S` saves.
- No capture is made until a non-zero selection exists. Tiny selections below 2 × 2 pixels are cancelled.

### Saving behavior

- Save always produces PNG in v1.
- Default proposed name: `Cachr_yyyy-MM-dd_HH-mm-ss.png`.
- If a default folder is configured, Save writes there immediately; otherwise it opens the standard Windows Save As dialog.
- Never overwrite a file silently; choose a non-conflicting suffix or let the system dialog resolve it.

## 6. Product requirements

### Functional requirements

| ID | Requirement |
|---|---|
| FR-01 | App registers the selected shortcut when it starts and reports a registration conflict in settings/tray. |
| FR-02 | Shortcut opens the overlay without activating a normal application window. |
| FR-03 | User can select a rectangle over one or more monitors. |
| FR-04 | Copy puts the selected bitmap on the clipboard in a format common chat and office apps can paste. |
| FR-05 | Save writes exactly the selected pixels as a valid PNG. |
| FR-06 | Escape, right-click, and close cancel without changing clipboard or filesystem. |
| FR-07 | Settings and tray Exit are accessible without recapturing. |

### Quality requirements

| ID | Requirement |
|---|---|
| NFR-01 | Cold shortcut-to-overlay target: under 250 ms on a typical Windows 11 PC. |
| NFR-02 | Selection feedback target: visibly tracks the pointer within one frame (about 16 ms at 60 Hz). |
| NFR-03 | Captures preserve native pixels on mixed-DPI monitors; no coordinate drift or unintended scaling. |
| NFR-04 | No image is sent off-device. The app has no network dependency or telemetry in v1. |
| NFR-05 | App operates on Windows 10 1809+ and Windows 11; Windows 11 is the primary visual target. |
| NFR-06 | All foreground controls are keyboard-operable and expose accessible names. |

## 7. Acceptance scenarios

1. From any desktop app, press Ctrl+Alt+Shift+4, drag a 500 × 300 region, press Enter, and paste into Paint: the pasted bitmap is 500 × 300.
2. Capture a region on a secondary monitor positioned to the left of the primary monitor: the selected area and cursor alignment remain correct.
3. With displays at different scaling levels, capture produces the physical-pixel rectangle the user selected, without an offset.
4. Press Escape during selection: no clipboard update, no file, no lingering overlay.
5. Choose Save, accept a path, and open the output: it is a non-empty PNG with the chosen dimensions.
6. Configure a shortcut already used by another process: settings keep the previous working shortcut and explain the conflict.

## 8. Open decisions to resolve before coding

1. **Distribution:** MSIX (recommended for clean installation/update) or unpackaged portable executable. The code architecture can support either, but packaging should be decided before release work.
2. **Save policy:** keep the proposed immediate-save behavior when a folder is configured, or always show Save As for maximum user control.
3. **License:** MIT (recommended for a small permissive utility) or another OSI-approved license.

## 9. Source constraints

WinUI 3 is the native Windows App SDK UI framework for C# desktop applications. Its top-level windows map to HWNDs, which enables the small amount of necessary Win32 interop. Global hotkeys use `RegisterHotKey`/`WM_HOTKEY`; Microsoft documents Windows-logo key combinations as reserved, which drives the shortcut decision. See [WinUI overview](https://learn.microsoft.com/en-us/windows/apps/get-started/winui-get-started-overview), [windowing overview](https://learn.microsoft.com/en-us/windows/apps/develop/ui/windowing-overview), and [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey).
