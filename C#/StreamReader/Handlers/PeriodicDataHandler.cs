
using System.Collections.Concurrent;
using Google.Protobuf.Collections;
using MA.Streaming.OpenData;
using MESL.SqlRace.Domain;
using Stream.Api.Stream.Reader.SqlRace;


namespace Stream.Api.Stream.Reader.Handlers
{
    internal class PeriodicDataHandler(
        AtlasSessionWriter sessionWriter, 
        StreamApiClient streamApiClient, 
        IClientSession clientSession, 
        ConfigurationProcessor configProcessor)
    {
        private ConcurrentDictionary<ulong, RepeatedField<string>> parameterListDataFormatCache = new();
        public bool TryHandle(PeriodicDataPacket packet)
        {
            var parameterList = packet.DataFormat.HasDataFormatIdentifier
                ? this.GetParameterList(packet.DataFormat.DataFormatIdentifier)
                : packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;

            var newParameters = parameterList
                .Where(x => !sessionWriter.IsParameterExistInConfig(x, packet.Interval, clientSession))
                .ToList();

            if (newParameters.Any())
            {
                // If the packet contains new parameters, put it in the list parameters to add to config and queue the packet to process later.
                foreach (var parameter in newParameters)
                    configProcessor.AddPeriodicPacketParameter(new Tuple<string, uint>(parameter, packet.Interval));
                return false;
            }

            for (var i = 0; i < parameterList.Count; i++)
            {
                var parameterIdentifier = parameterList[i];
                var column = packet.Columns[i];
                switch (column.ListCase)
                {
                    case SampleColumn.ListOneofCase.DoubleSamples:
                        {
                            var samples = column.DoubleSamples.Samples.Select(x => x.Value).ToList();
                            if (!sessionWriter.TryAddPeriodicData(clientSession, parameterIdentifier,
                                    samples, (long)packet.StartTime, packet.Interval))
                            {
                                return false;
                            }

                            break;
                        }
                    case SampleColumn.ListOneofCase.Int32Samples:
                        {
                            var samples = column.Int32Samples.Samples.Select(x => (double)x.Value).ToList();
                            if (!sessionWriter.TryAddPeriodicData(clientSession, parameterIdentifier,
                                    samples, (long)packet.StartTime, packet.Interval))
                            {
                                return false;
                            }

                            break;
                        }
                    case SampleColumn.ListOneofCase.BoolSamples:
                        {
                            var samples = column.BoolSamples.Samples.Select(x => x.Value ? 1.0 : 0.0).ToList();
                            if (!sessionWriter.TryAddPeriodicData(clientSession, parameterIdentifier,
                                    samples, (long)packet.StartTime, packet.Interval))
                            {
                                return false;
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
            }

            return true;
        }

        private RepeatedField<string> GetParameterList(ulong dataFormatId)
        {
            if (this.parameterListDataFormatCache.TryGetValue(dataFormatId, out RepeatedField<string>? parameterList))
                return parameterList;

            parameterList = streamApiClient.GetParameterList(dataFormatId);

            this.parameterListDataFormatCache[dataFormatId] = parameterList;

            return parameterList;
        }
    }
}
