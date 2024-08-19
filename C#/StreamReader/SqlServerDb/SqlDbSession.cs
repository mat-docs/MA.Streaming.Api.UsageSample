// <copyright file="SqlDbSession.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using Google.Protobuf.Collections;
using MA.Streaming.API;
using MA.Streaming.OpenData;
using MA.Streaming.Proto.Client.Remote;

namespace Stream.Api.Stream.Reader.SqlServerDb
{
    internal class SqlDbSession : ISession
    {
        private const long NumberOfNanosecondsInDay = 86400000000000;
        private readonly BulkInsertHandler bulkInsertHandler;
        private readonly DataFormatManagerService.DataFormatManagerServiceClient dataFormatManagerServiceClient;
        private readonly string dataSource;
        private readonly PacketReaderService.PacketReaderServiceClient packetReaderServiceClient;
        private readonly string streamApiSessionKey;
        private readonly Dictionary<ulong, string> eventDataFormatCache = new();
        private readonly ConcurrentDictionary<ulong, string> eventDataFormatCacheConcurrent = new();
        private readonly Dictionary<ulong, RepeatedField<string>> parameterDataFormatCache = new();
        private readonly ConcurrentDictionary<ulong, RepeatedField<string>> parameterDataFormatCacheConcurrent = new();

        public SqlDbSession(string dataSource, string streamApiSessionKey, BulkInsertHandler bulkInsertHandler)
        {
            this.dataSource = dataSource;
            this.streamApiSessionKey = streamApiSessionKey;
            this.bulkInsertHandler = bulkInsertHandler;
            packetReaderServiceClient = RemoteStreamingApiClient.GetPacketReaderClient();
            dataFormatManagerServiceClient = RemoteStreamingApiClient.GetDataFormatManagerClient();
        }

        public void ReadPackets(CancellationToken cancellationToken, Connection connectionDetails)
        {
            var packetStream = packetReaderServiceClient.ReadPackets(new ReadPacketsRequest()
                { Connection = connectionDetails });

            Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    while (await packetStream.ResponseStream.MoveNext(cancellationToken))
                    {
                        var packetResponse = packetStream.ResponseStream.Current;
                        foreach (var response in packetResponse.Response) HandleNewPacket(response.Packet);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to read packet stream.");
                }
            }, cancellationToken);
        }

        public void UpdateSessionInfo(GetSessionInfoResponse response)
        {
            return;
        }

        public void EndSession()
        {
            Console.WriteLine("Session Stop Received.");
        }

        public Task HandleNewPacket(Packet packet)
        {
            var packetType = packet.Type + "Packet";
            var content = packet.Content;
            try
            {
                switch (packetType)
                {
                    case "PeriodicDataPacket":
                    {
                        PeriodicDataPacket periodicDataPacket = PeriodicDataPacket.Parser.ParseFrom(content);
                        HandlePeriodicPacket(periodicDataPacket);
                        //Task.Run(() =>
                        //{
                        //    HandlePeriodicData(periodicDataPacket);
                        //});
                        break;
                    }
                    case "RowDataPacket":
                    {
                        RowDataPacket rowDataPacket = RowDataPacket.Parser.ParseFrom(content);
                        HandleRowData(rowDataPacket);
                        //Task.Run(() =>
                        //{
                        //    HandleRowData(rowDataPacket);
                        //});

                        break;
                    }
                    case "MarkerPacket":
                    {
                        MarkerPacket markerPacket = MarkerPacket.Parser.ParseFrom(content);
                        HandleMarkerPacket(markerPacket);
                        //Task.Run(() =>
                        //{
                        //    HandleMarkerPacket(markerPacket);
                        //});
                        break;
                    }
                    case "EventPacket":
                    {
                        EventPacket eventPacket = EventPacket.Parser.ParseFrom(content);
                        HandleEventPacket(eventPacket);
                        //Task.Run(() =>
                        //{
                        //    HandleEventPacket(eventPacket);
                        //});
                        break;
                    }
                    default:
                    {
                        Console.WriteLine($"Unable to parse packet {packetType}.");
                        return Task.CompletedTask;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to handle packet {packetType} due to {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private void HandlePeriodicPacket(PeriodicDataPacket packet)
        {
            try
            {
                var parameterList = packet.DataFormat.HasDataFormatIdentifier
                    ? GetParameterListAsync(packet.DataFormat.DataFormatIdentifier).Result
                    : packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;

                var parameterData = new Dictionary<string, ICollection<(ulong, double)>>();
                for (var i = 0; i < parameterList.Count; i++)
                {
                    if (parameterList[i] != "TimebaseTest_1ms_uint32_1Hz") continue;
                    parameterData[parameterList[i]] = new List<(ulong, double)>();
                    var column = packet.Columns[i];
                    switch (column.ListCase)
                    {
                        case SampleColumn.ListOneofCase.DoubleSamples:
                        {
                            for (var j = 0; j < column.DoubleSamples.Samples.Count; j++)
                            {
                                var dataSample = column.DoubleSamples.Samples[j];
                                parameterData[parameterList[i]].Add((
                                    packet.StartTime + packet.Interval * (ulong)j, dataSample.Value));
                            }

                            break;
                        }
                        case SampleColumn.ListOneofCase.Int32Samples:
                        {
                            for (var j = 0; j < column.Int32Samples.Samples.Count; j++)
                            {
                                var dataSample = column.Int32Samples.Samples[j];
                                parameterData[parameterList[i]].Add((
                                    packet.StartTime + packet.Interval * (ulong)j, dataSample.Value));
                            }

                            break;
                        }
                        case SampleColumn.ListOneofCase.BoolSamples:
                        {
                            for (var j = 0; j < column.BoolSamples.Samples.Count; j++)
                            {
                                var dataSample = column.BoolSamples.Samples[j];
                                parameterData[parameterList[i]].Add((
                                    packet.StartTime + packet.Interval * (ulong)j, dataSample.Value ? 1.0 : 0.0));
                            }

                            break;
                        }
                        case SampleColumn.ListOneofCase.StringSamples:
                        {
                            Console.WriteLine($"Unable to decode String Parameter {parameterList[i]}");
                            continue;
                        }
                        case SampleColumn.ListOneofCase.None:
                        default:
                        {
                            Console.WriteLine($"No data found for parameter {parameterList[i]}.");
                            continue;
                        }
                    }
                }

                if (parameterData.Count > 0) bulkInsertHandler.InsertData(parameterData, "Sample_Test");
                //bulkInsertHandler.InsertData(parameterData);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void HandleRowData(RowDataPacket packet)
        {
            try
            {
                var parameterList = packet.DataFormat.HasDataFormatIdentifier
                    ? GetParameterListAsync(packet.DataFormat.DataFormatIdentifier).Result
                    : packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;

                var parameterDictionary = new Dictionary<string, ICollection<(ulong, double)>>();
                foreach (var parameter in parameterList) parameterDictionary[parameter] = new List<(ulong, double)>();

                for (var i = 0; i < packet.Rows.Count; i++)
                {
                    var row = packet.Rows[i];
                    var timestamp = packet.Timestamps[i] % NumberOfNanosecondsInDay;

                    switch (row.ListCase)
                    {
                        case SampleRow.ListOneofCase.DoubleSamples:
                        {
                            for (var j = 0; j < row.DoubleSamples.Samples.Count; j++)
                            {
                                if (parameterList[j] != "TimebaseTest_1ms_uint32_1Hz") continue;
                                parameterDictionary[parameterList[j]]
                                    .Add((timestamp, row.DoubleSamples.Samples[j].Value));
                            }

                            break;
                        }
                        case SampleRow.ListOneofCase.Int32Samples:
                        {
                            for (var j = 0; j < row.Int32Samples.Samples.Count; j++)
                            {
                                if (parameterList[j] != "TimebaseTest_1ms_uint32_1Hz") continue;
                                parameterDictionary[parameterList[j]]
                                    .Add(((timestamp, row.Int32Samples.Samples[j].Value)));
                            }

                            break;
                        }
                        case SampleRow.ListOneofCase.BoolSamples:
                        {
                            for (var j = 0; j < row.BoolSamples.Samples.Count; j++)
                            {
                                if (parameterList[j] != "TimebaseTest_1ms_uint32_1Hz") continue;
                                parameterDictionary[parameterList[j]].Add((timestamp,
                                    row.BoolSamples.Samples[j].Value ? 1.0 : 0.0));
                            }

                            break;
                        }
                        case SampleRow.ListOneofCase.StringSamples:
                        {
                            Console.WriteLine("Unable to add String data for this row.");
                            continue;
                        }
                        case SampleRow.ListOneofCase.None:
                        default:
                        {
                            Console.WriteLine("No rows found for this packet.");
                            continue;
                        }
                    }
                }

                if (parameterDictionary.Count > 0) bulkInsertHandler.InsertData(parameterDictionary, "Sample_Test");
                //bulkInsertHandler.InsertData(parameterDictionary);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void HandleMarkerPacket(MarkerPacket packet)
        {
            try
            {
                var timestamp = packet.Timestamp;
                bulkInsertHandler.InsertMarkerData(
                    new List<(ulong, string, string)>() { (timestamp, packet.Label, packet.Type) }, "Marker_Test");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void HandleEventPacket(EventPacket packet)
        {
            try
            {
                var eventIdentifier = packet.DataFormat.HasDataFormatIdentifier
                    ? GetEventIdentifierAsync(packet.DataFormat.DataFormatIdentifier).Result
                    : packet.DataFormat.EventIdentifier;

                var timestamp = packet.Timestamp % NumberOfNanosecondsInDay;
                bulkInsertHandler.InsertEventData(
                    new List<(ulong, string, double, double, double)>()
                    {
                        (timestamp, eventIdentifier, packet.RawValues[0], packet.RawValues[1], packet.RawValues[2])
                    }, "Event_Test");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task<RepeatedField<string>?> GetParameterListAsync(ulong dataFormat)
        {
            try
            {
                if (parameterDataFormatCacheConcurrent.TryGetValue(dataFormat,
                        out RepeatedField<string>? parameterList)) return parameterList;
                parameterList = dataFormatManagerServiceClient.GetParametersListAsync(new GetParametersListRequest()
                    { DataFormatIdentifier = dataFormat, DataSource = dataSource }).ResponseAsync.Result.Parameters;
                parameterDataFormatCacheConcurrent[dataFormat] = parameterList;
                return parameterList;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return null;
        }

        private RepeatedField<string> GetParameterList(ulong dataFormat)
        {
            try
            {
                lock (parameterDataFormatCache)
                {
                    if (parameterDataFormatCache.TryGetValue(dataFormat, out RepeatedField<string>? parameterList))
                        return parameterList;
                    parameterList = dataFormatManagerServiceClient.GetParametersList(new GetParametersListRequest()
                        { DataFormatIdentifier = dataFormat, DataSource = dataSource }).Parameters;
                    parameterDataFormatCache[dataFormat] = parameterList;
                    return parameterList;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return null;
        }

        private async Task<string> GetEventIdentifierAsync(ulong dataFormat)
        {
            if (eventDataFormatCacheConcurrent.TryGetValue(dataFormat, out string? eventIdentifier))
                return eventIdentifier;

            eventIdentifier = dataFormatManagerServiceClient.GetEventAsync(new GetEventRequest()
                { DataFormatIdentifier = dataFormat, DataSource = dataSource }).ResponseAsync.Result.Event;
            eventDataFormatCacheConcurrent[dataFormat] = eventIdentifier;
            return eventIdentifier;
        }

        private string GetEventIdentifier(ulong dataFormat)
        {
            if (eventDataFormatCache.TryGetValue(dataFormat, out string? eventIdentifier)) return eventIdentifier;

            eventIdentifier = dataFormatManagerServiceClient.GetEvent(new GetEventRequest()
                { DataFormatIdentifier = dataFormat, DataSource = dataSource }).Event;
            eventDataFormatCache[dataFormat] = eventIdentifier;
            return eventIdentifier;
        }
    }
}