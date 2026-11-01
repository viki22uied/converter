# File Converter — Logi Actions SDK Plugin

Convert **any file to any format** — PDF, Word, PowerPoint, Excel, Markdown, HTML, images — with a single press on the **MX Creative Keypad**, a twist-and-press on the **MX Creative Dialpad**, or a tap from the **Logi Options+ Actions Ring** (which you can trigger straight from your Logitech mouse).

Everything runs **locally and for free** — nothing is uploaded, no account, no API key.

Built with the [Logi Actions SDK](https://logitech.github.io/actions-sdk-docs/) (C# / .NET 8).

> 🏆 Developed by **Vignesh Kumar U** — a **Top 50** project (team Deep Work) at the **Logitech Dev Studio Hackathon**.

---

## What makes it a real converter

It doesn't just push everything to PDF. A built-in **routing engine** chains two free tools so genuine cross-format conversions work:

| You want | It does | Verified |
| --- | --- | :-: |
| Word → PowerPoint | Pandoc | ✅ |
| PowerPoint → PDF | LibreOffice | ✅ |
| PDF → Word | LibreOffice (PDF import) | ✅ |
| PDF → PowerPoint | LibreOffice → Pandoc | ✅ |
| Markdown → PDF | Pandoc → LibreOffice | ✅ |
| Markdown → PowerPoint | Pandoc | ✅ |
| HTML → Word | Pandoc | ✅ |
| Office ↔ Office, anything → PDF | LibreOffice | ✅ |

When a conversion needs two steps (e.g. PDF → PowerPoint goes PDF → Word → PowerPoint), the plugin does it automatically behind the scenes through a temporary file. You just pick the output format and press.

If a particular combination genuinely can't be produced, the plugin **still gives you a usable PDF and tells you why** — the Actions Ring never dead-ends.

---

## The two free engines

The plugin **auto-detects** these; if one is missing it tells you exactly what to install.

| Engine | Does | Download |
| --- | --- | --- |
| **LibreOffice** | Office formats ↔ PDF, spreadsheets, PDF → Word | <https://www.libreoffice.org/download/> |
| **Pandoc** | Word ↔ PowerPoint, Markdown / HTML → anything | <https://pandoc.org/installing.html> |

Both are free, open-source, one-time installs. LibreOffice covers most everyday conversions; Pandoc adds the cross-format superpowers (Word→PPT, Markdown→anything).

---

## Actions

All actions appear in Logi Options+ under the **File Converter** plugin and can be assigned to the Actions Ring, MX Creative Keypad or Dialpad.

| Action | Type | Use it for |
| --- | --- | --- |
| **Convert a File** | Configurable (`ActionEditorCommand`) | The main action. Pick the output format, optionally a specific file or a watch-folder, where to save, and whether to open it afterwards. |
| **Convert to PDF / Word / PowerPoint / …** | Buttons (`PluginDynamicCommand`) | One-tap "convert the newest file to X". |
| **Convert: Pick Format & Press** | Dial (`PluginDynamicAdjustment`) | Rotate to choose a format, press to convert — for the MX Creative Dialpad. |
| **File Converter** folder | Folder (`PluginDynamicFolder`) | A grid of format buttons for the Actions Ring. |

### Which file gets converted?

- **Convert a File** action: the file you pick in its settings, or — if you leave that blank — the **newest file** in the folder you name (your **Downloads** folder by default).
- Quick buttons / dial / folder: always the **newest file** in the source folder (Downloads by default).

The converted file is saved next to the original (or in an output folder you choose), keeping the original name with the new extension.

---

## Supported devices

Declared in `src/package/metadata/LoupedeckPackage.yaml`:

- **MX Creative Console** — MX Creative Keypad and MX Creative Dialpad (`LoupedeckExtendedFamily`). Actions assigned here are also available in the **Options+ Actions Ring**, which can be opened from a Logitech mouse.
- Loupedeck CT, Live, Live S and Razer Stream Controllers (`LoupedeckCtFamily`).

---

## Prerequisites

1. **Logi Options+** — installs the Logi Plugin Service that hosts the plugin. <https://www.logitech.com/software/logi-options-plus.html>
2. **.NET 8 SDK** (to build). <https://dotnet.microsoft.com/download/dotnet/8.0>
3. **Logi Plugin Tool** (to package/install): `dotnet tool install --global LogiPluginTool`
4. **LibreOffice** and **Pandoc** (the conversion engines, see table above).

---

## Build & run (development)

```bash
dotnet build
```

The build writes a `Converter.link` file into the Logi Plugin Service `Plugins` folder and reloads the plugin, so it appears in Options+ immediately. Open the customization view for the Actions Ring / MX Creative Keypad / Dialpad → **All Actions** → find **File Converter** under **Installed Plugins**. If it doesn't show, open Options+ settings and choose **Restart Logi Plugin Service**.

Hot reload:

```bash
cd src
dotnet watch build
```

---

## Package & install (distribution)

```bash
dotnet build -c Release
logiplugintool pack ./bin/Release ./Converter.lplug4
logiplugintool install ./Converter.lplug4
```

`Converter.lplug4` is the artifact you install on any machine or submit to the Marketplace.

---

## Configuration

Stored in the host's per-plugin settings store (the **Convert a File** action also exposes the per-action options directly in Options+):

| Setting key | Meaning | Default |
| --- | --- | --- |
| `SourceFolder` | Folder scanned for the newest file | Downloads → Desktop → user profile |
| `OutputFolder` | Where converted files are written | *(empty)* = next to the source file |
| `SofficePath` | Explicit path to LibreOffice `soffice` | *(empty)* = auto-detect |
| `PandocPath` | Explicit path to `pandoc` | *(empty)* = auto-detect |

---

## How the routing works

For a requested conversion the plugin asks `ConversionPlanner` for candidate pipelines, ordered fewest-steps / highest-fidelity first, using **DOCX as a universal hub** (both engines read and write it well) and **PDF as the final safety net**. It then runs each candidate through a temporary working directory and keeps the first that actually produces a file — so even if the "best" route fails on an unusual input, a working route is used. A private, per-step LibreOffice profile is used so conversions work even while you have LibreOffice open.

```
Example — PDF → PowerPoint:
  1. LibreOffice:  document.pdf  → (temp) document.docx   [--infilter=writer_pdf_import]
  2. Pandoc:       document.docx → document.pptx
```

---

## Project layout

```
ConverterPlugin.sln
src/
  ConverterPlugin.csproj          # net8.0, references PluginApi.dll
  ConverterPlugin.cs              # Plugin entry point; checks for both engines on load
  Actions/
    ConvertAction.cs              # Main configurable action (format picker + options)
    ConvertFileCommand.cs         # One-tap button per output format
    FormatDialAdjustment.cs       # Dial: rotate to pick a format, press to convert
    ConverterFolder.cs            # Folder: grid of format buttons
  Services/
    ConversionFormat.cs           # Format registry + source-family classification
    ConversionPlanner.cs          # Builds ordered candidate pipelines (the router)
    SmartConverter.cs             # Runs pipelines, PDF safety net, missing-tool guidance
    LibreOfficeConverter.cs       # Headless LibreOffice step runner + detection
    PandocConverter.cs            # Pandoc step runner + detection
    ProcessRunner.cs              # External-process helper
    FileSourceResolver.cs         # Finds the newest convertible file
    ConverterSettings.cs          # Plugin settings with defaults
    ConvertRunner.cs              # Resolve file → convert → toast / open
  Helpers/ PluginLog.cs, PluginResources.cs
  Resources/ convert-icon.png
  package/metadata/ LoupedeckPackage.yaml, Icon256x256.png
.vscode/                          # Build / package / install tasks
```

---

## Submitting to the Logitech Marketplace

- The manifest includes the required fields: `displayName`, `description`, `version`, `author`, `copyright`, `license` (**MIT** — GPL is not accepted), `licenseUrl`, `supportPageUrl`, `homePageUrl`, and a 256×256 icon.
- Package with `logiplugintool pack` and run `logiplugintool verify ./Converter.lplug4` before uploading.
- Bump `version` for each release.
- Because conversion relies on LibreOffice and Pandoc, state in your listing that users install those two free tools. (The plugin invokes them as separate programs, so its own MIT license is unaffected by Pandoc's GPL.)

---

## Credits

Developed by **Vignesh Kumar U** ([@viki22uied](https://github.com/viki22uied)) — a **Top 50** project at the **Logitech Dev Studio Hackathon** (team Deep Work).

---

## License

[MIT](LICENSE) © Vignesh Kumar U
