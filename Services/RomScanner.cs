using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using SharpCompress.Archives;

namespace RomRebuilderUI.Services
{
    public class ScannedRomFile
    {
        public string FilePath { get; set; } = string.Empty;       // Path to archive or loose file
        public string InternalName { get; set; } = string.Empty;   // Filename inside archive or standalone filename
        public long Size { get; set; }                             // File or entry size in bytes
        public string Hash { get; set; } = string.Empty;           // SHA1 or CRC hash for DAT matching
        public bool IsArchive { get; set; }                        // True if inside an archive, false if loose file
    }

    public static class RomScanner
    {
        private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".7z", ".rar"
        };

        /// <summary>
        /// Recursively scans multiple source directories for archives (.zip, .7z, .rar) and loose ROM files.
        /// </summary>
        public static List<ScannedRomFile> ScanDirectories(IEnumerable<string> sourceDirs)
        {
            var discoveredRoms = new List<ScannedRomFile>();

            if (sourceDirs == null)
                return discoveredRoms;

            foreach (var sourceDir in sourceDirs)
            {
                if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
                    continue;

                var allFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);

                foreach (var filePath in allFiles)
                {
                    string ext = Path.GetExtension(filePath);

                    if (ArchiveExtensions.Contains(ext))
                    {
                        try
                        {
                            using var archive = ArchiveFactory.OpenArchive(filePath);
                            foreach (var entry in archive.Entries)
                            {
                                if (entry.IsDirectory || string.IsNullOrEmpty(entry.Key)) continue;

                                discoveredRoms.Add(new ScannedRomFile
                                {
                                    FilePath = filePath,
                                    InternalName = entry.Key,
                                    Size = entry.Size,
                                    IsArchive = true,
                                    Hash = string.Empty
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to read archive {filePath}: {ex.Message}");
                        }
                    }
                    else
                    {
                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            discoveredRoms.Add(new ScannedRomFile
                            {
                                FilePath = filePath,
                                InternalName = fileInfo.Name,
                                Size = fileInfo.Length,
                                IsArchive = false,
                                Hash = CalculateSha1(filePath)
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to read loose file {filePath}: {ex.Message}");
                        }
                    }
                }
            }

            return discoveredRoms;
        }

        public static List<ScannedRomFile> ScanDirectory(string sourceDir)
        {
            return ScanDirectories(new[] { sourceDir });
        }

        private static string CalculateSha1(string filePath)
        {
            try
            {
                using var fs = File.OpenRead(filePath);
                using var sha1 = SHA1.Create();
                byte[] hashBytes = sha1.ComputeHash(fs);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to calculate SHA1 for {filePath}: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
