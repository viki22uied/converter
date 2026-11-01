namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    // Orchestrates the conversion engines to deliver genuine any-to-any conversion.
    //
    // For a requested conversion it asks ConversionPlanner for candidate pipelines, then runs
    // them in order through temporary intermediates, keeping the first that actually produces
    // a file. If nothing works it falls back to PDF (which works for virtually any input) so
    // the user always gets a usable result, and if an engine is simply not installed it
    // returns a clear, actionable message telling them what to install.
    public static class SmartConverter
    {
        private const String PandocInstallUrl = "https://pandoc.org/installing.html";
        private const String LibreInstallUrl = "https://www.libreoffice.org/download/";

        public static ConversionResult Convert(Plugin plugin, String inputPath, ConversionFormat requested, String outputFolderOverride = null)
        {
            if (requested == null)
            {
                return ConversionResult.Fail("No output format selected.");
            }

            if (String.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
            {
                return ConversionResult.Fail("Couldn't find a file to convert.");
            }

            var settings = new ConverterSettings(plugin);
            var finalDir =
                !String.IsNullOrWhiteSpace(outputFolderOverride) ? outputFolderOverride :
                !String.IsNullOrWhiteSpace(settings.OutputFolder) ? settings.OutputFolder :
                Path.GetDirectoryName(inputPath);

            try
            {
                Directory.CreateDirectory(finalDir);
            }
            catch (Exception ex)
            {
                return ConversionResult.Fail($"Cannot create output folder '{finalDir}': {ex.Message}");
            }

            var finalPath = Path.Combine(finalDir, Path.GetFileNameWithoutExtension(inputPath) + "." + requested.Extension);

            // Already in the requested format in the requested place: nothing to do.
            if (String.Equals(Path.GetFullPath(finalPath), Path.GetFullPath(inputPath), StringComparison.OrdinalIgnoreCase))
            {
                return ConversionResult.Ok(inputPath, $"Already a {requested.DisplayName}.");
            }

            var engines = EngineAvailability.Snapshot(plugin);

            // Try the planned routes to the requested format.
            if (TryRun(plugin, inputPath, requested, finalPath, engines, out var produced, out var missingTools))
            {
                return ConversionResult.Ok(finalPath, $"Converted to {requested.DisplayName}: {Path.GetFileName(produced)}");
            }

            // Last resort: produce a PDF, which works for almost anything.
            if (!String.Equals(requested.Id, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                var pdf = ConversionFormat.Pdf;
                var pdfPath = Path.Combine(finalDir, Path.GetFileNameWithoutExtension(inputPath) + "." + pdf.Extension);
                if (TryRun(plugin, inputPath, pdf, pdfPath, engines, out var pdfProduced, out _))
                {
                    return ConversionResult.Ok(
                        pdfProduced,
                        $"Couldn't make a {requested.DisplayName} from this file, so it was saved as PDF: {Path.GetFileName(pdfProduced)}",
                        usedFallback: true);
                }
            }

            // Nothing worked. If a required engine is missing, say which one to install.
            if (missingTools.Contains(EngineKind.Pandoc) && !engines.PandocAvailable)
            {
                return ConversionResult.Fail($"Converting to {requested.DisplayName} needs Pandoc (free). Install it from {PandocInstallUrl}, then try again.");
            }

            if (missingTools.Contains(EngineKind.LibreOffice) && !engines.LibreAvailable)
            {
                return ConversionResult.Fail($"Conversion needs LibreOffice (free). Install it from {LibreInstallUrl}, then try again.");
            }

            return ConversionResult.Fail($"Couldn't convert {Path.GetFileName(inputPath)} to {requested.DisplayName}. The file may be open, corrupted or password-protected.");
        }

        // Runs candidate pipelines in order; on the first that produces a file, moves it to
        // `finalPath` and returns true. `missingTools` collects engines that would have been
        // needed but are not installed, so the caller can advise the user.
        private static Boolean TryRun(
            Plugin plugin,
            String inputPath,
            ConversionFormat target,
            String finalPath,
            EngineAvailability engines,
            out String producedPath,
            out HashSet<EngineKind> missingTools)
        {
            producedPath = null;
            missingTools = new HashSet<EngineKind>();

            var candidates = ConversionPlanner.BuildCandidates(inputPath, target);
            foreach (var pipeline in candidates)
            {
                // Skip pipelines whose engines aren't all installed, noting what's missing.
                var unavailable = pipeline.Select(s => s.Engine).Where(e => !engines.IsAvailable(e)).Distinct().ToList();
                if (unavailable.Count > 0)
                {
                    foreach (var e in unavailable)
                    {
                        missingTools.Add(e);
                    }

                    continue;
                }

                if (Execute(plugin, inputPath, pipeline, finalPath, out producedPath))
                {
                    PluginLog.Info($"Pipeline [{String.Join(", ", pipeline)}] succeeded.");
                    return true;
                }
            }

            return false;
        }

        // Executes one pipeline through a temp working directory, then moves the final output
        // to `finalPath`.
        private static Boolean Execute(Plugin plugin, String inputPath, IReadOnlyList<ConversionStep> pipeline, String finalPath, out String producedPath)
        {
            producedPath = null;
            var work = Path.Combine(Path.GetTempPath(), "LogiConvertWork_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(work);
                var current = inputPath;

                foreach (var step in pipeline)
                {
                    var output = step.Engine == EngineKind.Pandoc
                        ? PandocConverter.RunStep(plugin, current, step.Target, work, out var err)
                        : LibreOfficeConverter.RunStep(plugin, current, step.Target, work, out err);

                    if (output == null)
                    {
                        PluginLog.Verbose($"Step {step} failed: {err}");
                        return false;
                    }

                    current = output;
                }

                // Move the final intermediate to the requested destination. The temp working
                // directory is often on a different volume than the output folder, where
                // File.Move can fail, so fall back to copy + delete.
                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }

                try
                {
                    File.Move(current, finalPath);
                }
                catch (IOException)
                {
                    File.Copy(current, finalPath, overwrite: true);
                }

                producedPath = finalPath;
                return true;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Pipeline execution error");
                return false;
            }
            finally
            {
                ProcessRunner.TryDeleteDirectory(work);
            }
        }
    }

    // A snapshot of which engines are installed, computed once per conversion.
    internal sealed class EngineAvailability
    {
        public Boolean LibreAvailable { get; private init; }

        public Boolean PandocAvailable { get; private init; }

        public static EngineAvailability Snapshot(Plugin plugin) => new()
        {
            LibreAvailable = LibreOfficeConverter.TryLocateSoffice(plugin, out _),
            PandocAvailable = PandocConverter.TryLocate(plugin, out _),
        };

        public Boolean IsAvailable(EngineKind kind) =>
            kind == EngineKind.Pandoc ? this.PandocAvailable : this.LibreAvailable;
    }
}
