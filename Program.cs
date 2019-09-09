using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SMUPNET2
{
    public class Program
    {
        private static void Main(string[] args)
        {
            Console.Title = "SMU Patcher - by RaIŇİ᎐";

            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                Console.ReadKey();
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Log.Fatal(e.ExceptionObject as Exception, "Unhandled Exception!");
            };

            if (args.Length == 0)
            {
                Log.Error("No File detected");
                return;
            }

            if (!File.Exists(args[0]))
            {
                Log.Error("Could not find File {0}", args[0]);
                return;
            }

            var biosName = Path.GetFileName(args[0]);
            var biosBytes = File.ReadAllBytes(args[0]);
            var biosSize = biosBytes.Length;
            
            Log.Info($"BIOS {biosName} loaded, size {biosSize.ToString("N0")} bytes ({(biosSize / 1024).ToString("N0")} KB)");

            var settings = Settings.Default();
            var settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "settings.json");

            if (File.Exists(settingsPath))
            {
                settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(settingsPath));
            }
            else
            {
                File.WriteAllText(settingsPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
            }

            var smuModList = Utils.BoyerMooreHorspool.SearchPattern(biosBytes, settings.ModulePattern, settings.ModuleStartOffset)
                .OrderBy(x => x)
                .ToArray(); 
            if (smuModList.Length == 0)
            {
                Log.Error("Could not find any SMU Modules with Pattern {0}", settings.ModulePattern);
                return;
            }

            Log.Info("Found {0} SMU Modules", smuModList.Length);

            var patches = new Dictionary<int, List<byte>>();
            var patchesDir = Path.Combine(Directory.GetCurrentDirectory(), "Patches");

            Directory.CreateDirectory(patchesDir);

            foreach (var patchFile in Directory.GetFiles(patchesDir, "*.SMU*", SearchOption.TopDirectoryOnly))
            {
                var regexMatch = Regex.Match(Path.GetExtension(Path.GetFileName(patchFile)), "(\\d+)$");
                if (regexMatch.Success && int.TryParse(regexMatch.Value, out var number))
                {
                    if (patches.ContainsKey(number))
                    {
                        Log.Warn("Canceled loading {0}, Patch with Number {1} is already loaded", patchFile, number);
                        continue;
                    }

                    patches.Add(number, File.ReadAllBytes(patchFile).ToList());
                    continue;
                }

                Log.Warn("Could not parse Patch File {0}", patchFile);
            }

            if (!patches.Any())
            {
                Log.Error("Could not find any Patches");
                return;
            }

            var patchSettings = settings.Patches.FirstOrDefault(x => x.Key == smuModList.Length);
            if (patchSettings.Equals(default(KeyValuePair<int, Patch>)))
            {
                Log.Error("Could not find Patch settings for SMU count {0}", smuModList.Length);
                return;
            }

            if (patchSettings.Value.Order == null || patchSettings.Value.Order.Count() != smuModList.Length)
            {
                Log.Error("Invalid Patch Order");
                return;
            }

            for (var i = 0; i < smuModList.Length; i++)
            {
                if (!(patchSettings.Value.Order[i] is int patchNum))
                {
                    Log.Info("Skipping Module {0}", i);
                    continue;
                }

                if (!patches.TryGetValue(patchNum, out var patchBytes))
                {
                    Log.Error("Could not find Patch with Number {0} in Patchlist", patchNum);
                    return;
                }

                var modStart = smuModList[i];
                var modSize = BitConverter.ToInt32(biosBytes, modStart + settings.ModuleSizeOffset);

                var modVersionOld = modStart + settings.ModuleVersionOffset;
                var modVersionNew = settings.ModuleVersionOffset;
                var modVersionOldStr = $"{biosBytes[modVersionOld + 0x2]}.{biosBytes[modVersionOld + 0x1]}.{biosBytes[modVersionOld + 0x0]}";
                var modVersionNewStr = $"{patchBytes[modVersionNew + 0x2]}.{patchBytes[modVersionNew + 0x1]}.{patchBytes[modVersionNew + 0x0]}";

                if (modSize < patchBytes.Count)
                {
                    Log.Error("Patch {0} ({1}) for Module {2} is {3} bytes bigger than the Original ({4})", patchNum, modVersionNewStr,
                        i, (patchBytes.Count - modSize).ToString("N0"), modVersionOldStr);
                    return;
                }

                if (modSize > patchBytes.Count)
                {
                    while (patchBytes.Count != modSize)
                    {
                        patchBytes.Add(0xFF);
                    }
                }

                for (var b = 0; b < patchBytes.Count; b++)
                {
                    biosBytes[modStart + b] = patchBytes[b];
                }

                Log.Info("Patched SMU Module {0} ({1}-{2}) Version {3} -> {4}", i, modStart.ToString("X"), (modStart + modSize).ToString("X"), modVersionOldStr, modVersionNewStr);
            }

            if (biosSize != biosBytes.Length)
            {
                Log.Warn("Patched BIOS size does not match original BIOS size ({0}-{1})", biosSize.ToString("N0"), biosBytes.Length.ToString("N0"));
            }

            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(args[0]), $"{biosName}.SMU.MOD"), biosBytes);

            Log.Info("Finished Patching");
        }

        public static NLog.Logger Log =
            NLog.LogManager.GetCurrentClassLogger();
    }
}
