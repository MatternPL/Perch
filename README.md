# Perch

Pin windows on top and open apps on the monitor you want.

Free, no telemetry. Runs in the tray.

[![Buy me a beer](https://img.buymeacoffee.com/button-api/?text=Buy%20me%20a%20beer&emoji=%F0%9F%8D%BA&slug=mattern&button_colour=5F7FFF&font_colour=ffffff&font_family=Arial&outline_colour=000000&coffee_colour=FFDD00)](https://www.buymeacoffee.com/mattern)

## Download

Get the **PerchSetup** installer from the [latest release](../../releases/latest). It installs for your
user only, so it doesn't need admin rights.

There is also a portable **Perch.exe** if you don't want to install anything.

The build isn't code-signed yet, so Windows SmartScreen will show a warning the first time.
Click **More info** and then **Run anyway**.

Requires Windows 10 (1809 or newer) or Windows 11, 64-bit.

## Features

**Pin a window.** Press `Ctrl+Alt+P` to keep the active window on top of everything else.
Press it again to unpin. `Ctrl+Alt+U` unpins everything. You can also pin from the list.

**Placement rules.** Make an app always open on a specific monitor, for example Discord
maximized on monitor 2. Pick the monitor on a map of your own setup. Rules also apply to
windows that were already open when Perch started.

**Tray.** Closing the window keeps Perch running. Right-click the tray icon to open it,
unpin everything or quit.

Both shortcuts can be changed in Settings.

## Limitations

- Nothing can be shown on top of a game in exclusive fullscreen. Use borderless mode.
- Windows from programs running as administrator can't be pinned or moved.

## Building

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build src/Perch/Perch.csproj -c Release
```

To build the installer and the portable exe:

```powershell
winget install JRSoftware.InnoSetup
./installer/build-installer.ps1
```

The files end up in `dist/`.

Settings are stored in `%APPDATA%\Perch\config.json`.

## Support

Perch is free. If you find it useful, you can
[buy me a beer](https://www.buymeacoffee.com/mattern).

Also check out [Brisk](https://github.com/MatternPL/Brisk).

## License

[MIT](LICENSE)
