using System.Collections.Generic;
using Newtonsoft.Json;

namespace SMUPNET2
{
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
        public Dictionary<int, Patch> Patches { get; set; }

        public static Settings Default()
        {
            return new Settings {
                ModulePattern = "24 50 53 31 00 00",
                ModuleStartOffset = -0x10,
                ModuleVersionOffset = 0x60,
                ModuleSizeOffset = 0x6C,
                Patches = new Dictionary<int, Patch>
                {
                    { 6, new Patch {
                        Order = new int?[]
                        {
                            1,2,3,1,2,3
                        }}
                    },
                    { 8, new Patch {
                        Order = new int?[]
                        {
                            null,1,2,3,1,2,3,null
                        }}
                    }
                }
            };
        }
    }
}
