# RadialMenu

**Hold right-click. Get a glass dial menu right under your cursor.**

RadialMenu is a small, free Windows app that turns a right-click *hold* into a radial (pie) menu with the actions you use all day: volume, brightness, switching apps, full screen, snapping windows, media controls, screenshots and more. Release over an action to run it. No windows to open, no hunting for keys.

<!-- Add a screenshot or GIF here: ![RadialMenu demo](docs/demo.gif) -->

## Features

- **Hold right-click** (or hold and move a little) to open the dial centred on your cursor
- **Two rings:** pick a category, move outward, release on an action
- **Scroll to adjust** volume and brightness while hovering them
- **Liquid-glass look:** real frosted blur of whatever is behind the dial, with tint, rim light and soft shadow
- **Live Appearance settings:** blur, tint, size, animation speed, Dark / Light / Clear style, highlight colour
- **Smooth animations** that you can slow down or turn off
- **Auto-sizes** itself for your screen resolution
- **Normal right-clicks still work:** a quick click opens the usual context menu
- **Lightweight:** lives in the system tray, no installer, no account, no internet needed
- **Fully customisable:** add your own actions in one C# file

## What is in the menu

| Category | Actions |
|---|---|
| Volume | up, down, mute (scroll wheel adjusts) |
| Brightness | brighter, dimmer, night light settings (scroll wheel adjusts) |
| Windows | switch app, task view, full screen (F11), maximize, minimize, snap left/right, show desktop |
| Media | previous, play/pause, next, stop |
| Browser / tabs | back, forward, refresh, new tab, close tab, reopen closed tab |
| Edit | copy, paste, cut, undo, redo, select all |
| Capture | snipping tool, screenshot, emoji panel, clipboard history |
| System | Task Manager, Settings, File Explorer, Run, lock PC, close window |

## Download and run

1. Install the **[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)** (x64) if you don't have it.
2. Download the latest zip from the **[Releases](../../releases)** page and extract it anywhere.
3. Run `RadialMenu.exe`. A tray icon appears.
4. Hold the right mouse button on your desktop or in any app.

> **Windows SmartScreen** may warn that the app is from an unknown publisher, because it is not code-signed. Choose **More info → Run anyway**. The full source is in this repository, so you can read it or build it yourself.

### How to use

- **Open:** hold the right button for about 0.2 s, or hold and move slightly.
- **Choose:** move toward a slice, then outward to see its actions.
- **Run:** release over an action. Release in the centre or over nothing to cancel.
- **Tray icon menu:** Enabled, Appearance…, Start with Windows, Open settings file, Exit.

## Appearance

Right-click the tray icon → **Appearance…** to open the settings window. A live, animated preview of the dial sits beside it, so you see every change as you make it. Settings are saved automatically.

## Build from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```
git clone https://github.com/saadtheem/RadialMenu.git
cd RadialMenu
build.bat
```

The app is created in `dist\RadialMenu.exe`. For development use `dotnet run`.

## Customise the menu

Open `MenuModel.cs` and edit `MenuBuilder.Build()`. Each action is one line:

```csharp
I("📄", "Copy", Keys(VK.Ctrl, VK.C)),            // a keyboard shortcut
I("📝", "Notepad", Run("notepad.exe")),          // open a program or link
I("🔇", "Mute", AudioController.ToggleMute),     // call your own code
```

The ring re-divides itself automatically when you add or remove slices.

## Settings file

`%AppData%\RadialMenu\config.json`

| Setting | Meaning |
|---|---|
| `HoldMs` | how long to hold before the dial opens |
| `MoveThresholdPx` | pixels to move while holding to open it (about 96 = 1 inch) |
| `ExcludedProcesses` | programs where right-click is left alone, e.g. games, remote desktop, 3D tools |
| `BlurRadius`, `TintStrength`, `Style`, `Accent`, `SizePercent`, `Animations`, `AnimSpeed` | appearance (use the Appearance window instead of editing these) |

## Known limitations

- Windows blocks mouse hooks and simulated keys over **elevated (administrator)** windows. To use the dial there, run RadialMenu as administrator.
- **Brightness** works on laptop screens. External monitors need DDC/CI, which is not included yet.
- Apps that use right-button dragging (some CAD and 3D programs) should be added to `ExcludedProcesses`.
- The glass effect is a snapshot taken when the dial opens, so video playing behind it pauses visually while it is open.
- Some online games with anti-cheat may not like global mouse hooks. Add them to `ExcludedProcesses`.

## Privacy and security

RadialMenu runs entirely on your PC. It makes no network connections, collects nothing, and stores only its settings file. It uses a global mouse hook to detect the right-click hold, which is why some security software may mention it. The code is open for you to check.

## Roadmap

- Editable menu inside the app (no code needed)
- Brightness for external monitors (DDC/CI)
- Per-app menus
- Keyboard-triggered dial
- Signed installer

Ideas and bug reports are welcome in **Issues**.

## Contributing

Pull requests are welcome. Please open an issue first for larger changes.

## License

[MIT](LICENSE)
