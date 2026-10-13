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
        private const int MaxLogFiles = 30; // oldest logs are removed beyond this

        private static readonly object Lock = new object();
        private static StreamWriter? _writer;
        private static readonly StringBuilder _line = new StringBuilder();

        // While true (header art, settings menus) console text is NOT written to the log
        public static bool Paused { get; set; }

        public static void Init()
        {
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), LogFolderName);
                Directory.CreateDirectory(dir);
                TrimOldLogs(dir);

                string file = Path.Combine(dir, $"launcher_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
                var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };

                LogEvent("=== AutoLauncher-for-Paper session started ===");
                AppDomain.CurrentDomain.ProcessExit += (s, e) => LogEvent("=== Session ended ===");

                // Tee: console keeps working exactly as before, and every line is also logged
                Console.SetOut(new TeeWriter(Console.Out));
            }
            catch (Exception ex)
            {
                // Logging must never stop the launcher
                _writer = null;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠️  [WARNING] Could not create log folder: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static void TrimOldLogs(string dir)
        {
            try
            {
                var old = Directory.GetFiles(dir, "launcher_*.log")
                                   .OrderByDescending(f => f, StringComparer.Ordinal)
                                   .Skip(MaxLogFiles - 1);
                foreach (var f in old) File.Delete(f);
            }
            catch { }
        }

        // Writes a line directly, even while Paused
        public static void LogEvent(string text)
        {
            lock (Lock)
            {
                try { _writer?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}"); } catch { }
            }
        }

        internal static void Feed(char c)
        {
            lock (Lock)
            {
                if (_writer == null) return;

                if (c == '\r') { _line.Clear(); return; } // countdown/progress redraws keep only the final state
                if (c != '\n') { _line.Append(c); return; }

                string text = _line.ToString().TrimEnd();
                _line.Clear();
                if (Paused || text.Length == 0) return;

                try { _writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] {text}"); } catch { }
            }
        }

        private sealed class TeeWriter : TextWriter
        {
            private readonly TextWriter _inner;
            public TeeWriter(TextWriter inner) { _inner = inner; }
            public override Encoding Encoding => _inner.Encoding;

            public override void Write(char value) { _inner.Write(value); Feed(value); }

            public override void Write(string? value)
            {
                if (value == null) return;
                _inner.Write(value);
                foreach (char c in value) Feed(c);
            }

            public override void WriteLine(string? value)
            {
                _inner.WriteLine(value);
                if (value != null) foreach (char c in value) Feed(c);
                Feed('\n');
            }

            public override void WriteLine() { _inner.WriteLine(); Feed('\n'); }
            public override void Flush() => _inner.Flush();
        }
    }
}
