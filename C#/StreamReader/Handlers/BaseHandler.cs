// <copyright file="BaseHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal abstract class BaseHandler<T> : IPacketHandler<T>
    {
        private DateTime lastUpdated = DateTime.UtcNow;

        public abstract void Handle(T packet);

        public virtual void Stop()
        {
            do
            {
                Task.Delay(1000).Wait();
            }
            while (DateTime.UtcNow - this.lastUpdated < TimeSpan.FromSeconds(10));
        }

        protected void Update()
        {
            this.lastUpdated = DateTime.UtcNow;
        }
    }
}