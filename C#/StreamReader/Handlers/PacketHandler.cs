// <copyright file="PacketHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class PacketHandler : BaseHandler, IPacketHandler
    {
        private readonly PeriodicDataHandler periodicDataHandler;
        private readonly RowDataHandler rowDataHandler;
        private readonly MarkerHandler markerHandler;
        private readonly EventDataHandler eventDataHandler;
        private readonly ErrorDataHandler errorDataHandler;
        private readonly RawCanDataHandler rawCanDataHandler;
        private readonly SynchroDataHandler synchroDataHandler;

        public PacketHandler(
            PeriodicDataHandler periodicDataHandler,
            RowDataHandler rowDataHandler,
            MarkerHandler markerHandler,
            EventDataHandler eventDataHandler,
            ErrorDataHandler errorDataHandler,
            RawCanDataHandler rawCanDataHandler,
            SynchroDataHandler synchroDataHandler
        )
        {
            this.eventDataHandler = eventDataHandler;
            this.markerHandler = markerHandler;
            this.periodicDataHandler = periodicDataHandler;
            this.rowDataHandler = rowDataHandler;
            this.errorDataHandler = errorDataHandler;
            this.rawCanDataHandler = rawCanDataHandler;
            this.synchroDataHandler = synchroDataHandler;
        }

        public new void Stop()
        {
            this.synchroDataHandler.Stop();
            this.rawCanDataHandler.Stop();
            this.errorDataHandler.Stop();
            this.eventDataHandler.Stop();
            this.markerHandler.Stop();
            this.rowDataHandler.Stop();
            this.periodicDataHandler.Stop();
            base.Stop();
        }

        public void Handle(Packet packet)
        {
            var packetType = packet.Type;
            var content = packet.Content;
            this.Update();
            try
            {
                switch (packetType)
                {
                    case "PeriodicData":
                    {
                        var periodicDataPacket = PeriodicDataPacket.Parser.ParseFrom(content);
                        this.periodicDataHandler.TryHandle(periodicDataPacket);
                        break;
                    }
                    case "RowData":
                    {
                        var rowDataPacket = RowDataPacket.Parser.ParseFrom(content);
                        this.rowDataHandler.TryHandle(rowDataPacket);
                        break;
                    }
                    case "Marker":
                    {
                        var markerPacket = MarkerPacket.Parser.ParseFrom(content);
                        this.markerHandler.TryHandle(markerPacket);
                        break;
                    }
                    case "Event":
                    {
                        var eventPacket = EventPacket.Parser.ParseFrom(content);
                        this.eventDataHandler.TryHandle(eventPacket);
                        break;
                    }
                    case "Error":
                    {
                        var errorPacket = ErrorPacket.Parser.ParseFrom(content);
                        this.errorDataHandler.TryHandle(errorPacket);
                        break;
                    }
                    case "RawCANData":
                    {
                        var rawCanPacket = RawCANDataPacket.Parser.ParseFrom(content);
                        this.rawCanDataHandler.TryHandle(rawCanPacket);
                        break;
                    }
                    case "SynchroData":
                    {
                        var synchroDataPacket = SynchroDataPacket.Parser.ParseFrom(content);
                        this.synchroDataHandler.TryHandle(synchroDataPacket);
                        break;
                    }
                    default:
                    {
                        Console.WriteLine($"Unable to parse packet {packetType}.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to handle packet {packetType} due to {ex.Message}");
            }
        }
    }
}