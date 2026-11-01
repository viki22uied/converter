namespace Loupedeck.ConverterPlugin
{
    using System;

    // The main, fully configurable convert action. Added to an MX Creative Keypad key, a
    // dial, or the Logi Options+ Actions Ring (which can be triggered from a mouse), it opens
    // a small settings panel in Options+ where the user picks:
    //
    //   * the output format (PDF, Word, PowerPoint, Excel, ...),
    //   * optionally a specific file to convert (otherwise the newest file in a folder),
    //   * the folder to watch, and where to save the result,
    //   * whether to open the converted file afterwards.
    //
    // Pressing it runs the smart any-to-any conversion and shows a success/error toast.
    public class ConvertAction : ActionEditorCommand
    {
        private const String ControlTarget = "target";
        private const String ControlFile = "file";
        private const String ControlFolder = "folder";
        private const String ControlOutput = "outputFolder";
        private const String ControlOpen = "openAfter";

        public ConvertAction()
        {
            this.Name = "ConvertFile";
            this.DisplayName = "Convert a File";
            this.GroupName = "Convert";
            this.Description = "Convert a file to PDF, Word, PowerPoint, Excel and more.";

            this.ActionEditor.AddControlEx(
                new ActionEditorListbox(ControlTarget, "Convert to"));

            this.ActionEditor.AddControlEx(
                new ActionEditorFileSelector(ControlFile, "File to convert (optional)"));

            this.ActionEditor.AddControlEx(
                new ActionEditorTextbox(ControlFolder, "…or newest file in this folder")
                    .SetPlaceholder("Leave empty to use your Downloads folder"));

            this.ActionEditor.AddControlEx(
                new ActionEditorTextbox(ControlOutput, "Save the converted file to")
                    .SetPlaceholder("Leave empty to choose in a Save As window each time"));

            this.ActionEditor.AddControlEx(
                new ActionEditorCheckbox(ControlOpen, "Open the file after converting")
                    .SetDefaultValue(false));

            this.ActionEditor.ListboxItemsRequested += this.OnListboxItemsRequested;
        }

        protected override Boolean RunCommand(ActionEditorActionParameters actionParameters)
        {
            var target = ConversionFormat.ById(actionParameters.GetString(ControlTarget)) ?? ConversionFormat.Pdf;

            var file = actionParameters.GetString(ControlFile);
            var folder = actionParameters.GetString(ControlFolder);
            var outputFolder = actionParameters.GetString(ControlOutput);
            var openAfter = actionParameters.TryGetBoolean(ControlOpen, out var open) && open;

            var request = new ConvertRequest
            {
                Format = target,
                InputPath = String.IsNullOrWhiteSpace(file) ? null : file,
                SourceFolder = folder,
                OutputFolder = outputFolder,
                OpenAfter = openAfter,
            };

            var result = ConvertRunner.Run(this.Plugin, request);
            return result.Success;
        }

        protected override String GetCommandDisplayName(ActionEditorActionParameters actionParameters)
        {
            var target = ConversionFormat.ById(actionParameters.GetString(ControlTarget));
            return target == null ? "Convert a File" : $"Convert → {target.DisplayName}";
        }

        protected override BitmapImage GetCommandImage(ActionEditorActionParameters actionParameters, Int32 imageWidth, Int32 imageHeight)
        {
            var target = ConversionFormat.ById(actionParameters.GetString(ControlTarget));
            var label = target == null ? "Convert" : target.Extension.ToUpperInvariant();

            try
            {
                using var builder = new BitmapBuilder(imageWidth, imageHeight);
                builder.Clear(new BitmapColor(28, 115, 232));

                try
                {
                    var icon = PluginResources.ReadImage("convert-icon.png");
                    var iconSize = (Int32)(imageHeight * 0.52);
                    builder.DrawImage(icon, (imageWidth - iconSize) / 2, (Int32)(imageHeight * 0.08), iconSize, iconSize);
                }
                catch
                {
                    // Icon is optional; the label below is enough.
                }

                builder.DrawText(label, 0, (Int32)(imageHeight * 0.60), imageWidth, (Int32)(imageHeight * 0.36), BitmapColor.White, (Int32)(imageHeight * 0.22));
                return builder.ToImage();
            }
            catch
            {
                return null;
            }
        }

        private void OnListboxItemsRequested(Object sender, ActionEditorListboxItemsRequestedEventArgs e)
        {
            if (!e.ControlName.EqualsNoCase(ControlTarget))
            {
                return;
            }

            foreach (var format in ConversionFormat.All)
            {
                e.AddItem(format.Id, format.DisplayName, $"Convert the file to {format.DisplayName}");
            }

            var current = e.ActionEditorState?.GetControlValue(ControlTarget) as String;
            if (!String.IsNullOrEmpty(current) && ConversionFormat.ById(current) != null)
            {
                e.SetSelectedItemName(current);
            }
            else
            {
                e.SetSelectedItemName("pdf");
            }
        }
    }
}
