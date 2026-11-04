namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.IO;

    // Reads and writes the small set of user-configurable options for the plugin.
    //
    // Values are persisted through the host's own plugin-settings store
    // (Plugin.SetPluginSetting / TryGetPluginSetting), so they survive restarts and are
    // namespaced to this plugin. Sensible defaults mean the plugin works out of the box:
    // it converts the most recently modified file in the source folder (Downloads by
    // default) and writes the result next to it.
    public sealed class ConverterSettings
    {
        private const String KeySourceFolder = "SourceFolder";
        private const String KeyOutputFolder = "OutputFolder";
        private const String KeySofficePath = "SofficePath";
        private const String KeyPandocPath = "PandocPath";

        private readonly Plugin _plugin;

        public ConverterSettings(Plugin plugin)
        {
            this._plugin = plugin;
        }

        // Folder scanned for the file to convert. Defaults to the user's Downloads folder,
        // falling back to the Desktop, then the user profile.
        public String SourceFolder
        {
            get
            {
                var value = this.Get(KeySourceFolder);
                return String.IsNullOrWhiteSpace(value) ? DefaultSourceFolder() : value;
            }
            set => this._plugin.SetPluginSetting(KeySourceFolder, value, false);
        }

        // Folder the converted file is written to. Empty means "same folder as the source file".
        public String OutputFolder
        {
            get => this.Get(KeyOutputFolder);
            set => this._plugin.SetPluginSetting(KeyOutputFolder, value, false);
        }

        // Optional explicit path to the LibreOffice `soffice` executable. When empty the
        // converter auto-detects the standard install location for the OS.
        public String SofficePath
        {
            get => this.Get(KeySofficePath);
            set => this._plugin.SetPluginSetting(KeySofficePath, value, false);
        }

        // Optional explicit path to the Pandoc executable. When empty the converter
        // auto-detects the standard install location for the OS.
        public String PandocPath
        {
            get => this.Get(KeyPandocPath);
            set => this._plugin.SetPluginSetting(KeyPandocPath, value, false);
        }

        // Null plugin (e.g. the conversion test harness) means "no saved settings".
        private String Get(String key) =>
            this._plugin != null && this._plugin.TryGetPluginSetting(key, out var value) ? value ?? String.Empty : String.Empty;

        private static String DefaultSourceFolder()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var downloads = Path.Combine(profile, "Downloads");
            if (Directory.Exists(downloads))
            {
                return downloads;
            }

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return Directory.Exists(desktop) ? desktop : profile;
        }
    }
}
