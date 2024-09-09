// <copyright file="SessionKeyEventArgs.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

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