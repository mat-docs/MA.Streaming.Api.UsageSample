// <copyright file="SqlRaceRecorder.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace
{
    public class SqlRaceRecorder : ISqlRaceRecorder
    {
        private readonly string connectionString;
        private readonly Guid recorderGuid;

        public SqlRaceRecorder(string connectionString)
        {
            this.connectionString = connectionString;
            this.recorderGuid = Guid.NewGuid();
        }

        public void StartRecorder()
        {
            var recorderConfiguration = RecordersConfiguration.GetRecordersConfiguration();

            recorderConfiguration.AddConfiguration(
                this.recorderGuid,
                "SQLServer",
                this.connectionString,
                this.connectionString,
                this.connectionString,
                false
            );
        }

        public void StopRecorder()
        {
            var recorderConfiguration = RecordersConfiguration.GetRecordersConfiguration();
            recorderConfiguration.RemoveConfiguration(this.recorderGuid);
        }
    }
}