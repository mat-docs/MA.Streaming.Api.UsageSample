// <copyright file="OptionsManager.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader
{
    internal class OptionsManager
    {
        private readonly SessionManagement sessionManager;

        public OptionsManager(SessionManagement sessionManagement)
        {
            this.sessionManager = sessionManagement;
        }

        public void RunOptions()
        {
            Console.WriteLine(
                """
                What kind of session would you like to record?
                Type the number and press enter:

                [0] = Live session.
                [1] = Historical session.
                """);
            var option = Console.ReadLine();
            switch (option)
            {
                case "0":
                {
                    this.RecordLiveSession();
                    break;
                }
                case "1":
                {
                    this.RecordHistoricalSession();
                    break;
                }
                default:
                {
                    return;
                }
            }
        }

        private void RecordLiveSession()
        {
            this.sessionManager.GetLiveSessions();
        }

        private void RecordHistoricalSession()
        {
            var exit = false;
            do
            {
                this.sessionManager.GetHistoricalSessions();
                Console.WriteLine(
                    """
                    Finished recording historical session. Would you like to record another one?
                    [0] = No.
                    [1] = Yes.
                    """);
                var input = Console.ReadLine();
                switch (input)
                {
                    case "1":
                    {
                        break;
                    }
                    case "0":
                    default:
                    {
                        exit = true;
                        break;
                    }
                }
            }
            while (!exit);
        }
    }
}