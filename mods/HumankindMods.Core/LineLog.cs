using System;
using System.Globalization;
using System.IO;

namespace HumankindMods.Core
{
    /// <summary>Append-only, flushed per line so a game crash keeps everything written so far.</summary>
    public sealed class LineLog : IDisposable
    {
        private readonly StreamWriter writer;

        public LineLog(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            writer = new StreamWriter(path, append: true) { AutoFlush = true };
        }

        public void Write(string message)
        {
            writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + message);
        }

        public void Dispose() => writer.Dispose();
    }
}
