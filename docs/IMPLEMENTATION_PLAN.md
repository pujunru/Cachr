# Implementation Plan

## Guiding constraint

Build only the APDD v1 scope. A proposed feature must be rejected unless it directly improves region capture, clipboard copy, PNG saving, or the minimal settings/tray workflow.

## Phase 0 — Foundation (0.5–1 day)

1. Create a solution with `Cachr.App`, `Cachr.Core`, `Cachr.Platform.Windows`, and tests.
2. Target current LTS .NET and the current stable Windows App SDK at implementation time; pin exact package versions in the project files.
3. Add nullable references, analyzers, `.editorconfig`, CI build/test workflow, MIT `LICENSE`, and a concise contribution guide.
4. Configure Per-Monitor V2 DPI awareness before the first window.

**Done when:** a packaged debug build starts as a tray utility and exits cleanly.

## Phase 1 — App lifetime and global shortcut (1 day)

1. Implement single-instance behavior and the hidden message HWND.
2. Add `HotkeyService` around `RegisterHotKey`, `UnregisterHotKey`, and `WM_HOTKEY`.
3. Register Ctrl+Alt+Shift+4 with `MOD_NOREPEAT`.
4. Add a tray menu with `Capture`, `Settings`, and `Exit`.
5. Surface registration success/conflict in the settings screen and tray notification.

**Tests:** repeated-keypress suppression; an already-registered shortcut fails gracefully; shortcut is released on exit.

**Done when:** pressing the shortcut or tray Capture enters a temporary placeholder capture state from any foreground application.

## Phase 2 — Selection overlay (2–3 days)

1. Implement virtual-screen bounds discovery and a topmost borderless overlay.
2. Add dimming, crosshair cursor, pointer capture, drag rectangle, size label, Escape/right-click cancellation.
3. Add `SelectionGeometry` tests: reverse drag, negative coordinates, zero-size selection, and boundaries.
4. Implement the compact Copy/Save action bar and its edge-flipping layout.
5. Verify with one monitor, a left-positioned monitor, and a mixed-DPI monitor setup.

**Tests:** geometry unit tests plus a manual visual checklist for focus, z-order, and alignment.

**Done when:** user can make and cancel a correctly positioned selection across every attached screen.

## Phase 3 — Pixel capture and copy (2 days)

1. Implement the GDI snapshot service with safe ownership for HDC/HBITMAP handles.
2. Hide the overlay before capture and validate the result dimensions exactly equal selection dimensions.
3. Convert the DIB data into the canonical `CapturedImage` buffer.
4. Implement clipboard publication with bounded retry and a success/failure notification.
5. Verify pasting into Paint, Word, browser-based chat, and an image editor.

**Tests:** dimensions of generated buffer; manual capture-content baseline comparison; clipboard failure path.

**Done when:** a captured region pastes correctly and never includes the overlay UI.

## Phase 4 — PNG save and settings (1–2 days)

1. Implement PNG encoding and an atomic write strategy (temporary file then rename where appropriate).
2. Add Save As using the standard picker; add optional configured default-folder save.
3. Implement filename pattern validation and collision-free suffixing.
4. Persist only shortcut, save folder, and filename pattern in local JSON.
5. Add recovery for missing/unwritable folder and dialog cancellation.

**Tests:** PNG signature/dimensions, collision behavior, invalid folder, filename generation.

**Done when:** saved files open in Windows Photos/Paint with exact dimensions and no silent overwrite.

## Phase 5 — Hardening and release (2–3 days)

1. Run the acceptance scenarios in the APDD on Windows 10 and Windows 11.
2. Exercise high-DPI, multi-monitor, sleep/resume, taskbar auto-hide, HDR, and clipboard contention cases.
3. Review keyboard accessibility and accessible labels.
4. Measure shortcut-to-overlay latency and selection responsiveness; profile before optimizing.
5. Produce MSIX installer (or approved portable artifact), publish source, release notes, license, and privacy statement: “no data leaves this device.”

**Done when:** all acceptance scenarios pass, known limitations are documented, and a clean machine can install/run the release.

## Test matrix

| Area | Minimum coverage |
|---|---|
| Unit | Selection normalization, state transitions, shortcut validation, filename generation, settings serialization. |
| Integration | Hotkey delivery, clipboard copy, PNG output, save-dialog cancel, GDI resource release. |
| Manual UI | 100/125/150/200% scaling, two monitors in all arrangements, dark/light system theme, keyboard-only capture/cancel/copy/save. |
| Reliability | 100 repeated captures, rapid shortcut presses, display unplug/replug during selection, clipboard held by another app. |

## Delivery milestones

| Milestone | User-visible result |
|---|---|
| M1 | Shortcut opens a polished, cancellable region selector. |
| M2 | Selected region copies to clipboard reliably. |
| M3 | Selected region saves as PNG and preferences persist. |
| M4 | Packaged, tested open-source v1 release. |

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Windows-key shortcut collision | Do not promise or default to Windows-key combinations; validate registration results. |
| DPI coordinate mismatch | Declare PMv2 early; retain physical pixels as the capture coordinate system; test mixed DPI. |
| Overlay accidentally captured | Hide overlay and yield once before snapshot; add a visual regression test. |
| GDI handle leaks | Wrap native handles in `SafeHandle`-based types and stress-test repeated capture. |
| Antivirus/packaging friction | Ship signed MSIX when practical; keep dependencies small and transparent. |

## Definition of v1 complete

V1 is complete only when the user can invoke one reliable global shortcut, drag a region on any connected monitor, then either paste the exact pixels into another application or save them as PNG—without accounts, network activity, editors, annotations, or monetized features.
