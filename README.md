# DualPad

DualPad trasforma un controller **DualSense** (PS5), collegato via USB o Bluetooth, in un **controller Xbox 360 virtuale** per Windows. Il **touchpad funziona come mouse**. È un'alternativa moderna a DS4Windows, con un'interfaccia Fluent per Windows 11 e un editor visuale dei tasti.

## Requisiti

- Windows 10/11 x64
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- Driver [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases/latest): crea il controller Xbox virtuale
- Facoltativo: [HidHide](https://github.com/nefarius/HidHide/releases/latest), per nascondere il DualSense ai giochi ed evitare il doppio input

> Chiudi DS4Windows prima di avviare DualPad: se sono aperti entrambi, i giochi vedono due controller.

## Funzioni

- **Layout Xbox predefinito**: ✕→A, ○→B, □→X, △→Y, L1/R1→LB/RB, L2/R2→LT/RT analogici, Crea→View, Options→Menu, PS→Xbox, L3/R3, croce direzionale e stick nelle posizioni Xbox
- **Touchpad come mouse**: movimento con accelerazione, tocco = clic, tocco con due dita = clic destro, pressione sul lato destro = clic destro, scorrimento con due dita
- **Editor visuale dei tasti**: clicchi il tasto sul disegno, oppure lo premi sul controller, e scegli un'azione Xbox, un tasto della tastiera o un'azione del mouse. Ogni tasto si ripristina con un clic
- **Stick e grilletti**: zona morta radiale, curva di risposta, inversione dell'asse Y, stick come mouse; anteprime live
- **Lightbar e LED**: colore libero o in base alla batteria, luminosità, numero del giocatore sui LED
- **Vibrazione**: il rumble dei giochi arriva al DualSense, con intensità regolabile
- **Profili**: profili multipli, da duplicare, rinominare, importare ed esportare (JSON in `%AppData%\DualPad\Profiles`)
- **Area di notifica**: avvio con Windows, avvio ridotto a icona, notifiche di connessione e di batteria scarica

## Compilazione

```bash
dotnet build
dotnet test
dotnet run --project src/DualPad.App
```

Per un eseguibile da distribuire:

```bash
dotnet publish src/DualPad.App -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

## Struttura

| Progetto | Contenuto |
| --- | --- |
| `src/DualPad.Core` | HID (SetupAPI/hid.dll), protocollo DualSense (report USB 0x01/0x02, Bluetooth 0x31 con CRC32), motore di mappatura, touchpad-mouse, uscita ViGEm, profili, HidHide |
| `src/DualPad.App` | Interfaccia WPF + [WPF-UI](https://github.com/lepoco/wpfui): Home, Tasti, Touchpad e stick, Luci e vibrazione, Profili, Impostazioni |
| `tests/DualPad.Core.Tests` | Test su parser, CRC, layout Xbox, touchpad e profili |

`DualPad.exe --snapshot <cartella> [--live] [--light]` salva tutte le pagine come PNG, utile per controllare la grafica.

## Crediti

Ispirato a [DS4Windows](https://github.com/Ryochan7/DS4Windows) (Ryochan7 e contributori). La struttura originale (lettura HID, `Xbox360OutDevice` su ViGEm, `DS4Touchpad`/`Mouse` per il cursore) è stata studiata con [REA](https://github.com/morluto/rea). Usa i driver ViGEmBus e HidHide di Nefarius.
