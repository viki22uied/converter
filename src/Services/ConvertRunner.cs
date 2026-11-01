namespace Loupedeck.ConverterPlugin
{
    using System;

    // Glue between the actions (button / dial / folder) and the conversion engine:
    // resolve the file to convert, run the conversion and report the outcome to the log.
    internal static class ConvertRunner
    {
        public static ConversionResult Run(Plugin plugin, ConversionFormat format)
        {
            if (format == null)
            {
                return ConversionResult.Fail("Unknown output format.");
            }

            var settings = new ConverterSettings(plugin);

            if (!FileSourceResolver.TryGetLatestFile(settings.SourceFolder, out var inputPath, out var reason))
            {
                PluginLog.Warning($"Nothing to convert: {reason}");
                return ConversionResult.Fail(reason);
            }

            PluginLog.Info($"Converting latest file '{inputPath}' to {format.DisplayName}...");
            var result = LibreOfficeConverter.Convert(plugin, inputPath, format);

            if (result.Success)
            {
                PluginLog.Info(result.Message);
            }
            else
            {
                PluginLog.Error(result.Message);
            }

            return result;
        }
    }
}
