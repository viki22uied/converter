namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    // Decides which file an action should convert.
    //
    // The resolver picks the most recently modified document in the configured source
    // folder. This is deterministic and fully cross-platform (pure System.IO), which keeps
    // the plugin reliable on both Windows and macOS. Temporary and hidden files, and the
    // kinds of files LibreOffice cannot open, are skipped.
    internal static class FileSourceResolver
    {
        // Extensions LibreOffice can open as source documents. Kept deliberately broad.
        private static readonly HashSet<String> ConvertibleExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".doc", ".docx", ".docm", ".dot", ".dotx", ".odt", ".rtf", ".txt", ".html", ".htm", ".md",
            ".xls", ".xlsx", ".xlsm", ".ods", ".csv",
            ".ppt", ".pptx", ".pps", ".ppsx", ".odp",
            ".odg", ".svg", ".wpd", ".pdf",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".tif",
        };

        public static Boolean TryGetLatestFile(String sourceFolder, out String filePath, out String reason)
        {
            filePath = null;
            reason = null;

            if (String.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                reason = $"Source folder does not exist: {sourceFolder}";
                return false;
            }

            FileInfo latest;
            try
            {
                latest = new DirectoryInfo(sourceFolder)
                    .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                    .Where(IsConvertible)
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                reason = $"Could not read source folder: {ex.Message}";
                return false;
            }

            if (latest == null)
            {
                reason = $"No convertible files found in {sourceFolder}";
                return false;
            }

            filePath = latest.FullName;
            return true;
        }

        private static Boolean IsConvertible(FileInfo file)
        {
            if ((file.Attributes & FileAttributes.Hidden) != 0)
            {
                return false;
            }

            var name = file.Name;
            if (name.StartsWith("~") || name.StartsWith(".") || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ConvertibleExtensions.Contains(file.Extension);
        }
    }
}
