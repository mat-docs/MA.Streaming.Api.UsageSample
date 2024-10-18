// <copyright file="BaseHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class BaseHandler
    {
        private DateTime lastUpdated = DateTime.Now;

        public void Stop()
        {
            do
            {
                Task.Delay(1000).Wait();
            }
            while (DateTime.Now - this.lastUpdated < TimeSpan.FromSeconds(10));
        }

        protected void Update()
        {
            this.lastUpdated = DateTime.Now;
        }
    }
}