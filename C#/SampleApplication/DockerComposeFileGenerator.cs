// <copyright file="DockerComposeFileGenerator.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace MA.Streaming.Api.UsageSample;

internal class DockerComposeFileGenerator
{
    public void Generate(
        string filePath,
        bool createKafkaServicePart,
        bool createKeyGeneratorServicePart,
        int keyGeneratorServicePort,
        bool createStreamApiPart,
        int streamApiRpcPort,
        int prometheusPort)
    {
        if (!createKafkaServicePart &&
            !createKeyGeneratorServicePart &&
            !createStreamApiPart)
        {
            File.WriteAllText(filePath, "");
            return;
        }

        var dockerComposeFile = @$"
services:  

{this.CreateKafkaComposePart(createKafkaServicePart)}

{this.CreateKeyGeneratorServicePart(createKeyGeneratorServicePart, keyGeneratorServicePort)}

{this.CreateKeyStreamApiPart(createStreamApiPart, createKafkaServicePart, createKeyGeneratorServicePart, streamApiRpcPort, prometheusPort)}
";
        File.WriteAllText(filePath, dockerComposeFile);
    }

    private string CreateKeyStreamApiPart(
        bool createStreamApiPart,
        bool createKafkaServicePart,
        bool createKeyGeneratorServicePart,
        int streamApiRpcPort,
        int prometheusPort)
    {
        if (!createStreamApiPart)
        {
            return string.Empty;
        }

        var kafkaDepends = createKafkaServicePart
            ? @"      - kafka"
            : "";
        var keyGenDepends = createKeyGeneratorServicePart
            ? @"      - key-generator-service"
            : "";
        var dependsOn = @$"
    depends_on:
{kafkaDepends}
{keyGenDepends}";

        return $@"
  stream-api-server:
    image: mclarenapplied/streaming-proto-server-host:latest
    ports:
      - {streamApiRpcPort}:{streamApiRpcPort}
      - {prometheusPort}:{prometheusPort}
{dependsOn}
    restart: always
    environment:     
      CONFIG_PATH: /Configs/AppConfig.json
      AUTO_START: true
    volumes:
      - ./Configs:/app/Configs
";
    }

    private string CreateKeyGeneratorServicePart(bool keyGeneratorServicePart, int port)
    {
        return !keyGeneratorServicePart
            ? string.Empty
            : @$"
  key-generator-service:
    image: mclarenapplied/keygenerator-proto-server:latest
    ports:    
      - {port}:{port}
    environment:
      PORT: {port}
";
    }

    private string CreateKafkaComposePart(bool createKafkaServicePart)
    {
        return !createKafkaServicePart
            ? string.Empty
            : @"
  zookeeper:
    image: confluentinc/cp-zookeeper:latest    
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
    ports:
      - 12181:2181
    
  kafka:
    image: confluentinc/cp-kafka:latest
    depends_on:
      - zookeeper 
    ports:
      - 9092:9092    
      - 9094:9094    
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:9093,PLAINTEXT_HOST://kafka:9092,PLAINTEXT_LOCAL_HOST://localhost:9094
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT,PLAINTEXT_LOCAL_HOST:PLAINTEXT
      KAFKA_INTER_BROKER_LISTENER_NAME: PLAINTEXT
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
";
    }
}