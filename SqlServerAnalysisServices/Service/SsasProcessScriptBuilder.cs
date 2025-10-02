using SqlServerAnalysisServices.Utility;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlServerAnalysisServices.Service
{
    public record SsasProcessObject
    {
        [JsonPropertyName("database")]
        public string DatabaseName { get; private set; }

        [JsonPropertyName("partition")]
        public string PartitionName { get; private set; }

        [JsonPropertyName("table")]
        public string TableName { get; private set; }

        public SsasProcessObject Database(string databaseName)
        {
            DatabaseName = string.IsNullOrWhiteSpace(databaseName) ? null : databaseName;
            return this;
        }

        public SsasProcessObject Partition(string partitionName)
        {
            PartitionName = string.IsNullOrWhiteSpace(partitionName) ? null : partitionName;
            return this;
        }

        public SsasProcessObject Table(string tableName)
        {
            TableName = string.IsNullOrWhiteSpace(tableName) ? null : tableName;
            return this;
        }
    }

    public record SsasProcessScriptBuilder
    {
        private List<SsasProcessObject> Objects { get; init; } = [];
        public IEnumerable<SsasProcessObject> ProcessingObjects => Objects;

        public SsasProcessScriptBuilder CreateObject(Action<SsasProcessObject> configuratorCallback)
        {
            var configurator = new SsasProcessObject();
            configuratorCallback(configurator);
            Objects.Add(configurator);
            return this;
        }

        internal string Build() => $$"""
        {
            "refresh": {
                "type": "full",
                "objects": {{JsonSerializer.Serialize(Objects, CustomJsonSerializerOptions.DefaultWithIgnoreNull)}}
            }
        }
        """;

        public static SsasProcessScriptBuilder CreateFullRefreshScript(string databaseName)
        {
            var fullRefreshScript = new SsasProcessScriptBuilder();
            fullRefreshScript.CreateObject(obj => obj.Database(databaseName));
            return fullRefreshScript;
        }
    }
}