namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;

    // Options for a single conversion run.
    internal sealed class ConvertRequest
    {
        public ConversionFormat Format { get; init; }

        // Explicit file to convert. When null/empty, the latest file in SourceFolder is used.
        public String InputPath { get; init; }

        // Folder scanned for the latest file. When null/empty, the configured default is used.
        public String SourceFolder { get; init; }

        // Where the converted file is written. When null/empty, the configured default is used.
        public String OutputFolder { get; init; }

        // Open the converted file in its default application afterwards.
        public Boolean OpenAfter { get; init; }
    }

    // Glue between the actions (button / dial / folder / editor action) and the conversion
    // engine: resolve the file to convert, run the smart conversion, surface a friendly
    // toast to the user and optionally open the result.
    internal static class ConvertRunner
    {
        // Convenience for the quick one-tap actions: convert the latest file in the default folder.
        public static ConversionResult RunLatest(Plugin plugin, ConversionFormat format) =>
            Run(plugin, new ConvertRequest { Format = format });

        public static ConversionResult Run(Plugin plugin, ConvertRequest request)
        {
            if (request?.Format == null)
            {
                return Report(plugin, ConversionResult.Fail("No output format selected."));
            }

            var settings = new ConverterSettings(plugin);

            // Resolve the input file.
            String inputPath;
            if (!String.IsNullOrWhiteSpace(request.InputPath))
            {
                if (!File.Exists(request.InputPath))
                {
                    return Report(plugin, ConversionResult.Fail($"File not found: {request.InputPath}"));
                }

                inputPath = request.InputPath;
            }
            else
            {
                var folder = String.IsNullOrWhiteSpace(request.SourceFolder) ? settings.SourceFolder : request.SourceFolder;
                if (!FileSourceResolver.TryGetLatestFile(folder, out inputPath, out var reason))
                {
                    return Report(plugin, ConversionResult.Fail(reason));
                }
            }

            PluginLog.Info($"Converting '{inputPath}' to {request.Format.DisplayName}...");
            var result = SmartConverter.Convert(plugin, inputPath, request.Format, request.OutputFolder);

            if (result.Success && request.OpenAfter)
            {
                TryOpen(result.OutputPath);
            }

            return Report(plugin, result);
        }

        // Pushes the outcome to the plugin status line (a toast in Logi Options+) and the log.
        private static ConversionResult Report(Plugin plugin, ConversionResult result)
        {
            if (result.Success)
            {
                PluginLog.Info(result.Message);
                plugin?.OnPluginStatusChanged(PluginStatus.Normal, result.Message);
            }
            else
            {
                PluginLog.Error(result.Message);
                plugin?.OnPluginStatusChanged(PluginStatus.Error, result.Message);
            }

            return result;
        }

        private static void TryOpen(String path)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", $"\"{path}\"");
                }
                else
                {
                    Process.Start("xdg-open", $"\"{path}\"");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Could not open the converted file.");
            }
        }
    }
}
