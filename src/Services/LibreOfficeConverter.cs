namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    // The result of a conversion attempt.
    public sealed class ConversionResult
    {
        public Boolean Success { get; init; }

        public String OutputPath { get; init; }

        public String Message { get; init; }

        // True when the requested format could not be produced and a PDF was created instead.
        public Boolean UsedFallback { get; init; }

        public static ConversionResult Ok(String outputPath, String message, Boolean usedFallback = false) =>
            new() { Success = true, OutputPath = outputPath, Message = message, UsedFallback = usedFallback };

        public static ConversionResult Fail(String message) =>
            new() { Success = false, Message = message };
    }

    // One of the external conversion engines the plugin drives.
    public enum EngineKind
    {
        LibreOffice,
        Pandoc,
    }

    // Drives a headless LibreOffice process to perform a single conversion step.
    //
    //     soffice --headless --norestore --convert-to <filter> --outdir <dir> <input>
    //
    // A private, per-step user-installation profile is used so conversions still work while
    // the user has LibreOffice open, and concurrent conversions do not clash.
    public static class LibreOfficeConverter
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

        public static Boolean TryLocateSoffice(Plugin plugin, out String path) =>
            TryLocateSoffice(new ConverterSettings(plugin).SofficePath, out path);

        public static Boolean TryLocateSoffice(String configuredPath, out String path)
        {
            path = null;

            if (!String.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            {
                path = configuredPath;
                return true;
            }

            foreach (var candidate in CandidatePaths())
            {
                if (File.Exists(candidate))
                {
                    path = candidate;
                    return true;
                }
            }

            var onPath = ProcessRunner.ResolveFromPath(OperatingSystem.IsWindows() ? "soffice.exe" : "soffice");
            if (onPath != null)
            {
                path = onPath;
                return true;
            }

            return false;
        }

        // Runs a single LibreOffice conversion of `inputPath` to `target` into `outputDir`.
        // Returns the output file path on success, or null (with `error` set) on failure.
        public static String RunStep(Plugin plugin, String inputPath, ConversionFormat target, String outputDir, out String error)
        {
            error = null;

            if (!TryLocateSoffice(plugin, out var soffice))
            {
                error = "LibreOffice not found.";
                return null;
            }

            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                error = $"Cannot create output folder: {ex.Message}";
                return null;
            }

            var profileDir = Path.Combine(Path.GetTempPath(), "LogiConverter_" + Guid.NewGuid().ToString("N"));
            var args = new List<String>
            {
                "--headless",
                "--norestore",
                "--nologo",
                "--nofirststartwizard",
                $"-env:UserInstallation={new Uri(profileDir).AbsoluteUri}",
            };

            // Help LibreOffice open PDFs as editable documents (PDF -> Word, etc.).
            if (String.Equals(Path.GetExtension(inputPath), ".pdf", StringComparison.OrdinalIgnoreCase)
                && !String.Equals(target.Extension, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                args.Add("--infilter=writer_pdf_import");
            }

            args.Add("--convert-to");
            args.Add(target.Filter);
            args.Add("--outdir");
            args.Add(outputDir);
            args.Add(inputPath);

            try
            {
                var (exitCode, stdout, stderr) = ProcessRunner.Run(soffice, args, DefaultTimeout);
                var expected = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputPath) + "." + target.Extension);

                if (File.Exists(expected))
                {
                    return expected;
                }

                error = $"LibreOffice could not produce {target.DisplayName} (exit {exitCode}). {ProcessRunner.Tail(stderr, stdout)}".Trim();
                return null;
            }
            catch (Exception ex)
            {
                error = $"LibreOffice error: {ex.Message}";
                return null;
            }
            finally
            {
                ProcessRunner.TryDeleteDirectory(profileDir);
            }
        }

        private static IEnumerable<String> CandidatePaths()
        {
            if (OperatingSystem.IsWindows())
            {
                var pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
                var pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
                yield return Path.Combine(pf, "LibreOffice", "program", "soffice.exe");
                yield return Path.Combine(pf86, "LibreOffice", "program", "soffice.exe");
            }
            else if (OperatingSystem.IsMacOS())
            {
                yield return "/Applications/LibreOffice.app/Contents/MacOS/soffice";
            }
            else
            {
                yield return "/usr/bin/soffice";
                yield return "/usr/local/bin/soffice";
                yield return "/opt/libreoffice/program/soffice";
            }
        }
    }
}
