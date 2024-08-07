using Google.Protobuf.Collections;
using MA.Streaming.API;
using MA.Streaming.Core;
using MA.Streaming.OpenData;
using MA.Streaming.Proto.Client.Remote;
using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader
{
    internal class AtlasSessionHandler
    {
        private readonly TimeAndSizeWindowBatchProcessor<EventPacket> eventProcessor;
        private readonly TimeAndSizeWindowBatchProcessor<RowDataPacket> rowProcessor;
        private readonly TimeAndSizeWindowBatchProcessor<PeriodicDataPacket> periodicProcessor;
        private readonly TimeAndSizeWindowBatchProcessor<MarkerPacket> markerProcessor;
        private readonly DataFormatManagerService.DataFormatManagerServiceClient dataFormatManagerServiceClient;
        private readonly string dataSource;
        private readonly AtlasSessionWriter sessionWriter;
        private const long NumberOfNanosecondsInDay = 86400000000000;
        private double updated = DateTime.Now.ToOADate();
        public IClientSession clientSession { get; set; }
        public AtlasSessionHandler(AtlasSessionWriter atlasSessionWriter, string dataSource)
        {
            this.eventProcessor =
                new TimeAndSizeWindowBatchProcessor<EventPacket>(this.WriteBatchEventPackets,
                    new CancellationTokenSource(), 100, 0);
            this.rowProcessor =
                new TimeAndSizeWindowBatchProcessor<RowDataPacket>(this.WriteBatchRowPackets, new CancellationTokenSource(), 100, 0);
            this.periodicProcessor =
                new TimeAndSizeWindowBatchProcessor<PeriodicDataPacket>(this.WriteBatchPeriodicPackets, new CancellationTokenSource(), 100, 0);
            this.markerProcessor =
                new TimeAndSizeWindowBatchProcessor<MarkerPacket>(this.WriteBatchMarkerPackets, new CancellationTokenSource(), 100, 0);
            this.dataFormatManagerServiceClient = RemoteStreamingApiClient.GetDataFormatManagerClient();
            this.dataSource = dataSource;
            this.sessionWriter = atlasSessionWriter;
        }

        public void Handle(EventPacket packet)
        {
            this.eventProcessor.Add(packet);
        }

        public void Handle(RowDataPacket packet)
        {
            this.rowProcessor.Add(packet);
        }
        public void Handle(PeriodicDataPacket packet)
        {
            this.periodicProcessor.Add(packet);
        }
        public void Handle(MarkerPacket packet)
        {
            this.markerProcessor.Add(packet);
        }

        public void EndSession()
        {
            Console.WriteLine("Stop session notification received, waiting for all packets to be read.");
            do
            {

            } while (DateTime.Now - DateTime.FromOADate(updated) < TimeSpan.FromSeconds(120));
            sessionWriter.CloseSession(clientSession);
        }

        private Task WriteBatchEventPackets(IReadOnlyList<EventPacket> packets)
        {
            var eventIdentifiers = new List<Tuple<string, EventPacket>>();
            foreach (var packet in packets)
            {
                string eventIdentifier;
                if (packet.DataFormat.HasDataFormatIdentifier)
                    eventIdentifier = dataFormatManagerServiceClient.GetEvent(new GetEventRequest()
                    { DataFormatIdentifier = packet.DataFormat.DataFormatIdentifier, DataSource = dataSource }).Event;
                else
                    eventIdentifier = packet.DataFormat.EventIdentifier;
                eventIdentifiers.Add(new Tuple<string, EventPacket>(eventIdentifier, packet));
            }

            var newEvents = eventIdentifiers
                .Select(x => x.Item1)
                .Where(x => !this.sessionWriter.eventDefCache.ContainsKey(x)).ToList();
            if (newEvents.Any())
            {
                this.sessionWriter.AddBasicEventConfiguration(clientSession, newEvents);
            }

            foreach (var eventTuple in eventIdentifiers)
            {
                var timestamp = (long)eventTuple.Item2.Timestamp % NumberOfNanosecondsInDay;
                sessionWriter.AddEvent(clientSession, eventTuple.Item1, timestamp, eventTuple.Item2.RawValues.ToArray());
            }

            Interlocked.Exchange(ref this.updated, DateTime.Now.ToOADate());
            return Task.CompletedTask;
        }

        private Task WriteBatchPeriodicPackets(IReadOnlyList<PeriodicDataPacket> packets)
        {
            var parameterLists = new List<Tuple<RepeatedField<string>, PeriodicDataPacket>>();
            foreach (var packet in packets)
            {
                if (packet.DataFormat.HasDataFormatIdentifier)
                    parameterLists.Add(new Tuple<RepeatedField<string>, PeriodicDataPacket>(dataFormatManagerServiceClient.GetParametersList(new GetParametersListRequest()
                            { DataFormatIdentifier = packet.DataFormat.DataFormatIdentifier, DataSource = dataSource })
                        .Parameters, packet));
                else
                    parameterLists.Add(new Tuple<RepeatedField<string>, PeriodicDataPacket>(packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers, packet));
            }

            var newParameters = parameterLists
                .Select(x => x.Item1)
                .SelectMany(x => x)
                .Where(parameter => !this.sessionWriter.channelIdParameterDictionary.ContainsKey(parameter)).ToList();

            if (newParameters.Any())
            {
                this.sessionWriter.AddBasicParameterConfiguration(clientSession, newParameters);
            }

            foreach(var parameter in parameterLists)
            {
                var data = new List<double>();
                var status = new List<DataStatus>();
                var timestamps = new List<long>();
                for (var i = 0; i < parameter.Item1.Count; i++)
                {
                    var parameterIdentifier = parameter.Item1[i];
                    var column = parameter.Item2.Columns[i];
                    switch (column.ListCase)
                    {
                        case SampleColumn.ListOneofCase.DoubleSamples:
                            {
                                for (var j = 0; j < column.DoubleSamples.Samples.Count; j++)
                                {
                                    var dataSample = column.DoubleSamples.Samples[j];
                                    data.Add(dataSample.Value);
                                    status.Add(dataSample.Status);
                                    timestamps.Add((long)(parameter.Item2.StartTime + parameter.Item2.Interval * (uint)j) %
                                                   NumberOfNanosecondsInDay);
                                }

                                break;
                            }
                        case SampleColumn.ListOneofCase.Int32Samples:
                            {
                                for (var j = 0; j < column.Int32Samples.Samples.Count; j++)
                                {
                                    var dataSample = column.Int32Samples.Samples[j];
                                    data.Add(dataSample.Value);
                                    status.Add(dataSample.Status);
                                    timestamps.Add((long)(parameter.Item2.StartTime + parameter.Item2.Interval * (uint)j) %
                                                   NumberOfNanosecondsInDay);
                                }

                                break;
                            }
                        case SampleColumn.ListOneofCase.BoolSamples:
                            {
                                for (var j = 0; j < column.BoolSamples.Samples.Count; j++)
                                {
                                    var dataSample = column.BoolSamples.Samples[j];
                                    data.Add(dataSample.Value ? 1.0 : 0.0);
                                    status.Add(dataSample.Status);
                                    timestamps.Add((long)(parameter.Item2.StartTime + parameter.Item2.Interval * (uint)j) %
                                                   NumberOfNanosecondsInDay);
                                }

                                break;
                            }
                        case SampleColumn.ListOneofCase.StringSamples:
                            {
                                Console.WriteLine($"Unable to decode String Parameter {parameterIdentifier}");
                                continue;
                            }
                        case SampleColumn.ListOneofCase.None:
                        default:
                            {
                                Console.WriteLine($"No data found for parameter {parameterIdentifier}.");
                                continue;
                            }

                    }
                    if (!sessionWriter.TryAddData(clientSession, parameterIdentifier, data, timestamps))
                        Console.WriteLine($"Failed to write data to parameter {parameterIdentifier}.");
                    Interlocked.Exchange(ref this.updated, DateTime.Now.ToOADate());
                }
            }
            return Task.CompletedTask;
        }

        private Task WriteBatchRowPackets(IReadOnlyList<RowDataPacket> packets)
        {
            var parameterLists = new List<Tuple<RepeatedField<string>, RowDataPacket>>();
            foreach (var packet in packets)
            {
                if (packet.DataFormat.HasDataFormatIdentifier)
                    parameterLists.Add(new Tuple<RepeatedField<string>, RowDataPacket>(dataFormatManagerServiceClient.GetParametersList(new GetParametersListRequest()
                            { DataFormatIdentifier = packet.DataFormat.DataFormatIdentifier, DataSource = dataSource })
                        .Parameters, packet));
                else
                    parameterLists.Add(new Tuple<RepeatedField<string>, RowDataPacket>(packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers, packet));
            }

            var newParameters = parameterLists
                .Select(x => x.Item1)
                .SelectMany(x => x)
                .Where(parameter => !this.sessionWriter.channelIdParameterDictionary.ContainsKey(parameter)).ToList();

            if (newParameters.Any())
            {
                this.sessionWriter.AddBasicParameterConfiguration(clientSession, newParameters);
            }

            foreach(var parameterList in parameterLists)
            {
                for (var i = 0; i < parameterList.Item2.Rows.Count; i++)
                {
                    var row = parameterList.Item2.Rows[i];
                    switch (row.ListCase)
                    {
                        case SampleRow.ListOneofCase.DoubleSamples:
                        {
                            for (var j = 0; j < row.DoubleSamples.Samples.Count; j++)
                            {
                                sessionWriter.TryAddData(clientSession, parameterList.Item1[j],
                                    new List<double>() { row.DoubleSamples.Samples[j].Value },
                                    new List<long>()
                                        { (long)parameterList.Item2.Timestamps[i] % NumberOfNanosecondsInDay });
                            }

                            break;
                        }
                        case SampleRow.ListOneofCase.Int32Samples:
                        {
                            for (var j = 0; j < row.Int32Samples.Samples.Count; j++)
                            {
                                sessionWriter.TryAddData(clientSession, parameterList.Item1[j],
                                    new List<double>() { row.Int32Samples.Samples[j].Value },
                                    new List<long>()
                                        { (long)parameterList.Item2.Timestamps[i] % NumberOfNanosecondsInDay });
                            }

                            break;
                        }
                        case SampleRow.ListOneofCase.BoolSamples:
                        {
                            for (var j = 0; j < row.BoolSamples.Samples.Count; j++)
                            {
                                sessionWriter.TryAddData(clientSession, parameterList.Item1[j],
                                    new List<double>() { row.BoolSamples.Samples[j].Value ? 1.0 : 0.0 },
                                    new List<long>()
                                        { (long)parameterList.Item2.Timestamps[i] % NumberOfNanosecondsInDay });
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
                    Interlocked.Exchange(ref this.updated, DateTime.Now.ToOADate());
                }
            }
            return Task.CompletedTask;
        }
        private Task WriteBatchMarkerPackets(IReadOnlyList<MarkerPacket> packets)
        {
            foreach (var packet in packets)
            {
                var timestamp = (long)packet.Timestamp % NumberOfNanosecondsInDay;
                if (packet.Type == "Lap")
                {
                    sessionWriter.AddLap(clientSession, timestamp, (short)packet.Value, packet.Description, true);
                }
                else
                {
                    sessionWriter.AddMarker(clientSession, timestamp, packet.Label);
                }
            }
            Interlocked.Exchange(ref this.updated, DateTime.Now.ToOADate());
            return Task.CompletedTask;
        }
    }
}
