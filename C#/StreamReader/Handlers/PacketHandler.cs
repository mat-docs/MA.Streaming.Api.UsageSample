// <copyright file="PacketHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Handlers
{
    public class PacketHandler : IPacketHandler
    {
        private readonly PeriodicDataHandler periodicDataHandler;
        private readonly RowDataHandler rowDataHandler;
        private readonly MarkerHandler markerHandler;
        private readonly EventDataHandler eventDataHandler;

        public PacketHandler(
            PeriodicDataHandler periodicDataHandler,
            RowDataHandler rowDataHandler,
            MarkerHandler markerHandler,
            EventDataHandler eventDataHandler
        )
        {
            this.eventDataHandler = eventDataHandler;
            this.markerHandler = markerHandler;
            this.periodicDataHandler = periodicDataHandler;
            this.rowDataHandler = rowDataHandler;
        }

        public void Handle(Packet packet)
        {
            var packetType = packet.Type;
            var content = packet.Content;
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