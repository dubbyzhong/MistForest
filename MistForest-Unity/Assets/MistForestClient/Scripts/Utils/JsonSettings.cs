using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MistForest.Client.Utils
{
    public static class JsonSettings
    {
        public static readonly JsonSerializerSettings Default = new()
        {
            ContractResolver = new DefaultContractResolver()
            {
                NamingStrategy = new CamelCaseNamingStrategy(),
            },
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
        };
    }
}
