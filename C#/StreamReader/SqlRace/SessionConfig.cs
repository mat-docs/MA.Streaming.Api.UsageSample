// <copyright file="SessionConfig.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using System.Collections.Concurrent;

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace
{
    internal class SessionConfig : ISessionConfig
    {
        private readonly ConcurrentDictionary<string, uint> channelIdRowParameterDictionary = new();

        private readonly ConcurrentDictionary<string, Dictionary<uint, uint>> channelIdPeriodicParameterDictionary = new();

        private readonly ConcurrentDictionary<string, EventDefinition> eventDefCache = new();

        private readonly ConcurrentDictionary<string, ErrorDefinition> errorDefCache = new();

        private readonly ConcurrentDictionary<string, uint> synchroParameterChannelIdDictionary = new();

        public EventDefinition GetEventDefinition(string eventName)
        {
            return this.eventDefCache[eventName];
        }

        public uint GetParameterChannelId(string parameterName, uint interval)
        {
            return this.channelIdPeriodicParameterDictionary[parameterName][interval];
        }

        public uint GetParameterChannelId(string parameterName)
        {
            return this.channelIdRowParameterDictionary[parameterName];
        }

        public bool IsEventExistInConfig(string eventName)
        {
            return this.eventDefCache.ContainsKey(eventName);
        }

        public bool IsParameterExistInConfig(string parameterName)
        {
            return this.channelIdRowParameterDictionary.ContainsKey(parameterName);
        }

        public bool IsParameterExistInConfig(string parameterName, uint interval)
        {
            return this.channelIdPeriodicParameterDictionary
                .ContainsKey(parameterName) && this
                .channelIdPeriodicParameterDictionary[parameterName].ContainsKey(interval);
        }

        public void SetEventDefinition(string eventName, EventDefinition eventDefinition)
        {
            this.eventDefCache[eventName] = eventDefinition;
        }

        public void SetParameterChannelId(string parameterName, uint interval, uint channelId)
        {
            if (this.channelIdPeriodicParameterDictionary.TryGetValue(parameterName, out var intervals))
            {
                intervals.Add(interval, channelId);
                this.channelIdPeriodicParameterDictionary[parameterName] = intervals;
            }
            else
            {
                this.channelIdPeriodicParameterDictionary[parameterName] = new Dictionary<uint, uint>
                {
                    {
                        interval, channelId
                    }
                };
            }
        }

        public void SetParameterChannelId(string parameterName, uint channelId)
        {
            this.channelIdRowParameterDictionary[parameterName] = channelId;
        }

        public ErrorDefinition GetErrorDefinition(string errorIdentifier)
        {
            return this.errorDefCache[errorIdentifier];
        }

        public uint GetSynchroChannelId(string parameterName)
        {
            return this.synchroParameterChannelIdDictionary[parameterName];
        }

        public bool IsErrorExistInConfig(string errorName)
        {
            return this.errorDefCache.ContainsKey(errorName);
        }

        public bool IsSynchroExistInConfig(string parameterName)
        {
            return this.synchroParameterChannelIdDictionary.ContainsKey(parameterName);
        }

        public void SetErrorDefinition(string errorName, ErrorDefinition errorDefinition)
        {
            this.errorDefCache[errorName] = errorDefinition;
        }

        public void SetSynchroChannelId(string parameterName, uint channelId)
        {
            this.synchroParameterChannelIdDictionary[parameterName] = channelId;
        }
    }
}