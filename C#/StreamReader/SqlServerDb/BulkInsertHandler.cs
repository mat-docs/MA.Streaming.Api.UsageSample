// <copyright file="BulkInsertHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.Core;

namespace Stream.Api.Stream.Reader.SqlServerDb
{
    internal class BulkInsertHandler : BulkInsert
    {
        private readonly TimeAndSizeWindowBatchProcessor<Dictionary<string, ICollection<(ulong, double)>>>
            batchProcessor;

        public BulkInsertHandler(string connectionString) : base(connectionString)
        {
            batchProcessor =
                new TimeAndSizeWindowBatchProcessor<Dictionary<string, ICollection<(ulong, double)>>>(WriteBatchData,
                    new CancellationTokenSource(), 15000, 1);
        }

        public void InsertData(Dictionary<string, ICollection<(ulong, double)>> data)
        {
            batchProcessor.Add(data);
        }

        private Task WriteBatchData(IReadOnlyList<Dictionary<string, ICollection<(ulong, double)>>> dataCollection)
        {
            foreach (var data in dataCollection) base.InsertData(data, "Sample_Test");
            return Task.CompletedTask;
        }
    }
}