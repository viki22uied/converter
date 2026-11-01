namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // A folder that lays every output format out as its own button, giving the Actions Ring
    // and the MX Creative Keypad a one-tap "convert the latest file to X" grid. Open the
    // folder, then press the format you want.
    public class ConverterFolder : PluginDynamicFolder
    {
        public ConverterFolder()
        {
            this.DisplayName = "File Converter";
            this.Description = "Convert the most recent file to PDF, Word, PowerPoint and more.";
            this.GroupName = "Convert";
        }

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType) =>
            ConversionFormat.All.Select(f => this.CreateCommandName(f.Id));

        public override String GetButtonDisplayName(PluginImageSize imageSize) => "File\nConverter";

        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            var format = ResolveFormat(actionParameter);
            return format == null ? null : format.DisplayName.Replace(" (", "\n(");
        }

        public override void RunCommand(String actionParameter)
        {
            var format = ResolveFormat(actionParameter);
            if (format == null)
            {
                PluginLog.Warning($"ConverterFolder: unknown action '{actionParameter}'.");
                return;
            }

            ConvertRunner.RunLatest(this.Plugin, format);
            this.CommandImageChanged(actionParameter);
        }

        // The folder passes back the full command name (e.g. "ConverterFolder/pdf"); map it
        // to the matching format by its id suffix.
        private static ConversionFormat ResolveFormat(String actionParameter) =>
            ConversionFormat.All.FirstOrDefault(f =>
                actionParameter != null &&
                (actionParameter == f.Id || actionParameter.EndsWith("/" + f.Id, StringComparison.Ordinal) || actionParameter.EndsWith(f.Id, StringComparison.Ordinal)));
    }
}
