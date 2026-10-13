using System;
using System.IO;
using System.Linq;
using System.Text;

namespace PaperAutoUpdater
{
    // Mirrors everything printed to the console into PaperLauncherLogs\launcher_<date>.log
    public static class Logger
    {
        public const string LogFolderName = "PaperLauncherLogs";
        private const int MaxLogFiles = 30; // oldest logs are removed beyond this threshold

        private static readonly object Lock = new object(); // Ensures thread safety when writing to the log file
        private static StreamWriter? _writer;
        private static readonly StringBuilder _line = new StringBuilder(); // Buffer used to construct full lines before logging

        // While true (header art, settings menus) console text is NOT written to the log
        public static bool Paused { get; set; }

        // Initializes the logging system, creates the directory, and hooks into standard output
        public static void Init()
        {
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), LogFolderName);
                Directory.CreateDirectory(dir);
                TrimOldLogs(dir); // Cleans up old log files before creating a new one

                string file = Path.Combine(dir, $"launcher_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
                var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };

                LogEvent("=== AutoLauncher-for-Paper session started ===");
                AppDomain.CurrentDomain.ProcessExit += (s, e) => LogEvent("=== Session ended ==="); // Logs graceful exits

                // Tee: console keeps working exactly as before, and every line is also logged
                Console.SetOut(new TeeWriter(Console.Out));
            }
            catch (Exception ex)
            {
                // Logging must never stop the launcher from starting
                _writer = null;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠️  [WARNING] Could not create log folder: {ex.Message}");
                Console.ResetColor();
            }
        }

        // Deletes older log files to prevent unbounded disk usage growth
        private static void TrimOldLogs(string dir)
        {
            try
            {
                var old = Directory.GetFiles(dir, "launcher_*.log").OrderByDescending(f => f, StringComparer.Ordinal).Skip(MaxLogFiles - 1);

                foreach (var f in old)
                {
                    try
                    {
                        File.Delete(f);
                    }
                    catch { /* Ignora se o ficheiro estiver trancado por outro programa */ }
                }
            }
            catch { }
        }

        // Writes a line directly to the log file, even if logging is currently Paused (e.g., for system events)
        public static void LogEvent(string text)
        {
            lock (Lock)
            {
                try { _writer?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}"); } catch { }
            }
        }

        // Buffers characters until a full line is formed, then writes the line to the log file
        internal static void FeedString(string text)
        {
            if (_writer == null) return;

            lock (Lock)
            {
                foreach (char c in text)
                {
                    // \r resets the current line buffer (used for countdowns/progress bars)
                    if (c == '\r') { _line.Clear(); continue; }
                    // If it's not a newline, append it to the buffer and continue
                    if (c != '\n') { _line.Append(c); continue; }

                    string lineText = _line.ToString().TrimEnd();
                    _line.Clear(); // Clear the buffer for the next line
                    if (Paused || lineText.Length == 0) continue; // Do not log empty lines or if logging is paused

                    try { _writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] {lineText}"); } catch { }
                }
            }
        }

        // Compatibility wrapper for single-character feeds
        internal static void Feed(char c) => FeedString(c.ToString());

        // A custom TextWriter that intercepts console output, writes it to the original console, and feeds it to the Logger
        private sealed class TeeWriter : TextWriter
        {
            private readonly TextWriter _inner; // The original Console.Out
            public TeeWriter(TextWriter inner) { _inner = inner; }
            public override Encoding Encoding => _inner.Encoding;

            public override void Write(char value) { _inner.Write(value); Feed(value); }

            public override void Write(string? value)
            {
                if (value == null) return;
                _inner.Write(value);
                FeedString(value);
            }

            public override void WriteLine(string? value)
            {
                _inner.WriteLine(value);
                if (value != null) FeedString(value);
                FeedString("\n"); // Ensure the newline is processed by the logger
            }

            public override void WriteLine() { _inner.WriteLine(); FeedString("\n"); }
            public override void Flush() => _inner.Flush();
        }
    }
}