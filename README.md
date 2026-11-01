# File Converter — Logi Actions SDK Plugin

Convert the most recent file in a folder to **PDF, Word (DOCX), PowerPoint (PPTX), Excel (XLSX), OpenDocument, plain text, HTML or an image** — with a single press on the **MX Creative Keypad**, a twist-and-press on the **MX Creative Dialpad**, or a tap from the **Logi Options+ Actions Ring**.

Conversions are performed locally by a headless [LibreOffice](https://www.libreoffice.org/) process, so files never leave the machine.

Built with the [Logi Actions SDK](https://logitech.github.io/actions-sdk-docs/) (C# / .NET 8).

---

## What it does

You point the plugin at a **source folder** (your Downloads folder by default). Each action converts the **most recently modified file** in that folder to the chosen format and writes the result next to it (or to a separate output folder you configure).

### Actions

| Action | Type | Where it shines |
| --- | --- | --- |
| **Convert to PDF / Word / PowerPoint / Excel / ODT / Text / HTML / PNG / JPG** | Button (`PluginDynamicCommand`) | One action per format — assign any of them to a Keypad key or the Actions Ring. |
| **Convert: Pick Format & Press** | Dial (`PluginDynamicAdjustment`) | Rotate the dial to choose a format, press to convert. Ideal for the MX Creative Dialpad. |
| **File Converter** folder | Folder (`PluginDynamicFolder`) | Opens a grid with every format as its own button — a tidy one-tap menu for the Actions Ring and Keypad. |

All actions appear in Logi Options+ under the **File Converter** plugin, grouped under **Convert** / **Convert Latest File**.

---

## Supported devices

Declared in `src/package/metadata/LoupedeckPackage.yaml`:

- **MX Creative Console** — MX Creative Keypad and MX Creative Dialpad (`LoupedeckExtendedFamily`). Actions assigned here are also available in the **Options+ Actions Ring**.
- Loupedeck CT, Live, Live S and Razer Stream Controllers (`LoupedeckCtFamily`).

---

## Prerequisites

1. **Logi Options+** (Windows/macOS) — installs the Logi Plugin Service that hosts the plugin.
   <https://www.logitech.com/software/logi-options-plus.html>
2. **.NET 8 SDK** (to build) — <https://dotnet.microsoft.com/download/dotnet/8.0>
3. **Logi Plugin Tool** (to package/install):
   ```bash
   dotnet tool install --global LogiPluginTool
   ```
4. **LibreOffice** (the conversion engine, required at runtime) — <https://www.libreoffice.org/download/>
   The plugin auto-detects LibreOffice in its standard install location. On a custom install, set the `SofficePath` plugin setting (see **Configuration**).

---

## Build & run (development)

```bash
dotnet build
```

The build writes a `Converter.link` file into the Logi Plugin Service `Plugins` folder and asks the service to reload, so the plugin shows up in Options+ immediately. Open the customization view for the Actions Ring, MX Creative Keypad or Dialpad, go to **All Actions**, and you'll find **File Converter** under **Installed Plugins**. If it doesn't appear, open Options+ settings and choose **Restart Logi Plugin Service**.

### Hot reload

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

The resulting `Converter.lplug4` can be installed on any machine running the Logi Plugin Service, and is the artifact you submit to the Marketplace.

---

## Configuration

Settings are stored in the host's per-plugin settings store (defaults in parentheses):

| Setting key | Meaning | Default |
| --- | --- | --- |
| `SourceFolder` | Folder scanned for the file to convert | Downloads → Desktop → user profile |
| `OutputFolder` | Where the converted file is written | *(empty)* = same folder as the source |
| `SofficePath` | Explicit path to the LibreOffice `soffice` executable | *(empty)* = auto-detect |

> The current build ships with sensible defaults and no custom settings UI. The code reads these keys via `Plugin.TryGetPluginSetting`, so a settings panel (or an `ActionEditor`-based configurable action) can be layered on later without changing the conversion engine.

---

## How conversion works

Each action runs:

```
soffice --headless --norestore --nologo --nofirststartwizard \
        -env:UserInstallation=<private temp profile> \
        --convert-to <format> --outdir <output folder> <input file>
```

A private, per-conversion LibreOffice user profile is used so conversions still work while you have LibreOffice open, and concurrent conversions don't clash. The temp profile is cleaned up afterwards.

### Conversion coverage

LibreOffice converts **within a document family**, and the actions report a clear failure when a combination isn't possible:

- **Text documents** (`.docx`, `.doc`, `.odt`, `.rtf`, `.txt`, `.html`, `.md`) → PDF, DOCX, ODT, TXT, HTML.
- **Spreadsheets** (`.xlsx`, `.ods`, `.csv`) → XLSX, ODS, CSV, PDF.
- **Presentations** (`.pptx`, `.ppt`, `.odp`) → PPTX, ODP, PDF, PNG/JPG.
- **Images / drawings** → PNG, JPG, PDF.

Converting *across* families — e.g. a Word document directly into a PowerPoint deck — is not something LibreOffice supports; such an action fails gracefully and logs `no export filter … found`.

---

## Project layout

```
ConverterPlugin.sln
src/
  ConverterPlugin.csproj          # net8.0, references PluginApi.dll, build/reload targets
  Directory.Build.props
  ConverterPlugin.cs              # Plugin entry point (universal plugin)
  Actions/
    ConvertFileCommand.cs         # One button action per output format
    FormatDialAdjustment.cs       # Dial: rotate to pick a format, press to convert
    ConverterFolder.cs            # Folder: grid of format buttons
  Services/
    ConversionFormat.cs           # Format registry (id, display name, LibreOffice filter)
    ConverterSettings.cs          # Reads/writes plugin settings with defaults
    FileSourceResolver.cs         # Finds the latest convertible file
    LibreOfficeConverter.cs       # Headless LibreOffice process driver
    ConvertRunner.cs              # Resolve file -> convert -> report
  Helpers/
    PluginLog.cs
    PluginResources.cs
  Resources/
    convert-icon.png              # Embedded icon
  package/
    metadata/
      LoupedeckPackage.yaml       # Plugin manifest + Marketplace metadata
      Icon256x256.png
.vscode/                          # Build / package / install tasks
```

---

## Submitting to the Logitech Marketplace

The project is already structured for submission:

- **Manifest** (`LoupedeckPackage.yaml`) includes the required Marketplace fields: `displayName`, `description`, `version`, `author`, `copyright`, `license` (**MIT** — GPL is not accepted), `licenseUrl`, `supportPageUrl` and `homePageUrl`.
- **Icon** `Icon256x256.png` is provided.
- Package with `logiplugintool pack` and verify with `logiplugintool verify ./Converter.lplug4` before uploading.

Before you publish, update these to your own details:

- `author` / `copyright` in `LoupedeckPackage.yaml` and `LICENSE`.
- `homePageUrl` / `supportPageUrl` if the repo moves.
- Bump `version` for each release.

Because conversion depends on LibreOffice being installed, state that clearly in your Marketplace listing so reviewers and users know to install it.

---

## License

[MIT](LICENSE)
