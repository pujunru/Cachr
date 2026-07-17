# Cachr for Windows

A small, free and open-source Windows screenshot utility inspired by the focused capture flow of Shottr.

The first release is intentionally limited to region capture, copying the result to the clipboard, and saving it as a PNG file. It is a native C#/.NET 8 WinUI 3 desktop application.

## Run

Requirements for building: .NET SDK **8.0.423**, Visual Studio with the Windows App SDK workload (for the Windows packaging build tasks), and Windows 10 1809+. The Debug output is self-contained and does not require the Windows App Runtime to be preinstalled.

```powershell
dotnet restore Cachr.sln
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" Cachr.sln /t:Build /p:Configuration=Debug /p:Platform=x64
& .\src\Cachr\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\Cachr.exe
```

Run Cachr from the system tray, press the configured global shortcut (default: `Ctrl+Alt+Shift+4`), and drag a region. The result opens in a compact canvas window where it can be copied or saved as PNG. `Escape` cancels capture.

The shortcut, theme, and startup behavior can be changed from **Tray → Settings**.

## Downloads

Every successful Windows CI build produces two artifacts:

- `Cachr-portable-win-x64.zip` — self-contained, no installation required.
- `Cachr-Setup-win-x64.exe` — per-user installer with an optional **Start Cachr automatically when I sign in** checkbox.

## Development workflow

The `main` branch is protected. Product changes must be proposed through a pull request and pass the required `build` check before merge.

Design documents:

- [Application Product & Design Document](docs/APDD.md)
- [High-Level Design](docs/HLD.md)
- [Implementation Plan](docs/IMPLEMENTATION_PLAN.md)
