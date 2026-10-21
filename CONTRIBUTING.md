# Contributing to DualPad

Thanks for helping! Bug reports, translations and pull requests are all welcome.

## Reporting a bug

[Open an issue](https://github.com/alerm04/DualPad/issues/new/choose) and include:

- your Windows version;
- how the controller is connected (USB or Bluetooth) and whether it is a DualSense or a DualSense Edge;
- the game or app where it happens;
- what you expected and what happened instead.

## Translating DualPad

Every piece of text in the app lives in one JSON file per language in
[`src/DualPad.App/Localization`](src/DualPad.App/Localization). English (`en.json`) is the reference.

**To fix a translation**, edit the text in that language's file and open a pull request.

**To add a language**:

1. Copy `en.json` to a new file named after the language code, for example `nl.json` or `zh-TW.json`.
2. Set `_language` to the language's own name, for example `"Nederlands"`.
3. Translate every value. Keep the keys as they are, and keep placeholders such as `{0}` and `{1}`.
4. Open a pull request.

The app picks the file up automatically, and the test suite checks that every file has the same keys and placeholders as English.

Most of the current translations were written with AI help, so native speakers fixing awkward wording is especially useful.

## Building from source

You need Windows 10 or 11 and the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```powershell
dotnet build
dotnet test
dotnet run --project src/DualPad.App
```

The [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases/latest) driver must be installed for the virtual Xbox controller to work.

To build the single-file `DualPad.exe` that ships in the release:

```powershell
dotnet publish src/DualPad.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

`DualPad.exe --snapshot <folder> [--live] [--light] [--lang <code>]` renders every page to PNG files, which is handy for checking layout and translations without a controller.

## Project layout

| Project | What it contains |
| --- | --- |
| `src/DualPad.Core` | HID access (SetupAPI and hid.dll), the DualSense protocol (USB report 0x01/0x02, Bluetooth 0x31 with CRC32), the mapping engine, touchpad mouse, ViGEm output, profiles and HidHide |
| `src/DualPad.App` | The WPF interface built with [WPF-UI](https://github.com/lepoco/wpfui), and the translations |
| `tests/DualPad.Core.Tests` | Tests for the protocol parser, CRC, Xbox layout, touchpad, profiles and translations |

## Pull requests

- Keep each pull request focused on one change.
- Run `dotnet test` before opening it.
- Follow the style of the surrounding code.

By contributing, you agree that your contribution is licensed under the [MIT License](LICENSE).
