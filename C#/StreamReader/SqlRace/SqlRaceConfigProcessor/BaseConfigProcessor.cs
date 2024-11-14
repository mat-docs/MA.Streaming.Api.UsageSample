﻿// <copyright file="BaseConfigProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.DataPlatforms.DataRecorder.SqlRaceWriter.Abstractions;

using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader.SqlRace.SqlRaceConfigProcessor
{
    internal abstract class BaseConfigProcessor<T> : IConfigProcessor<T>
    {
        protected ConfigurationSetManager ConfigurationSetManager;
        protected RationalConversion DefaultConversion;
        protected IClientSession ClientSession;
        protected ReaderWriterLockSlim ConfigLock;
        protected SessionConfig SessionConfig;

        protected BaseConfigProcessor(
            ConfigurationSetManager configurationSetManager,
            RationalConversion defaultConversion,
            IClientSession clientSession,
            ReaderWriterLockSlim configLock,
            SessionConfig sessionConfig)
        {
            this.ConfigurationSetManager = configurationSetManager;
            this.DefaultConversion = defaultConversion;
            this.ClientSession = clientSession;
            this.ConfigLock = configLock;
            this.SessionConfig = sessionConfig;
        }

        protected uint GenerateUniqueChannelId()
        {
            return this.ClientSession.Session.ReserveNextAvailableRowChannelId() % 2147483647;
        }

        public abstract event EventHandler? ProcessCompleted;

        public abstract void AddToConfig(T item);
    }
}