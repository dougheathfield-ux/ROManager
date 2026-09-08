using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using RomRebuilderUI.Models;

namespace RomRebuilderUI.Services
{
    public class RebuildResult
    {
        public int Processed { get; set; }
        public int Moved { get; set; }
        public int Failed { get; set; }
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

        // --- MAME Audit Overloads ---
        public async Task<AuditSummary> RunMameAudit(IEnumerable<string> sourceDirs, string datPath, RebuildMode mode, IProgress<string>? progress = null, Action<MachineAuditItem>? onItemAudited = null)
        {
            return await AuditMameSetsAsync(sourceDirs, datPath, progress, onItemAudited);
        }

        public async Task<AuditSummary> AuditMameSetsAsync(IEnumerable<string> sourceDirs, string datPath, IProgress<string>? progress, Action<MachineAuditItem>? onItemAudited = null)
        {
            return await AuditConsoleSetsAsync(sourceDirs, datPath, false, "", datPath, progress, onItemAudited);
        }

        // --- MAME Rebuild Overloads ---
        public async Task<RebuildResult> RunMameRebuild(IEnumerable<string> sourceDirs, string outputDir, string datPath, RebuildMode mode, OutputFormat format, IProgress<string>? progress = null, Action<RebuildResult>? onProgressUpdate = null)
        {
            return await RebuildMameSetsAsync(sourceDirs, outputDir, datPath, mode, format, progress, onProgressUpdate);
        }

        // --- Console Audit Overloads ---
        public async Task<AuditSummary> RunConsoleAudit(IEnumerable<string> sourceDirs, string datPath, bool enable1G1R, string regionPriorities, IProgress<string>? progress = null, Action<MachineAuditItem>? onItemAudited = null)
        {
            return await AuditConsoleSetsAsync(sourceDirs, datPath, enable1G1R, regionPriorities, datPath, progress, onItemAudited);
        }

        // --- Console Rebuild Overloads ---
        public async Task<RebuildResult> RunConsoleRebuild(IEnumerable<string> sourceDirs, string outputDir, string datPath, bool enable1G1R, string regionPriorities, OutputFormat format, IProgress<string>? progress = null, Action<RebuildResult>? onProgressUpdate = null)
        {
            return await RebuildConsoleSetsAsync(sourceDirs, outputDir, datPath, enable1G1R, regionPriorities, format, datPath, progress, onProgressUpdate);
        }

        // --- DAT Parser Helpers ---
        private class RomDatInfo
        {
            public string Description { get; set; } = string.Empty;
            public long Size { get; set; }
            public string Crc { get; set; } = string.Empty;
        }

        private class MameRomEntry
        {
            public string Name { get; set; } = string.Empty;
            public long Size { get; set; }
            public string Crc { get; set; } = string.Empty;
        }

        private class MameMachineInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string CloneOf { get; set; } = string.Empty;
            public List<MameRomEntry> Roms { get; set; } = new();
        }

        private Dictionary<string, MameMachineInfo> LoadMameDatMachines(string datPath)
        {
            var machines = new Dictionary<string, MameMachineInfo>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(datPath) || !File.Exists(datPath)) return machines;

            try
            {
                var doc = XDocument.Load(datPath);
                var elements = doc.Descendants("machine").Concat(doc.Descendants("game"));

                foreach (var el in elements)
                {
                    string? name = el.Attribute("name")?.Value;
                    if (string.IsNullOrEmpty(name)) continue;

                    var machine = new MameMachineInfo
                    {
                        Name = name,
                        Description = el.Attribute("description")?.Value ?? name,
                        CloneOf = el.Attribute("cloneof")?.Value ?? string.Empty
                    };

                    foreach (var romEl in el.Descendants("rom"))
                    {
                        string? romName = romEl.Attribute("name")?.Value;
                        string? sizeStr = romEl.Attribute("size")?.Value;
                        string? crcStr = romEl.Attribute("crc")?.Value;

                        if (!string.IsNullOrEmpty(romName) && long.TryParse(sizeStr, out long size) && !string.IsNullOrEmpty(crcStr))
                        {
                            machine.Roms.Add(new MameRomEntry
                            {
                                Name = romName,
                                Size = size,
                                Crc = crcStr.ToUpperInvariant()
                            });
                        }
                    }
                    machines[name] = machine;
                }
            }
            catch { }
            return machines;
        }

        private (Dictionary<string, string> nameMappings, Dictionary<(long size, string crc), RomDatInfo> romMappings) LoadDatMappings(string datPath)
        {
            var nameMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var romMappings = new Dictionary<(long size, string crc), RomDatInfo>();

            if (string.IsNullOrEmpty(datPath) || !File.Exists(datPath)) 
                return (nameMappings, romMappings);

            try
            {
                var doc = XDocument.Load(datPath);
                var elements = doc.Descendants("machine").Concat(doc.Descendants("game"));
                
                foreach (var el in elements)
                {
                    string? name = el.Attribute("name")?.Value;
                    string? description = el.Attribute("description")?.Value ?? name;

                    if (!string.IsNullOrEmpty(name))
                    {
                        string safeName = name;
                        string safeDesc = description ?? safeName;

                        nameMappings[safeName] = safeDesc;
                        nameMappings[safeDesc] = safeDesc;

                        foreach (var romEl in el.Descendants("rom"))
                        {
                            string? romName = romEl.Attribute("name")?.Value;
                            string? sizeStr = romEl.Attribute("size")?.Value;
                            string? crcStr = romEl.Attribute("crc")?.Value;

                            if (!string.IsNullOrEmpty(romName))
                            {
                                nameMappings[Path.GetFileNameWithoutExtension(romName)] = safeDesc;
                            }

                            if (!string.IsNullOrEmpty(sizeStr) && long.TryParse(sizeStr, out long romSize) && !string.IsNullOrEmpty(crcStr))
                            {
                                var info = new RomDatInfo { Description = safeDesc, Size = romSize, Crc = crcStr.ToUpperInvariant() };
                                romMappings[(romSize, crcStr.ToUpperInvariant())] = info;
                                nameMappings[romName ?? ""] = safeDesc;
                            }
                        }
                    }
                }
            }
            catch { }
            return (nameMappings, romMappings);
        }

        // --- Core Audit Implementation with Live Incremental Callback ---
        public async Task<AuditSummary> AuditConsoleSetsAsync(IEnumerable<string> sourceDirs, string datPath, bool enable1G1R, string regionPriorities, string actualDatPath, IProgress<string>? progress, Action<MachineAuditItem>? onItemAudited = null)
        {
            var summary = new AuditSummary();
            var validDirs = sourceDirs?.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).ToList() ?? new List<string>();
            if (validDirs.Count == 0)
            {
                progress?.Report("Error: No valid Source Directories specified.");
                return summary;
            }

            progress?.Report($"Loading DAT file: {actualDatPath}...");
            var (nameMappings, romMappings) = LoadDatMappings(actualDatPath);
            progress?.Report($"Loaded {nameMappings.Count} name mappings and {romMappings.Count} CRC signatures from DAT.");

            var scannedRoms = new List<ScannedRomFile>();
            foreach (var dir in validDirs)
            {
                var filesInDir = await Task.Run(() => RomScanner.ScanDirectory(dir));
                scannedRoms.AddRange(filesInDir);
            }

            int validCount = 0;
            int badCount = 0;
            int unknownCount = 0;

            string tempRoot = Path.Combine(Path.GetTempPath(), "RomRebuilderTemp_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRoot);

            try
            {
                int index = 0;
                foreach (var rom in scannedRoms)
                {
                    index++;
                    string fileToAnalyze = rom.FilePath;
                    long fileSize = rom.Size;

                    if (rom.IsArchive || Path.GetExtension(rom.FilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        string extractSubDir = Path.Combine(tempRoot, Path.GetFileNameWithoutExtension(rom.FilePath));
                        Directory.CreateDirectory(extractSubDir);
                        try
                        {
                            ZipFile.ExtractToDirectory(rom.FilePath, extractSubDir, true);
                            var extractedFiles = Directory.GetFiles(extractSubDir, "*.*", SearchOption.AllDirectories);
                            if (extractedFiles.Length > 0)
                            {
                                fileToAnalyze = extractedFiles[0];
                                fileSize = new FileInfo(fileToAnalyze).Length;
                            }
                        }
                        catch { }
                    }

                    string fileCrc = await Task.Run(() => CalculateCrc32(fileToAnalyze));
                    string status = "Unknown";
                    string description = "Found in source (Not in DAT)";

                    var crcKey = (fileSize, fileCrc.ToUpperInvariant());
                    bool matchesCrc = romMappings.ContainsKey(crcKey);
                    bool matchesName = nameMappings.ContainsKey(Path.GetFileNameWithoutExtension(fileToAnalyze));

                    if (matchesCrc)
                    {
                        status = "Matched";
                        description = $"Valid ROM: {romMappings[crcKey].Description}";
                        validCount++;
                    }
                    else if (matchesName)
                    {
                        status = "Broken (Bad Dump)";
                        description = "File matches name/size in DAT but CRC checksum is invalid!";
                        badCount++;
                    }
                    else
                    {
                        status = "Unknown";
                        description = "File not found in DAT definitions.";
                        unknownCount++;
                    }

                    string targetName = matchesCrc ? SanitizeFileName(romMappings[crcKey].Description) : CleanRomName(Path.GetFileNameWithoutExtension(rom.FilePath));

                    var auditItem = new MachineAuditItem
                    {
                        Name = targetName,
                        Description = description,
                        RomCountSummary = $"{fileSize / 1024} KB (CRC: {fileCrc})",
                        Status = status
                    };

                    summary.Items.Add(auditItem);
                    onItemAudited?.Invoke(auditItem);

                    if (index % 50 == 0 || index == scannedRoms.Count)
                    {
                        progress?.Report($"Audited {index}/{scannedRoms.Count} files...");
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

            summary.TotalFiles = scannedRoms.Count;
            summary.ValidFiles = validCount;
            progress?.Report($"Audit complete. Valid: {validCount}, Bad Dumps: {badCount}, Unknown: {unknownCount}");
            return summary;
        }

        // --- MAME Rebuild Implementation with Throttled Progress ---
        public async Task<RebuildResult> RebuildMameSetsAsync(IEnumerable<string> sourceDirs, string outputDir, string datPath, RebuildMode mode, OutputFormat format, IProgress<string>? progress, Action<RebuildResult>? onProgressUpdate = null)
        {
            var result = new RebuildResult();
            if (string.IsNullOrEmpty(outputDir))
            {
                progress?.Report("Error: Output directory is not specified.");
                return result;
            }

            var validDirs = sourceDirs?.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).ToList() ?? new List<string>();
            if (validDirs.Count == 0)
            {
                progress?.Report("Error: No valid Source Directories specified.");
                return result;
            }

            string mameVersion = "mame";
            try
            {
                var docHeaderCheck = XDocument.Load(datPath);
                var versionEl = docHeaderCheck.Root?.Element("header")?.Element("version");
                if (versionEl != null && !string.IsNullOrEmpty(versionEl.Value))
                {
                    mameVersion = versionEl.Value.Trim();
                }
                else
                {
                    mameVersion = Path.GetFileNameWithoutExtension(datPath);
                }
            }
            catch
            {
                mameVersion = Path.GetFileNameWithoutExtension(datPath);
            }

            string targetOutputDir = Path.Combine(outputDir, "mame", SanitizeFileName(mameVersion));
            Directory.CreateDirectory(targetOutputDir);

            progress?.Report($"Loading MAME DAT file: {datPath} with Rebuild Mode [{mode}]...");

            var machines = LoadMameDatMachines(datPath);
            if (machines.Count == 0)
            {
                progress?.Report("Error: No machines found in DAT file or invalid DAT.");
                return result;
            }

            var scannedRoms = new List<ScannedRomFile>();
            foreach (var dir in validDirs)
            {
                progress?.Report($"Scanning source ROM directory: {dir}...");
                var filesInDir = await Task.Run(() => RomScanner.ScanDirectory(dir));
                scannedRoms.AddRange(filesInDir);
            }

            string tempRoot = Path.Combine(Path.GetTempPath(), "RomRebuilderMameTemp_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRoot);

            var availableRoms = new Dictionary<(long size, string crc), string>();

            try
            {
                foreach (var rom in scannedRoms)
                {
                    string fileToAnalyze = rom.FilePath;
                    long fileSize = rom.Size;

                    if (rom.IsArchive || Path.GetExtension(rom.FilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        string extractSubDir = Path.Combine(tempRoot, Path.GetFileNameWithoutExtension(rom.FilePath) + "_" + Guid.NewGuid().ToString());
                        Directory.CreateDirectory(extractSubDir);
                        try
                        {
                            ZipFile.ExtractToDirectory(rom.FilePath, extractSubDir, true);
                            foreach (var extractedFile in Directory.GetFiles(extractSubDir, "*.*", SearchOption.AllDirectories))
                            {
                                string crc = CalculateCrc32(extractedFile);
                                long size = new FileInfo(extractedFile).Length;
                                var key = (size, crc.ToUpperInvariant());
                                if (!availableRoms.ContainsKey(key))
                                {
                                    availableRoms[key] = extractedFile;
                                }
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        string crc = CalculateCrc32(fileToAnalyze);
                        var key = (fileSize, crc.ToUpperInvariant());
                        if (!availableRoms.ContainsKey(key))
                        {
                            availableRoms[key] = fileToAnalyze;
                        }
                    }
                }

                progress?.Report($"Indexed {availableRoms.Count} unique source ROM files.");

                var parentToClones = machines.Values
                    .Where(m => !string.IsNullOrEmpty(m.CloneOf))
                    .GroupBy(m => m.CloneOf, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                int machineIndex = 0;
                foreach (var machine in machines.Values)
                {
                    machineIndex++;
                    bool isClone = !string.IsNullOrEmpty(machine.CloneOf);

                    if (mode == RebuildMode.Merged && isClone)
                    {
                        continue;
                    }

                    var setRoms = new List<MameRomEntry>();
                    var romSourcePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    if (mode == RebuildMode.Merged && !isClone)
                    {
                        var clusterMachines = new List<MameMachineInfo> { machine };
                        if (parentToClones.TryGetValue(machine.Name, out var clones))
                        {
                            clusterMachines.AddRange(clones);
                        }

                        foreach (var cm in clusterMachines)
                        {
                            foreach (var r in cm.Roms)
                            {
                                var key = (r.Size, r.Crc);
                                if (availableRoms.TryGetValue(key, out var srcPath))
                                {
                                    setRoms.Add(r);
                                    romSourcePaths[r.Name] = srcPath;
                                }
                            }
                        }
                    }
                    else if (mode == RebuildMode.Split && isClone)
                    {
                        if (machines.TryGetValue(machine.CloneOf, out var parentMachine))
                        {
                            var parentCrcs = new HashSet<string>(parentMachine.Roms.Select(r => r.Crc), StringComparer.OrdinalIgnoreCase);
                            foreach (var r in machine.Roms)
                            {
                                if (!parentCrcs.Contains(r.Crc))
                                {
                                    var key = (r.Size, r.Crc);
                                    if (availableRoms.TryGetValue(key, out var srcPath))
                                    {
                                        setRoms.Add(r);
                                        romSourcePaths[r.Name] = srcPath;
                                    }
                                }
                            }
                        }
                        else
                        {
                            foreach (var r in machine.Roms)
                            {
                                var key = (r.Size, r.Crc);
                                if (availableRoms.TryGetValue(key, out var srcPath))
                                {
                                    setRoms.Add(r);
                                    romSourcePaths[r.Name] = srcPath;
                                }
                            }
                        }
                    }
                    else if (mode == RebuildMode.NonMerged && isClone)
                    {
                        var combinedRoms = new Dictionary<string, MameRomEntry>(StringComparer.OrdinalIgnoreCase);
                        if (machines.TryGetValue(machine.CloneOf, out var parentMachine))
                        {
                            foreach (var r in parentMachine.Roms) combinedRoms[r.Name] = r;
                        }
                        foreach (var r in machine.Roms) combinedRoms[r.Name] = r;

                        foreach (var r in combinedRoms.Values)
                        {
                            var key = (r.Size, r.Crc);
                            if (availableRoms.TryGetValue(key, out var srcPath))
                            {
                                setRoms.Add(r);
                                romSourcePaths[r.Name] = srcPath;
                            }
                        }
                    }
                    else
                    {
                        foreach (var r in machine.Roms)
                        {
                            var key = (r.Size, r.Crc);
                            if (availableRoms.TryGetValue(key, out var srcPath))
                            {
                                setRoms.Add(r);
                                romSourcePaths[r.Name] = srcPath;
                            }
                        }
                    }

                    result.Processed++;

                    if (setRoms.Count > 0)
                    {
                        try
                        {
                            string destZipPath = Path.Combine(targetOutputDir, $"{machine.Name}.zip");
                            using (var archive = ZipFile.Open(destZipPath, ZipArchiveMode.Create))
                            {
                                foreach (var r in setRoms)
                                {
                                    if (romSourcePaths.TryGetValue(r.Name, out var srcPath) && File.Exists(srcPath))
                                    {
                                        archive.CreateEntryFromFile(srcPath, r.Name);
                                    }
                                }
                            }
                            result.Moved++;
                        }
                        catch (Exception ex)
                        {
                            result.Failed++;
                            progress?.Report($"[Error] Failed to build set {machine.Name}: {ex.Message}");
                        }
                    }

                    if (machineIndex % 50 == 0 || machineIndex == machines.Count)
                    {
                        progress?.Report($"Rebuilt {result.Processed}/{machines.Count} MAME sets...");
                    }

                    onProgressUpdate?.Invoke(result);
                    await Task.Yield();
                }

                progress?.Report($"MAME rebuild finished! Created {result.Moved} sets in 'mame/{mameVersion}/' folder.");
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

        // --- Core Console Rebuild Implementation with Throttled Progress ---
        public async Task<RebuildResult> RebuildConsoleSetsAsync(IEnumerable<string> sourceDirs, string outputDir, string datPath, bool enable1G1R, string regionPriorities, OutputFormat format, string actualDatPath, IProgress<string>? progress, Action<RebuildResult>? onProgressUpdate = null)
        {
            var result = new RebuildResult();
            if (string.IsNullOrEmpty(outputDir))
            {
                progress?.Report("Error: Output directory is not specified.");
                return result;
            }

            var validDirs = sourceDirs?.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).ToList() ?? new List<string>();
            if (validDirs.Count == 0)
            {
                progress?.Report("Error: No valid Source Directories specified.");
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
            Directory.CreateDirectory(targetOutputDir);

            string badDir = Path.Combine(targetOutputDir, "_BadDumps");
            string unknownDir = Path.Combine(targetOutputDir, "_Unknown");

            progress?.Report($"Loading DAT file for rebuild: {actualDatPath}...");
            var (nameMappings, romMappings) = LoadDatMappings(actualDatPath);
            
            var scannedRoms = new List<ScannedRomFile>();
            foreach (var dir in validDirs)
            {
                var filesInDir = await Task.Run(() => RomScanner.ScanDirectory(dir));
                scannedRoms.AddRange(filesInDir);
            }

            string tempRoot = Path.Combine(Path.GetTempPath(), "RomRebuilderRebuild_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRoot);

            try
            {
                foreach (var rom in scannedRoms)
                {
                    result.Processed++;
                    try
                    {
                        string fileToAnalyze = rom.FilePath;
                        long fileSize = rom.Size;

                        if (rom.IsArchive || Path.GetExtension(rom.FilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            string extractSubDir = Path.Combine(tempRoot, Path.GetFileNameWithoutExtension(rom.FilePath) + "_" + Guid.NewGuid().ToString());
                            Directory.CreateDirectory(extractSubDir);
                            try
                            {
                                ZipFile.ExtractToDirectory(rom.FilePath, extractSubDir, true);
                                var extractedFiles = Directory.GetFiles(extractSubDir, "*.*", SearchOption.AllDirectories);
                                if (extractedFiles.Length > 0)
                                {
                                    fileToAnalyze = extractedFiles[0];
                                    fileSize = new FileInfo(fileToAnalyze).Length;
                                }
                            }
                            catch { }
                        }

                        string fileCrc = CalculateCrc32(fileToAnalyze);
                        var crcKey = (fileSize, fileCrc.ToUpperInvariant());

                        if (romMappings.TryGetValue(crcKey, out var datInfo))
                        {
                            string targetName = SanitizeFileName(datInfo.Description);
                            string destZipPath = Path.Combine(targetOutputDir, $"{targetName}.zip");
                            string internalFileName = $"{targetName}{Path.GetExtension(fileToAnalyze)}";

                            using (var archive = ZipFile.Open(destZipPath, ZipArchiveMode.Create))
                            {
                                archive.CreateEntryFromFile(fileToAnalyze, internalFileName);
                            }
                            result.Moved++;
                        }
                        else if (nameMappings.ContainsKey(Path.GetFileNameWithoutExtension(fileToAnalyze)))
                        {
                            Directory.CreateDirectory(badDir);
                            string destBad = Path.Combine(badDir, Path.GetFileName(rom.FilePath));
                            File.Copy(rom.FilePath, destBad, true);
                            result.Moved++;
                        }
                        else
                        {
                            Directory.CreateDirectory(unknownDir);
                            string destUnknown = Path.Combine(unknownDir, Path.GetFileName(rom.FilePath));
                            File.Copy(rom.FilePath, destUnknown, true);
                            result.Moved++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Failed++;
                        progress?.Report($"[Error] Failed to process file: {ex.Message}");
                    }

                    if (result.Processed % 50 == 0 || result.Processed == scannedRoms.Count)
                    {
                        progress?.Report($"Processed {result.Processed}/{scannedRoms.Count} console files...");
                    }

                    onProgressUpdate?.Invoke(result);
                    await Task.Yield();
                }
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    try { Directory.Delete(tempRoot, true); } catch { }
                }
            }

            progress?.Report($"Rebuild finished! Processed {result.Processed} files into '{vendor}/{system}'.");
            return result;
        }

        private string CleanRomName(string name) => string.IsNullOrWhiteSpace(name) ? "unknown" : name.Trim();

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
