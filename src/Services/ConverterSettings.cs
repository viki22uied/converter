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
                if (this._plugin.TryGetPluginSetting(KeySourceFolder, out var value) && !String.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                return DefaultSourceFolder();
            }
            set => this._plugin.SetPluginSetting(KeySourceFolder, value, false);
        }

        // Folder the converted file is written to. Empty means "same folder as the source file".
        public String OutputFolder
        {
            get => this._plugin.TryGetPluginSetting(KeyOutputFolder, out var value) ? value : String.Empty;
            set => this._plugin.SetPluginSetting(KeyOutputFolder, value, false);
        }

        // Optional explicit path to the LibreOffice `soffice` executable. When empty the
        // converter auto-detects the standard install location for the OS.
        public String SofficePath
        {
            get => this._plugin.TryGetPluginSetting(KeySofficePath, out var value) ? value : String.Empty;
            set => this._plugin.SetPluginSetting(KeySofficePath, value, false);
        }

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
