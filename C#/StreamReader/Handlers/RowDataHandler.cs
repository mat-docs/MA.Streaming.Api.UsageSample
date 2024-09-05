

using System.Collections.Concurrent;
using Google.Protobuf.Collections;
using MA.Streaming.OpenData;
using MESL.SqlRace.Domain;
using Stream.Api.Stream.Reader.SqlRace;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class RowDataHandler(
        AtlasSessionWriter sessionWriter,
        StreamApiClient streamApiClient,
        IClientSession clientSession,
        ConfigurationProcessor configProcessor)
    {
        private ConcurrentDictionary<ulong, RepeatedField<string>> parameterListDataFormatCache = new();

        public bool TryHandle(RowDataPacket packet)
        {
            RepeatedField<string> parameterList;
            try
            {
                parameterList = packet.DataFormat.HasDataFormatIdentifier
                    ? this.GetParameterList(packet.DataFormat.DataFormatIdentifier)
                    : packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parameter List Exception {ex}");
                parameterList = new RepeatedField<string>();
            }

            var newParameters = parameterList
                .Where(x => !sessionWriter.IsParameterExistInConfig(x, clientSession))
                .ToList();

            if (newParameters.Any())
            {
                // If the packet contains new parameters, put it in the parameter list to add to config and queue the packet to process later.
                configProcessor.AddRowPacketParameter(newParameters);
                return false;
            }

            for (var i = 0; i < packet.Rows.Count; i++)
            {
                var row = packet.Rows[i];
                var timestamp = (long)packet.Timestamps[i];

                switch (row.ListCase)
                {
                    case SampleRow.ListOneofCase.DoubleSamples:
                        {
                            if (!sessionWriter.TryAddData(clientSession, parameterList,
                                    row.DoubleSamples.Samples.Select(x => x.Value).ToList(), timestamp))
                            {
                                return false;
                            }

                            break;
                        }
                    case SampleRow.ListOneofCase.Int32Samples:
                        {
                            if (!sessionWriter.TryAddData(clientSession, parameterList,
                                    row.Int32Samples.Samples.Select(x => (double)x.Value).ToList(), timestamp))
                            {
                                return false;
                            }
                            
                            break;
                        }
                    case SampleRow.ListOneofCase.BoolSamples:
                        {
                            if (!sessionWriter.TryAddData(clientSession, parameterList,
                                    row.BoolSamples.Samples.Select(x => x.Value ? 1.0 : 0.0).ToList(), timestamp))
                            {
                                return false;
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
