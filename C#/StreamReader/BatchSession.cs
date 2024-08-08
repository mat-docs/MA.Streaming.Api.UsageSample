using MA.Streaming.API;
using MA.Streaming.OpenData;
using MA.Streaming.Proto.Client.Remote;
using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader
{
    internal class BatchSession : ISession
    {
        private AtlasSessionHandler handler;
        private readonly IClientSession clientSession;
        private readonly PacketReaderService.PacketReaderServiceClient packetReaderServiceClient;
        private readonly AtlasSessionWriter sessionWriter;
        private readonly string streamApiSessionKey;
        public BatchSession(AtlasSessionHandler handler, IClientSession clientSession, string streamApiSessionKey, AtlasSessionWriter sessionWriter)
        {
            this.handler = handler;
            this.clientSession = clientSession;
            this.streamApiSessionKey = streamApiSessionKey;
            packetReaderServiceClient = RemoteStreamingApiClient.GetPacketReaderClient();
            this.sessionWriter = sessionWriter;
            handler.clientSession = clientSession;
        }

        public async void ReadPackets(CancellationToken cancellationToken, Connection connectionDetails)
        {
            var packetStream = packetReaderServiceClient.ReadPackets(new ReadPacketsRequest()
                { Connection = connectionDetails });

            Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        while (await packetStream.ResponseStream.MoveNext(cancellationToken))
                        {
                            var packetResponse = packetStream.ResponseStream.Current;
                            foreach (var response in packetResponse.Response)
                            {
                                HandleNewPacket(response.Packet);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to read packet stream.");
                }
            }, cancellationToken);
        }

        public Task HandleNewPacket(Packet packet)
        {
            if (packet.SessionKey != streamApiSessionKey)
            {
                Console.WriteLine("Session Key does not match. Ignoring the packet.");
                return Task.CompletedTask;
            }

            var packetType = packet.Type + "Packet";
            var content = packet.Content;
            try
            {
                switch (packetType)
                {
                    case "PeriodicDataPacket":
                        {
                            PeriodicDataPacket periodicDataPacket = PeriodicDataPacket.Parser.ParseFrom(content); 
                            Task.Run(() =>
                            {
                                handler.Handle(periodicDataPacket);
                            });
                            
                            break;
                        }
                    case "RowDataPacket":
                        {
                            RowDataPacket rowDataPacket = RowDataPacket.Parser.ParseFrom(content);
                           Task.Run(() =>
                            {
                                handler.Handle(rowDataPacket);
                            });
                            
                            break;
                        }
                    case "MarkerPacket":
                        {
                            MarkerPacket markerPacket = MarkerPacket.Parser.ParseFrom(content);
                            Task.Run(() =>
                            {
                                handler.Handle(markerPacket);
                            });
                            
                            break;
                        }
                    case "EventPacket":
                        {
                            EventPacket eventPacket = EventPacket.Parser.ParseFrom(content);
                            Task.Run(() =>
                            {
                                handler.Handle(eventPacket);
                            });
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
        public void UpdateSessionInfo(GetSessionInfoResponse sessionInfo)
        {
            this.sessionWriter.UpdateSessionInfo(clientSession, sessionInfo);
        }
        public void EndSession()
        {
            handler.EndSession();
        }
    }
}
