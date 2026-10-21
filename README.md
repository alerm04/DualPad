<div align="center">

<img src="docs/icon.png" width="96" alt="DualPad icon" />

# DualPad

**Use your PS5 DualSense as an Xbox controller on Windows, with the touchpad as a mouse.**

A free, open-source alternative to DS4Windows: one file, no installer, a clean Windows 11 interface, a visual button editor and 12 languages.

[![Download](https://img.shields.io/badge/Download-DualPad.exe-3B8EFF?style=for-the-badge&logo=windows)](https://github.com/alerm04/DualPad/releases/latest/download/DualPad.exe)

[![Release](https://img.shields.io/github/v/release/alerm04/DualPad?style=flat-square)](https://github.com/alerm04/DualPad/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/alerm04/DualPad/total?style=flat-square)](https://github.com/alerm04/DualPad/releases)
[![Build](https://img.shields.io/github/actions/workflow/status/alerm04/DualPad/build.yml?branch=master&style=flat-square)](https://github.com/alerm04/DualPad/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-4ADE80?style=flat-square)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4?style=flat-square)
![Connection](https://img.shields.io/badge/USB%20%2B%20Bluetooth-supported-4ADE80?style=flat-square)
[![VirusTotal](https://img.shields.io/badge/VirusTotal-report-4ADE80?style=flat-square&logo=virustotal)](https://www.virustotal.com/gui/file/11ef985edc512313eeb15509041cb22ecfb7a4156b2b11f64466bae788c83a6b)

<img src="docs/demo.gif" width="860" alt="A real DualSense next to DualPad: it connects over Bluetooth and its light bar changes color live as colors are picked in the app" />

</div>

## Why DualPad

- **Works in every game**: DualPad creates a virtual Xbox 360 controller, so Game Pass, Xbox app, Epic and emulator games that only support Xbox pads just work.
- **Xbox layout out of the box**: ✕ → A, ○ → B, □ → X, △ → Y, L1/R1 → LB/RB, analog L2/R2 → LT/RT, Create → View, Options → Menu, PS → Xbox button. Sticks and D-pad sit where you expect them.
- **Touchpad as a mouse**: move the cursor, tap to click, tap with two fingers or press the right half for a right click, and scroll with two fingers.
- **Visual remapping**: click a button on the drawing, or just press it on the controller, then pick an Xbox button, a keyboard key or a mouse action. Every change can be undone with one click.
- **USB and Bluetooth**: battery level, charging state and connection type are shown live.
- **Lightbar, LEDs and rumble**: choose any color or let it follow the battery level, show the player number on the LEDs, and tune the rumble strength.
- **Profiles**: one per game, and you can duplicate, import or export them.
- **Low latency**: input is handled on a dedicated high-priority thread with no timers or queues. A built-in test measures what DualPad adds (about 1 ms in our tests).
- **Tidy**: lives in the system tray, starts with Windows if you want, and can turn the controller off when it exits.
- **Driver setup in one click**: required and optional drivers are installed from the settings through winget.
- **Your language**: English, Italiano, Español, Français, Deutsch, Português (Brasil), Polski, Türkçe, Русский, 日本語, 简体中文 and 한국어. DualPad follows the Windows language, and you can change it in Settings.

## Download and run

1. Download **[DualPad.exe](https://github.com/alerm04/DualPad/releases/latest/download/DualPad.exe)** (it is also in this repository's main folder). It is a single file, and .NET is already included.
2. Run it. If Windows SmartScreen shows *"Windows protected your PC"*, click **More info → Run anyway**. The warning appears because the app is not code-signed yet.
3. Connect the DualSense with a USB-C cable, or pair it over Bluetooth by holding **Create + PS** until the light blinks.
4. If the **ViGEmBus** driver is missing, DualPad tells you, and you can install it from **Settings → Drivers and components → Install**.

> Close DS4Windows (or similar tools) before starting DualPad, otherwise games will see two controllers.

**Requirements:** Windows 10/11 x64 and the [ViGEmBus](https://github.com/nefarius/ViGEmBus) driver, which can be installed from inside the app. [HidHide](https://github.com/nefarius/HidHide) is optional: it hides the PlayStation controller from games that would otherwise read it twice.

## Screenshots

<img src="docs/remap.png" width="900" alt="DualPad visual button editor" />

| Touchpad and sticks | Lightbar and rumble |
| --- | --- |
| <img src="docs/touchpad-sticks.png" alt="Touchpad and stick settings" /> | <img src="docs/lightbar-rumble.png" alt="Lightbar and rumble settings" /> |

| Latency test and power options | Drivers, language and theme |
| --- | --- |
| <img src="docs/advanced.png" alt="Advanced page with latency test" /> | <img src="docs/drivers.png" alt="Driver installation and language settings" /> |

## FAQ

**Is it really free?**
Yes. DualPad is open source under the [MIT License](LICENSE), with no ads, accounts or telemetry.

**Does it support the DualSense Edge?**
Yes. The extra Fn buttons and back paddles can be remapped too.

**How much latency does it add?**
On a Bluetooth DualSense we measured about 575 reports per second, around 0.01 ms of processing and about 1.2 ms for the virtual Xbox controller to deliver the state to games. A USB cable removes the Bluetooth link from the chain and gives the lowest total latency. You can run the test yourself under **Advanced**.

**My language is missing or a translation sounds wrong.**
Translations are plain JSON files, and adding or fixing one takes a few minutes: see [CONTRIBUTING.md](CONTRIBUTING.md#translating-dualpad).

**Where are my settings stored?**
In `%AppData%\DualPad`. Profiles are plain JSON files.

**Does it connect to the internet?**
Only when you click **Install** for a driver: winget then downloads it from the official source.

## Verify your download

Scanned on VirusTotal ([see the report](https://www.virustotal.com/gui/file/11ef985edc512313eeb15509041cb22ecfb7a4156b2b11f64466bae788c83a6b)).

SHA-256 of `DualPad.exe` 1.1.1:

```
11EF985EDC512313EEB15509041CB22ECFB7A4156B2B11F64466BAE788C83A6B
```

```powershell
Get-FileHash .\DualPad.exe -Algorithm SHA256
```

You can also build it yourself from this repository: see [Building from source](CONTRIBUTING.md#building-from-source).

## Contributing

Bug reports, ideas, translations and pull requests are welcome. Start with [CONTRIBUTING.md](CONTRIBUTING.md) or [open an issue](https://github.com/alerm04/DualPad/issues/new/choose).

If DualPad is useful to you, a ⭐ helps other players find it.

---

DualPad is released under the [MIT License](LICENSE). It is not affiliated with Sony or Microsoft. PlayStation, DualSense, Xbox and Windows are trademarks of their respective owners. The virtual controller relies on [ViGEmBus](https://github.com/nefarius/ViGEmBus) and the optional hiding on [HidHide](https://github.com/nefarius/HidHide), both by Nefarius Software Solutions.
