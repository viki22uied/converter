namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    // Drives Pandoc for a single conversion step. Pandoc is what makes genuine cross-family
    // conversions possible (Word <-> PowerPoint, Markdown -> anything, HTML -> Word, ...).
    //
    //     pandoc [-f <input format>] <input> -o <output>
    public static class PandocConverter
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

        public static Boolean TryLocate(Plugin plugin, out String path) =>
            TryLocate(new ConverterSettings(plugin).PandocPath, out path);

        public static Boolean TryLocate(String configuredPath, out String path)
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

            var onPath = ProcessRunner.ResolveFromPath(OperatingSystem.IsWindows() ? "pandoc.exe" : "pandoc");
            if (onPath != null)
            {
                path = onPath;
                return true;
            }

            return false;
        }

        public static String RunStep(Plugin plugin, String inputPath, ConversionFormat target, String outputDir, out String error)
        {
            error = null;

            if (!TryLocate(plugin, out var pandoc))
            {
                error = "Pandoc not found.";
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

            var expected = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputPath) + "." + target.Extension);

            // --sandbox: input is often an untrusted download; stop it embedding local files or fetching URLs.
            var args = new List<String> { "--sandbox" };
            var inputFormat = PandocInputFormat(inputPath);
            if (inputFormat != null)
            {
                args.Add("-f");
                args.Add(inputFormat);
            }

            args.Add(inputPath);
            args.Add("-o");
            args.Add(expected);

            try
            {
                var (exitCode, stdout, stderr) = ProcessRunner.Run(pandoc, args, DefaultTimeout);
                if (File.Exists(expected))
                {
                    return expected;
                }

                error = $"Pandoc could not produce {target.DisplayName} (exit {exitCode}). {ProcessRunner.Tail(stderr, stdout)}".Trim();
                return null;
            }
            catch (Exception ex)
            {
                error = $"Pandoc error: {ex.Message}";
                return null;
            }
        }

        // Pandoc infers most input formats from the extension, but plain-text/markdown needs
        // to be stated explicitly.
        private static String PandocInputFormat(String inputPath)
        {
            var ext = (Path.GetExtension(inputPath) ?? String.Empty).ToLowerInvariant();
            return ext switch
            {
                ".md" or ".markdown" or ".txt" or ".text" => "markdown",
                _ => null,
            };
        }

        private static IEnumerable<String> CandidatePaths()
        {
            if (OperatingSystem.IsWindows())
            {
                var pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
                var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                yield return Path.Combine(pf, "Pandoc", "pandoc.exe");
                yield return Path.Combine(localApp, "Pandoc", "pandoc.exe");
            }
            else if (OperatingSystem.IsMacOS())
            {
                yield return "/usr/local/bin/pandoc";
                yield return "/opt/homebrew/bin/pandoc";
            }
            else
            {
                yield return "/usr/bin/pandoc";
                yield return "/usr/local/bin/pandoc";
            }
        }
    }
}
