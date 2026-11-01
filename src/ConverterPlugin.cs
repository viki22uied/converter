namespace Loupedeck.ConverterPlugin
{
    using System;

    // Plugin-level logic for the File Converter plugin.
    //
    // This is a "universal" plugin: its actions are available to any supported device
    // (MX Creative Keypad, MX Creative Dialpad and the Actions Ring) without being tied
    // to a specific host application.
    public class ConverterPlugin : Plugin
    {
        // This plugin does not control a specific host application.
        public override Boolean UsesApplicationApiOnly => true;

        public override Boolean HasNoApplication => true;

        public ConverterPlugin()
        {
            PluginLog.Init(this.Log);
            PluginResources.Init(this.Assembly);
        }

        public override void Load()
        {
            PluginLog.Info("ConverterPlugin loaded.");

            // Surface clear, actionable warnings if an engine is missing, instead of silently
            // failing the first time the user runs an action. Both are free:
            //   LibreOffice  -> office formats <-> PDF, PDF -> Word, spreadsheets.
            //   Pandoc       -> Word <-> PowerPoint, Markdown/HTML -> anything.
            if (LibreOfficeConverter.TryLocateSoffice(this, out var sofficePath))
            {
                PluginLog.Info($"Using LibreOffice at: {sofficePath}");
            }
            else
            {
                PluginLog.Warning(
                    "LibreOffice was not found. Most conversions need it. Install it from " +
                    "https://www.libreoffice.org/download/ (or set 'SofficePath' in the plugin settings).");
            }

            if (PandocConverter.TryLocate(this, out var pandocPath))
            {
                PluginLog.Info($"Using Pandoc at: {pandocPath}");
            }
            else
            {
                PluginLog.Warning(
                    "Pandoc was not found. Cross-format conversions such as Word→PowerPoint and " +
                    "Markdown→anything need it. Install it from https://pandoc.org/installing.html " +
                    "(or set 'PandocPath' in the plugin settings).");
            }
        }

        public override void Unload()
        {
            PluginLog.Info("ConverterPlugin unloaded.");
        }
    }
}
