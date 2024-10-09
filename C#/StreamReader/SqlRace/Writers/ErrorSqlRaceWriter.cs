// <copyright file="ErrorSqlRaceWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    internal class ErrorSqlRaceWriter : ConfigSensitiveSqlRaceWriter
    {
        public ErrorSqlRaceWriter(IClientSession clientSession, ReaderWriterLockSlim configLock)
            : base(clientSession, configLock)
        {
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            if (data is not ISqlRaceErrorDto errorDto)
            {
                return false;
            }

            bool success;

            switch (errorDto)
            {
                case { ErrorStatus: ErrorStatus.Set, ErrorType: ErrorType.Current }:
                    success = this.WriteValuesToErrorParams(errorDto.LoggedParameterChannel, 1, errorDto.Timestamp) &&
                              this.WriteValuesToErrorParams(errorDto.CurrentParameterChannel, 1, errorDto.Timestamp);
                    break;
                case { ErrorStatus: ErrorStatus.Cleared, ErrorType: ErrorType.Current }:
                    success = this.WriteValuesToErrorParams(errorDto.LoggedParameterChannel, 1, errorDto.Timestamp) &&
                              this.WriteValuesToErrorParams(errorDto.CurrentParameterChannel, 0, errorDto.Timestamp);
                    break;
                case { ErrorStatus: ErrorStatus.Set, ErrorType: ErrorType.Logged }:
                    success = this.WriteValuesToErrorParams(errorDto.LoggedParameterChannel, 1, errorDto.Timestamp);
                    break;
                case { ErrorStatus: ErrorStatus.Cleared, ErrorType: ErrorType.Logged }:
                    success = this.WriteValuesToErrorParams(errorDto.CurrentParameterChannel, 0, errorDto.Timestamp) &&
                              this.WriteValuesToErrorParams(errorDto.LoggedParameterChannel, 0, errorDto.Timestamp);
                    break;
                default:
                {
                    Console.WriteLine($"Unable to write error {errorDto.ErrorIdentifier}.");
                    success = false;
                    break;
                }
            }

            return success;
        }

        private bool WriteValuesToErrorParams(uint channel, ushort value, long timestamp)
        {
            var dataBytes = BitConverter.GetBytes(value);
            bool success;
            try
            {
                this.ConfigLock.EnterReadLock();
                this.ClientSession.Session.AddRowData(timestamp, [channel], dataBytes);
                success = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write error due to {ex.Message}");
                success = false;
            }
            finally
            {
                this.ConfigLock.ExitReadLock();
            }

            return success;
        }
    }
}