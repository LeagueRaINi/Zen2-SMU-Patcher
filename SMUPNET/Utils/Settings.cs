using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace SMUPNET.Utils
{
    public class Patching
    {
        [JsonProperty("skip")]
        public int[] Skip { get; set; }

        [JsonProperty("order")]
        public int[] Order { get; set; }
    }

    public class Settings
    {
        [JsonProperty("modulePattern")]
        public string ModulePattern { get; set; }

        [JsonProperty("moduleStartOffset")]
        public int ModuleStartOffset { get; set; }

        [JsonProperty("moduleVersionOffset")]
        public int ModuleVersionOffset { get; set; }

        [JsonProperty("moduleSizeOffset")]
        public int ModuleSizeOffset { get; set; }

        [JsonProperty("patching")]
        public Dictionary<int, Patching> Patching { get; set; }

        public static Settings Load(string filePath)
        {
            if (File.Exists(filePath)) {
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(filePath));
            }

            File.WriteAllText(filePath, JsonConvert.SerializeObject(Settings.Default(), Formatting.Indented));

            throw new Exception("Could not find settings.json, created default file");
        }

        public static Settings Default()
        {
            return new Settings() {
                ModulePattern = "24 50 53 31 00 00",
                ModuleStartOffset = -0x10,
                ModuleVersionOffset = 0x50,
                ModuleSizeOffset = 0x5C,
                Patching = new Dictionary<int, Patching>() {
                    { 6, new Patching() {
                        Skip = null,
                        Order = new int[] {
                            1, 2, 3, 1, 2, 3
                        }
                    }},
                    { 8, new Patching() {
                        Skip = new int[] {
                            0, 7
                        },
                        Order = new int[] {
                            1, 2, 3, 1, 2, 3
                        }
                    }}
                }
            };
        }
    }
}
