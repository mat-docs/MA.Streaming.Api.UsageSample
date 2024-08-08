using MA.Streaming.Core;
using MA.Streaming.OpenData;

namespace Stream.Api.Stream.Reader
{
    internal class TextSessionHandler
    {
        private TimeAndSizeWindowBatchProcessor<Packet> _batchProcessor;

        public TextSessionHandler(string rootFolder, string dataSource)
        {
            _batchProcessor = new TimeAndSizeWindowBatchProcessor<Packet>(this.WriteBatchPacket, new CancellationTokenSource());
        }

        private Task WriteBatchPacket(IReadOnlyList<Packet> packets)
        {
            return Task.CompletedTask;
        }
    }
}
