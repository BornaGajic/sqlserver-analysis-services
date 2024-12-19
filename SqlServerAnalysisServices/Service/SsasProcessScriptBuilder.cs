using SqlServerAnalysisServices.Utility;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlServerAnalysisServices.Service
{
    public record SsasProcessObject
    {
        [JsonPropertyName("database")]
        internal string DatabaseName { get; set; }

        [JsonPropertyName("partition")]
        internal string PartitionName { get; set; }

        [JsonPropertyName("table")]
        internal string TableName { get; set; }

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
                "objects": {{JsonSerializer.Serialize(Objects, CustomJsonSerializerOptions.Default)}}
            }
        }
        """;
    }
}