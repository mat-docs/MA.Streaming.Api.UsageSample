// <copyright file="DockerComposeRunner.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Diagnostics;

namespace MA.Streaming.Api.UsageSample.Utility
{
    internal class DockerComposeRunner
    {
        public void Run(string dockerComposeFilePath, string composeProjectName)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = (Environment.OSVersion.Platform == PlatformID.Win32NT) ? "docker-compose.exe" : "docker-compose",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $"-f \"{ConvertToUnixPath(dockerComposeFilePath)}\" -p {composeProjectName} up -d"
            };

            var process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true,
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Console.WriteLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Console.WriteLine("data received as error: " + e.Data);
                }
            };

            process.Start();
            Console.WriteLine("Docker Compose command executed.");
        }

        private static string ConvertToUnixPath(string windowsPath)
        {
            var normalizedPath = Path.Combine(windowsPath.Split('\\'));
            var unixPath = normalizedPath.Replace("\\", "/");
            return unixPath;
        }
    }
}