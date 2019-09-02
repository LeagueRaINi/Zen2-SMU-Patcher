using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using SMUPNET.Utils;

namespace SMUPNET
{
    public class Program
    {
        private static void Main(string[] args)
        {
            Console.Title = "SMU Patcher - by RaINi";

            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

#if DEBUG
            args = new[] { "C:\\Users\\RaINi\\Desktop\\ROG-CROSSHAIR-VIII-HERO-WIFI-ASUS-0702.CAP"/*, "-e"*/ };
#endif

            var log = new Log(true);
            
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                log.Exception(e.ExceptionObject as Exception, true);
            };

            var settings = Settings.Load(Path.Combine(Directory.GetCurrentDirectory(), "settings.json"));

            if (!args.Any() || (args.Length == 1 && (args.Contains("--extract") || args.Contains("-e")))) {
                log.Info("Usage: dotnet SMUPNET.dll filepath [--extract | -e]", true);
            }

            if (!File.Exists(args[0])) {
                log.Error($"Could not find bios file '{args[0]}'", true);
            }

            var biosFileBytes = File.ReadAllBytes(args[0]);
            var biosFileSize = biosFileBytes.Length;
            var biosName = Path.GetFileName(args[0]);

            log.Info($"Bios {biosName} loaded, size: {biosFileSize.ToString("N0")} bytes ({(biosFileSize / 1024).ToString("N0")} KB)");

            var modOffsets = BoyerMoore.Search(biosFileBytes, settings.ModulePattern)
                .OrderBy(x => x)
                .ToList();
            if (!modOffsets.Any()) {
                log.Error("Could not find any smu modules with the given pattern", true);
            }

            log.Info($"Found {modOffsets.Count} smu modules");

            if (args.Contains("--extract") || args.Contains("-e")) {
                var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Extracted");

                Directory.CreateDirectory(outputDir);

                using (var zipFile = ZipFile.Open(Path.Combine(outputDir, $"{biosName}.SMU.zip"), ZipArchiveMode.Update)) {
                    for (var i = 0; i < modOffsets.Count; i++) {
                        var modOffset = modOffsets[i];

                        var versionStart = modOffset + settings.ModuleVersionOffset;
                        var versionStr = $"{biosFileBytes[versionStart + 0x2]}.{biosFileBytes[versionStart + 0x1]}.{biosFileBytes[versionStart + 0x0]}";

                        var sizeBytes = BitConverter.ToInt32(biosFileBytes, modOffset + settings.ModuleSizeOffset);
                        var sizeStr = $"{sizeBytes.ToString("N0").PadRight(7, ' ')} bytes ({(sizeBytes / 1024f).ToString("N0").PadRight(3, ' ')} KB)";

                        var modStart = modOffset + settings.ModuleStartOffset;
                        var modBytes = new byte[sizeBytes];

                        Array.Copy(biosFileBytes, modStart, modBytes, 0, modBytes.Length);

                        var zipEntry = zipFile.CreateEntry($"{versionStr} {modOffset.ToString("X")}.SMU{i}");

                        using (var bw = new BinaryWriter(zipEntry.Open())) {
                            bw.Write(modBytes);
                        }

                        log.Success($"Dumped module {i} [{modStart.ToString("X")}-{(modStart + sizeBytes).ToString("X")}] with a size of {sizeStr}");
                    }

                    log.Info("Finished dumping smu modules");
                }
            }
            else {
                var patchesDir = Path.Combine(Directory.GetCurrentDirectory(), "Patches");

                Directory.CreateDirectory(patchesDir);

                var patches = new Dictionary<int, List<byte>>();
                foreach(var patchFile in Directory.GetFiles(patchesDir, "*.SMU*")) {
                    var fileName = Path.GetFileName(patchFile);
                    var fileExt = Path.GetExtension(fileName);

                    var regexMatch = Regex.Match(fileExt, "(\\d+)$");
                    if (regexMatch.Success)  {
                        patches.Add(int.Parse(regexMatch.Value), File.ReadAllBytes(patchFile).ToList());
                        continue;
                    }

                    log.Warning($"Could not parse patch {fileName}");
                }

                if (!patches.Any()) {
                    log.Error("Could not find any patches make sure they have the correct format (*.SMU1, *SMU2, ...)", true);
                }

                var patchSettings = settings.Patching.FirstOrDefault(x => x.Key == modOffsets.Count);
                if (patchSettings.Equals(default(KeyValuePair<int, Patching>))) {
                    log.Error($"Could not find patch settings for smu module count {modOffsets.Count}", true);
                }

                if (patchSettings.Value.Skip != null) {
                    var tmpList = new List<int>();
                    for (var i = 0; i < modOffsets.Count; i++) {
                        var modOffset = modOffsets[i];
                        if (patchSettings.Value.Skip.Contains(i)) {
                            log.Info($"Skipping smu module {i}");
                            continue;
                        }

                        tmpList.Add(modOffset);
                    }

                    modOffsets = tmpList;
                }

                for (var i = 0; i < modOffsets.Count; i++) {
                    var modOffset = modOffsets[i];
                    var modStart = modOffset + settings.ModuleStartOffset;
                    var patchOrder = patchSettings.Value.Order[i];

                    if (!patches.TryGetValue(patchOrder, out var patchBytes)) {
                        log.Error($"Could not find patch {patchOrder} in patch list", true);
                    }

                    var moduleSize = BitConverter.ToInt32(biosFileBytes, modOffset + settings.ModuleSizeOffset);
                    if (moduleSize < patchBytes.Count) {
                        log.Error($"Patch {patchOrder} is bigger than smu module {i} at {modStart} ({moduleSize.ToString("X")} -> {patchBytes.Count.ToString("X")})", true);
                        continue;
                    }

                    if (moduleSize > patchBytes.Count) {
                        while (patchBytes.Count != moduleSize) {
                            patchBytes.Add(0xFF);
                        }
                    }

                    var versionStart = modOffset + settings.ModuleVersionOffset;
                    var oldVersionStr = $"{biosFileBytes[versionStart + 0x2]}.{biosFileBytes[versionStart + 0x1]}.{biosFileBytes[versionStart + 0x0]}";

                    for (var j = 0; j < patchBytes.Count; j++) {
                        biosFileBytes[modStart + j] = patchBytes[j];
                    }

                    var newVersionStr = $"{biosFileBytes[versionStart + 0x2]}.{biosFileBytes[versionStart + 0x1]}.{biosFileBytes[versionStart + 0x0]}";

                    log.Success($"Patched smu module [{modStart.ToString("X")}-{(modStart + moduleSize).ToString("X")}] with patch {patchOrder} ({oldVersionStr} -> {newVersionStr})");
                }

                if (biosFileSize != biosFileBytes.Length) {
                    log.Warning($"Modified bios size doesnt match original size! ({biosFileSize.ToString("N0")}-{biosFileBytes.Length.ToString("N0")})");
                }

                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(args[0]), $"{biosName}.SMU.MOD"), biosFileBytes);

                log.Info("Finished patching smu modules");
            }
        }
    }
}
