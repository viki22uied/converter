namespace Loupedeck.ConverterPlugin
{
    using System;

    // A single command exposing one button action per output format:
    // "Convert to PDF", "Convert to Word (DOCX)", "Convert to PowerPoint (PPTX)", etc.
    //
    // Each action converts the most recently modified file in the configured source folder
    // (Downloads by default) to the chosen format. These actions appear in Logi Options+
    // under the "Convert Latest File" group and can be assigned to the MX Creative Keypad,
    // a dial press, or the Actions Ring.
    public class ConvertFileCommand : PluginDynamicCommand
    {
        private const String GroupName = "Convert Latest File";

        // The display name shown on a button briefly reflects the last conversion result.
        private String _lastStatus = String.Empty;

        public ConvertFileCommand()
            : base()
        {
            foreach (var format in ConversionFormat.All)
            {
                this.AddParameter(format.Id, $"Convert to {format.DisplayName}", GroupName);
            }
        }

        protected override void RunCommand(String actionParameter)
        {
            var format = ConversionFormat.ById(actionParameter);
            if (format == null)
            {
                PluginLog.Warning($"Unknown convert action parameter '{actionParameter}'.");
                return;
            }

            var result = ConvertRunner.RunLatest(this.Plugin, format);
            this._lastStatus = result.Success ? "Done" : "Failed";
            this.ActionImageChanged(actionParameter);
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            var format = ConversionFormat.ById(actionParameter);
            if (format == null)
            {
                return null;
            }

            // Two lines: the target format and the latest status, when available.
            return String.IsNullOrEmpty(this._lastStatus)
                ? $"To {format.DisplayName}"
                : $"To {format.DisplayName}{Environment.NewLine}{this._lastStatus}";
        }
    }
}
