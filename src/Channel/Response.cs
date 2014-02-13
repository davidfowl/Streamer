using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Streamer
{
    public class Response
    {
        public int Id { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "R")]
        public JToken Result { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "E")]
        public string Error { get; set; }
    }
}
