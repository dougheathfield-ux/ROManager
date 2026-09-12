using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using RomRebuilderUI.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace RomRebuilderUI.Services
{
    public class RebuildProgressReport
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string CurrentMessage { get; set; } = string.Empty;
    }

    public class RebuildResult
    {
        public int Processed { get; set; }
        public int Moved { get; set; }
        public int Failed { get; set; }
        public int UnknownFilesHandled { get; set; }
    }

    public class RomRebuilderService
    {
        private static readonly uint[] CrcTable = MakeCrcTable();
        private static uint[] MakeCrcTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((c & 1) != 0)
                        c = 0xedb88320 ^ (c >> 1);
                    else
                        c >>= 1;
                }
                table[i] = c;
            }
            return table;
        }

        private string CalculateCrc32(string filePath)
        {
            try
            {
                using var fs = File.OpenRead(filePath);
                uint crc = 0xffffffff;
                byte[] buffer = new byte[8192];
                int count;
                while ((count = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        crc = (crc >> 8) ^ CrcTable[(crc & 0xff) ^ buffer[i]];
                    }
                }
                return (~crc).ToString("X8");
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task<AuditSummary> RunMameAudit(IEnumerable<string> sourceDirs, string datPath, RebuildMode mode, IProgress<RebuildProgressReport>? progress = null, Action<MachineAuditItem>? onItemAudited = null)
        {
            return await AuditMameSetsAsync(sourceDirs, datPath, progress, onItemAudited);
        }

        public async Task<AuditSummary> AuditMameSetsAsync(IEnumerable<string> sourceDirs, string datPath, IProgress<RebuildProgressReport>? progress, Action<MachineAuditItem>? onItemAudited = null)
        {
            return await AuditConsoleSetsAsync(sourceDirs, datPath, false, "", datPath, progress, onItemAudited);
        }

        public async Task<RebuildResult> RunMameRebuild(IEnumerable<string> sourceDirs, string outputDir, string datPath, RebuildMode mode, OutputFormat format, IProgress<RebuildProgressReport>? progress = null)
        {
            return await RebuildMameSetsAsync(sourceDirs, outputDir, datPath, mode, format, progress);
        }

        public async Task<AuditSummary> RunConsoleAudit(IEnumerable<string> sourceDirs, string datPath, bool enable1G1R, string regionPriorities, IProgress<RebuildProgressReport>? progress = null, Action<MachineAuditItem>? onItemAudited = null)
        {
            return await AuditConsoleSetsAsync(sourceDirs, datPath, enable1G1R, regionPriorities, datPath, progress, onItemAudited);
        }

        public async Task<RebuildResult> RunConsoleRebuild(IEnumerable<string> sourceDirs, string outputDir, string datPath, bool enable1G1R, string regionPriorities, bool sortIntoRegionFolders, OutputFormat format, IProgress<RebuildProgressReport>? progress = null)
        {
            return await RebuildConsoleSetsAsync(sourceDirs, outputDir, datPath, enable1G1R, regionPriorities, sortIntoRegionFolders, format, datPath, progress);
        }

        private class RomDatInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public long Size { get; set; }
            public string Crc { get; set; } = string.Empty;
        }

        private class ConsoleGameEntry
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public List<RomDatInfo> RequiredRoms { get; set; } = new();
        }

        private List<ConsoleGameEntry> LoadConsoleDatGames(string datPath)
        {
            var games = new List<ConsoleGameEntry>();
            if (string.IsNullOrEmpty(datPath) || !File.Exists(datPath)) return games;

            try
            {
                var doc = XDocument.Load(datPath);
                var elements = doc.Descendants("machine").Concat(doc.Descendants("game"));

                foreach (var el in elements)
                {
                    string? name = el.Attribute("name")?.Value;
                    string? description = el.Attribute("description")?.Value ?? name;
                    if (string.IsNullOrEmpty(name)) continue;

                    var gameEntry = new ConsoleGameEntry
                    {
                        Name = name,
                        Description = description ?? name
                    };

                    foreach (var romEl in el.Descendants("rom"))
                    {
                        string? romName = romEl.Attribute("name")?.Value;
                        string? sizeStr = romEl.Attribute("size")?.Value;
                        string? crcStr = romEl.Attribute("crc")?.Value;

                        if (!string.IsNullOrEmpty(romName) && long.TryParse(sizeStr, out long size) && !string.IsNullOrEmpty(crcStr))
                        {
                            gameEntry.RequiredRoms.Add(new RomDatInfo
                            {
                                Name = romName,
                                Description = gameEntry.Description,
                                Size = size,
                                Crc = crcStr.ToUpperInvariant()
                            });
                        }
                    }

                    if (gameEntry.RequiredRoms.Count > 0)
                    {
                        games.Add(gameEntry);
                    }
                }
            }
            catch { }
            return games;
        }

        public async Task<AuditSummary> AuditConsoleSetsAsync(IEnumerable<string> sourceDirs, string datPath, bool enable1G1R, string regionPriorities, string actualDatPath, IProgress<RebuildProgressReport>? progress, Action<MachineAuditItem>? onItemAudited = null)
        {
            var summary = new AuditSummary();
            var validDirs = sourceDirs?.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).ToList() ?? new List<string>();
            if (validDirs.Count == 0)
            {
                progress?.Report(new RebuildProgressReport { CurrentMessage = "Error: No valid Source Directories specified." });
                return summary;
            }

            progress?.Report(new RebuildProgressReport { CurrentMessage = $"Loading Console DAT file: {actualDatPath}..." });
            var consoleGames = LoadConsoleDatGames(actualDatPath);
            if (consoleGames.Count == 0)
            {
                progress?.Report(new RebuildProgressReport { CurrentMessage = "Error: No games found in DAT definitions or invalid DAT." });
                return summary;
            }

            progress?.Report(new RebuildProgressReport { CurrentMessage = "Scanning source ROM files..." });
            var scannedRoms = new List<ScannedRomFile>();
            foreach (var dir in validDirs)
            {
                var filesInDir = await Task.Run(() => RomScanner.ScanDirectory(dir));
                scannedRoms.AddRange(filesInDir);
            }

            string tempRoot = Path.Combine(Path.GetTempPath(), "RomRebuilderConsoleAudit_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRoot);

            var availableRoms = new Dictionary<(long size, string crc), (string filePath, string displayName)>();
            var availableFilesByName = new Dictionary<string, (string filePath, long size, string crc)>(StringComparer.OrdinalIgnoreCase);
            var extractedPathToSourceRomMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var matchedSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var rom in scannedRoms)
                {
                    string fileToAnalyze = rom.FilePath;
                    long fileSize = rom.Size;

                    if (rom.IsArchive)
                    {
                        string extractSubDir = Path.Combine(tempRoot, Path.GetFileNameWithoutExtension(rom.FilePath) + "_" + Guid.NewGuid().ToString());
                        Directory.CreateDirectory(extractSubDir);
                        try
                        {
                            using var archive = ArchiveFactory.OpenArchive(rom.FilePath);
                            foreach (var entry in archive.Entries)
                            {
                                if (!entry.IsDirectory && !string.IsNullOrEmpty(entry.Key))
                                {
                                    entry.WriteToDirectory(extractSubDir, new ExtractionOptions
                                    {
                                        ExtractFullPath = true,
                                        Overwrite = true
                                    });
                                }
                            }

                            foreach (var extractedFile in Directory.GetFiles(extractSubDir, "*.*", SearchOption.AllDirectories))
                            {
                                string crc = CalculateCrc32(extractedFile);
                                long size = new FileInfo(extractedFile).Length;
                                var key = (size, crc.ToUpperInvariant());
                                
                                availableRoms[key] = (extractedFile, Path.GetFileName(extractedFile));
                                availableFilesByName[Path.GetFileName(extractedFile)] = (extractedFile, size, crc);
                                extractedPathToSourceRomMap[extractedFile] = rom.FilePath;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to extract archive {rom.FilePath}: {ex.Message}");
                        }
                    }
                    else
                    {
                        string crc = CalculateCrc32(fileToAnalyze);
                        var key = (fileSize, crc.ToUpperInvariant());
                        
                        availableRoms[key] = (fileToAnalyze, Path.GetFileName(fileToAnalyze));
                        availableFilesByName[Path.GetFileName(fileToAnalyze)] = (fileToAnalyze, fileSize, crc);
                        extractedPathToSourceRomMap[fileToAnalyze] = rom.FilePath;
                    }
                }

                int index = 0;
                int completeCount = 0;
                int incompleteCount = 0;
                int missingCount = 0;

                foreach (var game in consoleGames)
                {
                    index++;
                    int foundCount = 0;
                    int totalRequired = game.RequiredRoms.Count;
                    var missingFilesList = new List<string>();

                    foreach (var reqRom in game.RequiredRoms)
                    {
                        var key = (reqRom.Size, reqRom.Crc);
                        if (availableRoms.TryGetValue(key, out var matchedRom))
                        {
                            foundCount++;
                            if (extractedPathToSourceRomMap.TryGetValue(matchedRom.filePath, out var sourcePath))
                            {
                                matchedSourcePaths.Add(sourcePath);
                            }
                        }
                        else if (reqRom.Name.EndsWith(".cue", StringComparison.OrdinalIgnoreCase) && availableFilesByName.TryGetValue(reqRom.Name, out var matchedByName))
                        {
                            foundCount++;
                            if (extractedPathToSourceRomMap.TryGetValue(matchedByName.filePath, out var sourcePath))
                            {
                                matchedSourcePaths.Add(sourcePath);
                            }
                        }
                        else
                        {
                            missingFilesList.Add(reqRom.Name);
                        }
                    }

                    string status;
                    if (foundCount == totalRequired)
                    {
                        status = "Complete";
                        completeCount++;
                    }
                    else if (foundCount > 0)
                    {
                        status = "Incomplete";
                        incompleteCount++;
                    }
                    else
                    {
                        status = "Missing";
                        missingCount++;
                    }

                    string region = ExtractRegion(game.Description, regionPriorities);
                    string summaryText = string.Format("{0}/{1} files missing", missingFilesList.Count, totalRequired);

                    var auditItem = new MachineAuditItem
                    {
                        Name = game.Description,
                        Description = game.Name,
                        Status = status,
                        Region = region,
                        RomCountSummary = summaryText,
                        MissingFiles = missingFilesList
                    };

                    summary.Items.Add(auditItem);
                    onItemAudited?.Invoke(auditItem);

                    progress?.Report(new RebuildProgressReport 
                    { 
                        Current = index, 
                        Total = consoleGames.Count, 
                        CurrentMessage = $"Auditing: {game.Description}" 
                    });

                    await Task.Delay(2);
                }

                var unknownFilesList = new List<string>();
                foreach (var scannedRom in scannedRoms)
                {
                    if (!matchedSourcePaths.Contains(scannedRom.FilePath))
                    {
                        string ext = Path.GetExtension(scannedRom.FilePath);
                        if (!string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(ext, ".nfo", StringComparison.OrdinalIgnoreCase))
                        {
                            unknownFilesList.Add(scannedRom.FilePath);
                        }
                    }
                }

                summary.UnknownFiles = unknownFilesList;
                summary.TotalFiles = consoleGames.Count;
                summary.ValidFiles = completeCount;
                progress?.Report(new RebuildProgressReport { CurrentMessage = $"Audit complete. Complete: {completeCount}, Incomplete: {incompleteCount}, Missing: {missingCount}, Unknown/Unneeded: {unknownFilesList.Count}" });
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    try { Directory.Delete(tempRoot, true); } catch { }
                }
            }

            return summary;
        }

        public async Task<RebuildResult> RebuildMameSetsAsync(IEnumerable<string> sourceDirs, string outputDir, string datPath, RebuildMode mode, OutputFormat format, IProgress<RebuildProgressReport>? progress)
        {
            return new RebuildResult();
        }

        public async Task<RebuildResult> RebuildConsoleSetsAsync(IEnumerable<string> sourceDirs, string outputDir, string datPath, bool enable1G1R, string regionPriorities, bool sortIntoRegionFolders, OutputFormat format, string actualDatPath, IProgress<RebuildProgressReport>? progress)
        {
            var result = new RebuildResult();
            if (string.IsNullOrEmpty(outputDir))
            {
                progress?.Report(new RebuildProgressReport { CurrentMessage = "Error: Output directory is not specified." });
                return result;
            }

            var validDirs = sourceDirs?.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).ToList() ?? new List<string>();
            if (validDirs.Count == 0)
            {
                progress?.Report(new RebuildProgressReport { CurrentMessage = "Error: No valid Source Directories specified." });
                return result;
            }

            string datFileName = Path.GetFileNameWithoutExtension(actualDatPath);
            string vendor = "Consoles";
            string system = datFileName;

            if (!string.IsNullOrWhiteSpace(datFileName) && datFileName.Contains(" - "))
            {
                var parts = datFileName.Split(new[] { " - " }, 2, StringSplitOptions.None);
                vendor = parts[0].Trim();
                system = parts[1].Trim();
            }
            else if (!string.IsNullOrWhiteSpace(datFileName))
            {
                system = datFileName;
            }

            string targetOutputDir = Path.Combine(outputDir, vendor, system);
            string unknownOutputDir = Path.Combine(targetOutputDir, "_Unknown");
            Directory.CreateDirectory(targetOutputDir);

            progress?.Report(new RebuildProgressReport { CurrentMessage = $"Loading DAT file for rebuild: {actualDatPath}..." });
            var consoleGames = LoadConsoleDatGames(actualDatPath);
            if (consoleGames.Count == 0)
            {
                progress?.Report(new RebuildProgressReport { CurrentMessage = "Error: No games found in DAT definitions." });
                return result;
            }

            progress?.Report(new RebuildProgressReport { CurrentMessage = "Scanning source ROM files..." });
            var scannedRoms = new List<ScannedRomFile>();
            foreach (var dir in validDirs)
            {
                var filesInDir = await Task.Run(() => RomScanner.ScanDirectory(dir));
                scannedRoms.AddRange(filesInDir);
            }

            string tempRoot = Path.Combine(Path.GetTempPath(), "RomRebuilderConsoleRebuild_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRoot);

            var availableRoms = new Dictionary<(long size, string crc), string>();
            var availableFilesByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var extractedPathToSourceRomMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var matchedSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var rom in scannedRoms)
                {
                    string fileToAnalyze = rom.FilePath;
                    if (rom.IsArchive)
                    {
                        string extractSubDir = Path.Combine(tempRoot, Path.GetFileNameWithoutExtension(rom.FilePath) + "_" + Guid.NewGuid().ToString());
                        Directory.CreateDirectory(extractSubDir);
                        try
                        {
                            using var archive = ArchiveFactory.OpenArchive(rom.FilePath);
                            foreach (var entry in archive.Entries)
                            {
                                if (!entry.IsDirectory && !string.IsNullOrEmpty(entry.Key))
                                {
                                    entry.WriteToDirectory(extractSubDir, new ExtractionOptions
                                    {
                                        ExtractFullPath = true,
                                        Overwrite = true
                                    });
                                }
                            }

                            foreach (var extractedFile in Directory.GetFiles(extractSubDir, "*.*", SearchOption.AllDirectories))
                            {
                                string crc = CalculateCrc32(extractedFile);
                                long size = new FileInfo(extractedFile).Length;
                                var key = (size, crc.ToUpperInvariant());
                                if (!availableRoms.ContainsKey(key))
                                {
                                    availableRoms[key] = extractedFile;
                                }
                                string fileName = Path.GetFileName(extractedFile);
                                if (!availableFilesByName.ContainsKey(fileName))
                                {
                                    availableFilesByName[fileName] = extractedFile;
                                }
                                extractedPathToSourceRomMap[extractedFile] = rom.FilePath;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to extract archive {rom.FilePath}: {ex.Message}");
                        }
                    }
                    else
                    {
                        string crc = CalculateCrc32(fileToAnalyze);
                        long size = new FileInfo(fileToAnalyze).Length;
                        var key = (size, crc.ToUpperInvariant());
                        if (!availableRoms.ContainsKey(key))
                        {
                            availableRoms[key] = fileToAnalyze;
                        }
                        string fileName = Path.GetFileName(fileToAnalyze);
                        if (!availableFilesByName.ContainsKey(fileName))
                        {
                          availableFilesByName[fileName] = fileToAnalyze;
                        }
                        extractedPathToSourceRomMap[fileToAnalyze] = rom.FilePath;
                    }
                }

                int index = 0;
                foreach (var game in consoleGames)
                {
                    index++;
                    progress?.Report(new RebuildProgressReport
                    {
                        Current = index,
                        Total = consoleGames.Count,
                        CurrentMessage = $"Rebuilding: {game.Description}"
                    });

                    var matchedFiles = new Dictionary<RomDatInfo, string>();
                    foreach (var reqRom in game.RequiredRoms)
                    {
                        var key = (reqRom.Size, reqRom.Crc);
                        if (availableRoms.TryGetValue(key, out var filePath))
                        {
                            matchedFiles[reqRom] = filePath;
                            if (extractedPathToSourceRomMap.TryGetValue(filePath, out var sourcePath))
                            {
                                matchedSourcePaths.Add(sourcePath);
                            }
                        }
                        else if (reqRom.Name.EndsWith(".cue", StringComparison.OrdinalIgnoreCase) && availableFilesByName.TryGetValue(reqRom.Name, out var namePath))
                        {
                            matchedFiles[reqRom] = namePath;
                            if (extractedPathToSourceRomMap.TryGetValue(namePath, out var sourcePath))
                            {
                                matchedSourcePaths.Add(sourcePath);
                            }
                        }
                    }

                    if (matchedFiles.Count > 0)
                    {
                        try
                        {
                            string targetName = SanitizeFileName(game.Description);
                            string destinationFolder = targetOutputDir;
                            if (sortIntoRegionFolders)
                            {
                                string regionTag = SanitizeFileName(ExtractRegion(game.Description, regionPriorities));
                                destinationFolder = Path.Combine(targetOutputDir, regionTag);
                                Directory.CreateDirectory(destinationFolder);
                            }

                            if (format == OutputFormat.Zip)
                            {
                                string destZipPath = Path.Combine(destinationFolder, $"{targetName}.zip");
                                using (var archive = ZipFile.Open(destZipPath, ZipArchiveMode.Create))
                                {
                                    foreach (var pair in matchedFiles)
                                    {
                                        archive.CreateEntryFromFile(pair.Value, pair.Key.Name);
                                    }
                                }
                            }
                            else if (format == OutputFormat.SevenZ)
                            {
                                string dest7zPath = Path.Combine(destinationFolder, $"{targetName}.7z");
                                if (File.Exists(dest7zPath)) File.Delete(dest7zPath);

                                string gameFolder = Path.Combine(destinationFolder, targetName + "_temp");
                                Directory.CreateDirectory(gameFolder);
                                try
                                {
                                    foreach (var pair in matchedFiles)
                                    {
                                        string destFilePath = Path.Combine(gameFolder, pair.Key.Name);
                                        string? parentDir = Path.GetDirectoryName(destFilePath);
                                        if (!string.IsNullOrEmpty(parentDir)) Directory.CreateDirectory(parentDir);
                                        File.Copy(pair.Value, destFilePath, true);
                                    }

                                    bool compressed = false;
                                    string[] exeNames = { "7z", "7za", @"C:\Program Files\7-Zip\7z.exe", @"C:\Program Files (x86)\7-Zip\7z.exe" };
                                    foreach (var exe in exeNames)
                                    {
                                        try
                                        {
                                            var psi = new ProcessStartInfo
                                            {
                                                FileName = exe,
                                                Arguments = $"a -t7z \"{dest7zPath}\" * -y",
                                                WorkingDirectory = gameFolder,
                                                RedirectStandardOutput = true,
                                                RedirectStandardError = true,
                                                UseShellExecute = false,
                                                CreateNoWindow = true
                                            };
                                            using var proc = Process.Start(psi);
                                            if (proc != null)
                                            {
                                                proc.WaitForExit();
                                                if (proc.ExitCode == 0 && File.Exists(dest7zPath) && new FileInfo(dest7zPath).Length > 0)
                                                {
                                                    compressed = true;
                                                    break;
                                                }
                                            }
                                        }
                                        catch { }
                                    }

                                    if (!compressed)
                                    {
                                        if (File.Exists(dest7zPath)) File.Delete(dest7zPath);
                                        string finalFolder = Path.Combine(destinationFolder, targetName);
                                        if (Directory.Exists(finalFolder)) Directory.Delete(finalFolder, true);
                                        Directory.Move(gameFolder, finalFolder);
                                    }
                                }
                                finally
                                {
                                    if (Directory.Exists(gameFolder))
                                    {
                                        try { Directory.Delete(gameFolder, true); } catch { }
                                    }
                                }
                            }
                            else
                            {
                                string gameFolder = Path.Combine(destinationFolder, targetName);
                                Directory.CreateDirectory(gameFolder);
                                foreach (var pair in matchedFiles)
                                {
                                    File.Copy(pair.Value, Path.Combine(gameFolder, pair.Key.Name), true);
                                }
                            }

                            result.Moved++;
                            result.Processed += matchedFiles.Count;
                        }
                        catch
                        {
                            result.Failed++;
                        }
                    }

                    await Task.Delay(2);
                }

                foreach (var scannedRom in scannedRoms)
                {
                    if (!matchedSourcePaths.Contains(scannedRom.FilePath))
                    {
                        try
                        {
                            Directory.CreateDirectory(unknownOutputDir);
                            string destUnknownPath = Path.Combine(unknownOutputDir, Path.GetFileName(scannedRom.FilePath));
                            if (!File.Exists(destUnknownPath))
                            {
                                File.Copy(scannedRom.FilePath, destUnknownPath, true);
                                result.UnknownFilesHandled++;
                            }
                        }
                        catch { }
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    try { Directory.Delete(tempRoot, true); } catch { }
                }
            }

            return result;
        }

        private string ExtractRegion(string description, string regionPriorities)
        {
            var priorities = regionPriorities.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(r => r.Trim())
                                             .ToList();

            foreach (var priority in priorities)
            {
                if (description.Contains(priority, StringComparison.OrdinalIgnoreCase))
                {
                    return priority;
                }
            }

            var match = System.Text.RegularExpressions.Regex.Match(description, @"\(([^)]+)\)");
            if (match.Success)
            {
                return match.Groups[1].Value.Split(',')[0].Trim();
            }

            return "Other";
        }

        private string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
    }
}
