using System;
using System.IO;

namespace UnderwaterGliderTwin.Logging
{
    public sealed class TwinLogger
    {
        private readonly string logDirectory;

        public TwinLogger(string logDirectory)
        {
            this.logDirectory = logDirectory;
            Directory.CreateDirectory(logDirectory);
        }

        public void AppendLoad(string message)
        {
            Append("load.log", message);
        }

        public void AppendAlarm(string message)
        {
            Append("alarm.log", message);
        }

        public void AppendPlayback(string message)
        {
            Append("playback.log", message);
        }

        private void Append(string fileName, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(logDirectory, fileName), line);
        }
    }
}
