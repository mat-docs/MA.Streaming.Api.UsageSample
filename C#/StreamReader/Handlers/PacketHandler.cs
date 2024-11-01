// <copyright file="PacketHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class PacketHandler : BaseHandler<Packet>
    {
        private readonly IPacketHandler<PeriodicDataPacket> periodicDataHandler;
        private readonly IPacketHandler<RowDataPacket> rowDataHandler;
        private readonly IPacketHandler<MarkerPacket> markerHandler;
        private readonly IPacketHandler<EventPacket> eventDataHandler;
        private readonly IPacketHandler<ErrorPacket> errorDataHandler;
        private readonly IPacketHandler<RawCANDataPacket> rawCanDataHandler;
        private readonly IPacketHandler<SynchroDataPacket> synchroDataHandler;

        public PacketHandler(
            IPacketHandler<PeriodicDataPacket> periodicDataHandler,
            IPacketHandler<RowDataPacket> rowDataHandler,
            IPacketHandler<MarkerPacket> markerHandler,
            IPacketHandler<EventPacket> eventDataHandler,
            IPacketHandler<ErrorPacket> errorDataHandler,
            IPacketHandler<RawCANDataPacket> rawCanDataHandler,
            IPacketHandler<SynchroDataPacket> synchroDataHandler
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

        public override void Stop()
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

        public override void Handle(Packet packet)
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
                        this.periodicDataHandler.Handle(periodicDataPacket);
                        break;
                    }
                    case "RowData":
                    {
                        var rowDataPacket = RowDataPacket.Parser.ParseFrom(content);
                        this.rowDataHandler.Handle(rowDataPacket);
                        break;
                    }
                    case "Marker":
                    {
                        var markerPacket = MarkerPacket.Parser.ParseFrom(content);
                        this.markerHandler.Handle(markerPacket);
                        break;
                    }
                    case "Event":
                    {
                        var eventPacket = EventPacket.Parser.ParseFrom(content);
                        this.eventDataHandler.Handle(eventPacket);
                        break;
                    }
                    case "Error":
                    {
                        var errorPacket = ErrorPacket.Parser.ParseFrom(content);
                        this.errorDataHandler.Handle(errorPacket);
                        break;
                    }
                    case "RawCANData":
                    {
                        var rawCanPacket = RawCANDataPacket.Parser.ParseFrom(content);
                        this.rawCanDataHandler.Handle(rawCanPacket);
                        break;
                    }
                    case "SynchroData":
                    {
                        var synchroDataPacket = SynchroDataPacket.Parser.ParseFrom(content);
                        this.synchroDataHandler.Handle(synchroDataPacket);
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