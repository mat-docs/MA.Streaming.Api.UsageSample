using MA.Streaming.API;
using MA.Streaming.OpenData;
using MA.Streaming.Proto.Client.Remote;
using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader
{
    internal class BatchSession
    {
        private AtlasSessionHandler handler;
        private readonly IClientSession clientSession;
        private readonly string dataSource;
        private readonly PacketReaderService.PacketReaderServiceClient packetReaderServiceClient;
        private readonly AtlasSessionWriter sessionWriter;
        private readonly string streamApiSessionKey;
        private DateTime lastUpdated;
        public BatchSession(AtlasSessionHandler handler, IClientSession clientSession, string streamApiSessionKey, AtlasSessionWriter sessionWriter, string dataSource)
        {
            this.handler = handler;
            this.clientSession = clientSession;
            this.streamApiSessionKey = streamApiSessionKey;
            packetReaderServiceClient = RemoteStreamingApiClient.GetPacketReaderClient();
            this.sessionWriter = sessionWriter;
            this.dataSource = dataSource;
            this.lastUpdated = DateTime.Now;
            handler.clientSession = clientSession;
        }

        public void ReadPackets(CancellationToken cancellationToken, Connection connectionDetails)
        {
            var packetStream = packetReaderServiceClient.ReadPackets(new ReadPacketsRequest()
                { Connection = connectionDetails });

            _ = Task.Run(async () =>
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

        public void HandleNewPacket(Packet packet)
        {
            if (packet.SessionKey != streamApiSessionKey)
            {
                Console.WriteLine("Session Key does not match. Ignoring the packet.");
                return;
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
                            handler.Handle(periodicDataPacket);
                            break;
                        }
                    case "RowDataPacket":
                        {
                            RowDataPacket rowDataPacket = RowDataPacket.Parser.ParseFrom(content);
                            handler.Handle(rowDataPacket);
                            break;
                        }
                    case "MarkerPacket":
                        {
                            MarkerPacket markerPacket = MarkerPacket.Parser.ParseFrom(content);
                            handler.Handle(markerPacket);
                            break;
                        }
                    case "EventPacket":
                        {
                            EventPacket eventPacket = EventPacket.Parser.ParseFrom(content);
                            handler.Handle(eventPacket);
                            break;
                        }
                    default:
                        {
                            Console.WriteLine($"Unable to parse packet {packetType}.");
                            return;
                        }
                }
                this.lastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to handle packet {packetType} due to {ex.Message}");
            }
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
