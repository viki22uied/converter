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

            // Surface a clear, actionable warning if the conversion engine is missing,
            // instead of silently failing the first time the user runs an action.
            if (!LibreOfficeConverter.TryLocateSoffice(this, out var sofficePath))
            {
                PluginLog.Warning(
                    "LibreOffice was not found. File conversion will not work until LibreOffice is installed " +
                    "or its path is set in the plugin settings (key 'SofficePath'). Download it from https://www.libreoffice.org/download/.");
            }
            else
            {
                PluginLog.Info($"Using LibreOffice at: {sofficePath}");
            }
        }

        public override void Unload()
        {
            PluginLog.Info("ConverterPlugin unloaded.");
        }
    }
}
