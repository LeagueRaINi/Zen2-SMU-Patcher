using Newtonsoft.Json;

namespace SMUPNET2
{
    public class Patch
    {
        [JsonProperty("order")]
        public int?[] Order { get; set; }
    }
}
