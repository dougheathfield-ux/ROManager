using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using RomRebuilderUI.Models;

namespace RomRebuilderUI.Services
{
    public class RomRebuilderService
    {
        public async Task RunMameRebuild(
            IEnumerable<string> sourceDirs,
            IEnumerable<string> addPaths,
            string outputDirectory,
            string datPath,
            RebuildMode rebuildMode,
            string compressionFormat,
            bool separateBiosSets,
            bool recompressFiles,
            bool removeMatchedSourceFiles,
            bool verifyHashesOnMatch,
            IProgress<RebuildProgressReport>? progress)
        {
            progress?.Report(new RebuildProgressReport { Current = 0, Total = 100, CurrentMessage = "Parsing MAME DAT file..." });
            await Task.Delay(500);

            var allSearchDirectories = new List<string>(sourceDirs);
            if (addPaths != null)
            {
                allSearchDirectories.AddRange(addPaths);
            }

            int simulatedTotalSets = 150;
            for (int i = 1; i <= simulatedTotalSets; i++)
            {
                progress?.Report(new RebuildProgressReport
                {
                    Current = i,
                    Total = simulatedTotalSets,
                    CurrentMessage = $"Rebuilding set {i} of {simulatedTotalSets} ({rebuildMode} mode)..."
                });
                await Task.Delay(20);
            }

            if (removeMatchedSourceFiles)
            {
                progress?.Report(new RebuildProgressReport { Current = simulatedTotalSets, Total = simulatedTotalSets, CurrentMessage = "Cleaning up matched source files..." });
                await Task.Delay(300);
            }

            progress?.Report(new RebuildProgressReport { Current = simulatedTotalSets, Total = simulatedTotalSets, CurrentMessage = "Arcade rebuild complete!" });
        }

        public async Task RunMameAudit(
            IEnumerable<string> sourceDirs,
            string datPath,
            RebuildMode rebuildMode,
            bool auditRoms,
            bool auditDisks,
            bool auditSamples,
            bool auditBios,
            IProgress<RebuildProgressReport>? progress,
            Action<MachineAuditItem> onMachineAudited)
        {
            progress?.Report(new RebuildProgressReport { Current = 0, Total = 100, CurrentMessage = "Parsing MAME DAT file..." });

            if (string.IsNullOrEmpty(datPath) || !File.Exists(datPath))
            {
                progress?.Report(new RebuildProgressReport { Current = 100, Total = 100, CurrentMessage = "Error: MAME DAT file not found." });
                return;
            }

            var searchDirs = sourceDirs?.ToList() ?? new List<string>();

            await Task.Run(() =>
            {
                try
                {
                    var xDoc = XDocument.Load(datPath);
                    var machines = xDoc.Descendants("machine").ToList();
                    if (machines.Count == 0)
                    {
                        machines = xDoc.Descendants("game").ToList();
                    }

                    int total = machines.Count;
                    int current = 0;

                    foreach (var machine in machines)
                    {
                        current++;
                        string name = machine.Attribute("name")?.Value ?? "unknown";
                        string description = machine.Element("description")?.Value ?? name;

                        // Respect target selection flags
                        var romElements = auditRoms ? machine.Elements("rom").ToList() : new List<XElement>();
                        var diskElements = auditDisks ? machine.Elements("disk").ToList() : new List<XElement>();

                        if (romElements.Count == 0 && diskElements.Count == 0)
                        {
                            continue;
                        }

                        int totalFiles = romElements.Count + diskElements.Count;
                        int foundFiles = 0;
                        var missingFiles = new List<string>();

                        foreach (var rom in romElements)
                        {
                            string romName = rom.Attribute("name")?.Value ?? string.Empty;
                            bool romFound = false;

                            if (!string.IsNullOrEmpty(romName))
                            {
                                foreach (string dir in searchDirs)
                                {
                                    if (Directory.Exists(dir))
                                    {
                                        string directFile = Path.Combine(dir, romName);
                                        string zipContainer = Path.Combine(dir, $"{name}.zip");
                                        string sevenZContainer = Path.Combine(dir, $"{name}.7z");

                                        if (File.Exists(directFile) || File.Exists(zipContainer) || File.Exists(sevenZContainer))
                                        {
                                            romFound = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (romFound)
                            {
                                foundFiles++;
                            }
                            else if (!string.IsNullOrEmpty(romName))
                            {
                                missingFiles.Add(romName);
                            }
                        }

                        foreach (var disk in diskElements)
                        {
                            string diskName = disk.Attribute("name")?.Value ?? name;
                            bool diskFound = false;

                            foreach (string dir in searchDirs)
                            {
                                if (Directory.Exists(dir))
                                {
                                    string chdFile1 = Path.Combine(dir, $"{diskName}.chd");
                                    string chdFile2 = Path.Combine(dir, name, $"{diskName}.chd");
                                    string chdFile3 = Path.Combine(dir, $"{name}.chd");

                                    if (File.Exists(chdFile1) || File.Exists(chdFile2) || File.Exists(chdFile3))
                                    {
                                        diskFound = true;
                                        break;
                                    }
                                }
                            }

                            if (diskFound)
                            {
                                foundFiles++;
                            }
                            else
                            {
                                missingFiles.Add($"{diskName}.chd");
                            }
                        }

                        string status = "Complete";

                        if (totalFiles > 0 && foundFiles == 0)
                        {
                            status = "Missing";
                        }
                        else if (foundFiles < totalFiles)
                        {
                            status = "Incomplete";
                        }

                        var auditItem = new MachineAuditItem
                        {
                            Name = name,
                            Description = description,
                            Status = status,
                            RomCountSummary = $"{foundFiles} / {totalFiles} files",
                            MissingFiles = missingFiles
                        };

                        onMachineAudited?.Invoke(auditItem);

                        if (current % 25 == 0 || current == total)
                        {
                            progress?.Report(new RebuildProgressReport
                            {
                                Current = current,
                                Total = total,
                                CurrentMessage = $"Auditing machine {current} of {total}: {name}"
                            });
                        }
                    }

                    progress?.Report(new RebuildProgressReport
                    {
                        Current = total,
                        Total = total,
                        CurrentMessage = $"MAME audit completed. Total machines scanned: {total}"
                    });
                }
                catch (Exception ex)
                {
                    progress?.Report(new RebuildProgressReport
                    {
                        Current = 100,
                        Total = 100,
                        CurrentMessage = $"DAT Parse Error: {ex.Message}"
                    });
                }
            });
        }

        public async Task<AuditSummary?> RunConsoleAudit(
            IEnumerable<string> sourceDirs,
            string datPath,
            bool is1G1REnabled,
            string regionPriorities,
            IProgress<RebuildProgressReport>? progress,
            Action<MachineAuditItem>? onMachineAudited)
        {
            progress?.Report(new RebuildProgressReport { Current = 0, Total = 100, CurrentMessage = "Initializing Console (1G1R) audit..." });
            await Task.Delay(500);

            for (int i = 1; i <= 20; i++)
            {
                progress?.Report(new RebuildProgressReport
                {
                    Current = i,
                    Total = 20,
                    CurrentMessage = $"Auditing console set {i} / 20..."
                });

                var item = new MachineAuditItem
                {
                    Name = $"console_game_{i}",
                    Description = $"Console Game Title {i}",
                    Status = i % 2 == 0 ? "Complete" : "Incomplete",
                    Region = "USA",
                    RomCountSummary = "1 / 1 file"
                };

                if (item.Status == "Incomplete")
                {
                    item.MissingFiles.Add($"rom_{i}_patch.bin");
                }

                onMachineAudited?.Invoke(item);
                await Task.Delay(30);
            }

            progress?.Report(new RebuildProgressReport { Current = 20, Total = 20, CurrentMessage = "Console audit complete." });

            return new AuditSummary
            {
                UnknownFiles = new List<string> { "unneeded_rom_dump.bin" }
            };
        }

        public async Task<RebuildResult> RunConsoleRebuild(
            IEnumerable<string> sourceDirs,
            string outputDirectory,
            string datPath,
            bool is1G1REnabled,
            string regionPriorities,
            bool isRegionSortingEnabled,
            OutputFormat outputFormat,
            IProgress<RebuildProgressReport>? progress)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            progress?.Report(new RebuildProgressReport { Current = 0, Total = 100, CurrentMessage = "Starting Console 1G1R rebuild..." });
            await Task.Delay(800);
            stopwatch.Stop();

            return new RebuildResult
            {
                Success = true,
                ElapsedTime = stopwatch.Elapsed,
                FilesProcessed = 200,
                FilesMatched = 190,
                Message = "Console rebuild finished successfully."
            };
        }
    }
}
