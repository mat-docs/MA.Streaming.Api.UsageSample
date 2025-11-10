// <copyright file="SessionKeyEventArgs.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.EventArguments
{
    public class SessionKeyEventArgs : EventArgs
    {
        public SessionKeyEventArgs(string sessionKey)
        {
            this.SessionKey = sessionKey;
        }

        public string SessionKey { get; }
    }
}