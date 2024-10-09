// <copyright file="DefaultValues.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace MA.Streaming.Api.UsageSample.Configs
{
    internal class DefaultValues
    {
        public int Strategy { get; set; }

        public string KafkaUri { get; set; } = "kafka:9092";

        public string ApiUri { get; set; } = "localhost:13579";

        public int MetricsPort { get; set; }

        public string KeyGenUri { get; set; } = "key-generator-service:15379";

        public string DataPublishStream { get; set; } = "Stream1";

        public string DataPublishKey { get; set; } = "session_1";
    }
}