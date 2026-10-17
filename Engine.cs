using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PaperAutoUpdater
{
    // The core execution engine responsible for validating the environment and running external processes
    public static class Engine
    {
        private static readonly List<Regex> CompiledFilters = new List<Regex>(); // Stores the regex patterns for console filtering

        // Synchronization lock for thread-safe console cursor manipulation
        private static readonly object ConsoleLock = new object();

        // Pre-compiled Regex patterns for TUI state interception
        private static readonly Regex DonePattern = new Regex(@"Done \([0-9.,]+s\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex JoinPattern = new Regex(@"\bjoined the game\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex LeavePattern = new Regex(@"\bleft the game\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Real-time TUI state variables
        private const int DashboardHeight = 3;
        private static volatile bool _tuiActive = false;
        private static volatile bool _serverOnline = false;
        private static volatile int _onlinePlayers = 0;
        private static int _maxPlayers = 20;
        private static DateTime _bootStartTime;
        private static int _lastDrawnTop = -1;
        private static int _lastWindowWidth = -1;

        // Verifies if the executable is running in a valid Minecraft server root directory
        public static void VerifyLocation()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("⚙️  [SYSTEM] Verifying installation directory...");
            Console.ResetColor();

            // Checks for common Minecraft server files/folders
            bool hasEula = File.Exists("eula.txt");
            bool hasServerProps = File.Exists("server.properties");
            bool hasPlugins = Directory.Exists("plugins");
            bool hasVersions = Directory.Exists("versions");
            bool hasPaper = Directory.Exists(".paper");

            // If none of these exist, the user probably double-clicked the .exe in their Downloads folder
            if (!hasEula && !hasServerProps && !hasPlugins && !hasVersions && !hasPaper)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ [CRITICAL ERROR] Execution Halted.");
                Console.WriteLine("The launcher executable is not placed in a valid Minecraft server directory.");
                Console.WriteLine("Please move this .exe to your main server folder (where eula.txt or plugins/ are).");
                Console.WriteLine("\nPress any key to terminate the process...");
                Console.ResetColor();
                Console.ReadKey();
                Environment.Exit(1);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ [SYSTEM] Directory verified successfully.\n");
            Console.ResetColor();
        }

        // Compiles wildcard strings from the configuration into Regex objects for faster evaluation during server runtime
        private static void PrepareFilters()
        {
            CompiledFilters.Clear();
            foreach (var filter in ConfigManager.Config.ConsoleFilters)
            {
                // Converts simple wildcards (*) into valid Regex (.*)
                string regexPattern = "^" + Regex.Escape(filter).Replace("\\*", ".*") + "$";
                CompiledFilters.Add(new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
            }
        }

        // Reads max-players from server.properties if available
        private static void LoadMaxPlayersFromProperties()
        {
            _maxPlayers = 20;
            if (!File.Exists("server.properties")) return;

            try
            {
                foreach (string line in File.ReadAllLines("server.properties"))
                {
                    if (line.StartsWith("max-players=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(line.Substring(12).Trim(), out int max) && max > 0)
                            _maxPlayers = max;
                        break;
                    }
                }
            }
            catch { }
        }

        // Intercepts server output lines to update dashboard metrics and prints non-filtered lines safely above the TUI
        private static void FilterAndPrint(string? line)
        {
            if (string.IsNullOrEmpty(line)) return;

            // Update dashboard state from server logs before filtering
            if (!_serverOnline && DonePattern.IsMatch(line))
            {
                _serverOnline = true;
            }
            else if (JoinPattern.IsMatch(line))
            {
                Interlocked.Increment(ref _onlinePlayers);
            }
            else if (LeavePattern.IsMatch(line))
            {
                int current = Interlocked.Decrement(ref _onlinePlayers);
                if (current < 0) Interlocked.Exchange(ref _onlinePlayers, 0);
            }

            foreach (var regex in CompiledFilters)
            {
                if (regex.IsMatch(line)) return; // Line matched a filter, drop it
            }

            lock (ConsoleLock)
            {
                if (!_tuiActive)
                {
                    Console.WriteLine(line);
                    return;
                }

                // 1. Erase existing dashboard before printing so it never duplicates in scrollback
                ClearDashboardRegionInternal();

                // 2. Ensure the scrollable log region respects the bottom margin
                int winHeight = Math.Max(10, Console.WindowHeight);
                int dashboardTop = Console.WindowTop + winHeight - DashboardHeight;

                if (Console.CursorTop >= dashboardTop - 1)
                {
                    bool wasPaused = Logger.Paused;
                    Logger.Paused = true;

                    // Push buffer up to maintain bottom margin for the TUI
                    int linesToPush = (Console.CursorTop - (dashboardTop - 2));
                    for (int i = 0; i < linesToPush; i++)
                    {
                        Console.SetCursorPosition(0, Console.WindowTop + winHeight - 1);
                        Console.WriteLine();
                    }

                    dashboardTop = Console.WindowTop + winHeight - DashboardHeight;
                    Console.SetCursorPosition(0, Math.Max(0, dashboardTop - 2));
                    Logger.Paused = wasPaused;
                }

                // 3. Print the actual server log line (captured by Logger)
                Console.WriteLine(line);

                // 4. Redraw dashboard anchored at the bottom and restore cursor
                DrawDashboardInternal();
            }
        }

        // Clears the previously drawn dashboard lines inside a locked context
        private static void ClearDashboardRegionInternal()
        {
            if (_lastDrawnTop < 0) return;

            bool wasPaused = Logger.Paused;
            Logger.Paused = true;

            try
            {
                int savedLeft = Console.CursorLeft;
                int savedTop = Console.CursorTop;

                // Clear only up to the current safe window width to prevent auto-wrap bugs
                int safeWidth = Math.Max(10, Console.WindowWidth - 1);
                string blank = new string(' ', safeWidth);

                for (int i = 0; i < DashboardHeight; i++)
                {
                    int row = _lastDrawnTop + i;
                    if (row >= 0 && row < Console.BufferHeight)
                    {
                        Console.SetCursorPosition(0, row);
                        Console.Write(blank);
                    }
                }

                Console.SetCursorPosition(savedLeft, savedTop);
            }
            catch { }
            finally
            {
                Logger.Paused = wasPaused;
            }
        }

        // Renders the 3-line ASCII dashboard at the bottom of the console window inside a locked context
        private static void DrawDashboardInternal()
        {
            if (!_tuiActive) return;

            bool wasPaused = Logger.Paused;
            Logger.Paused = true; // Never write TUI frames into the session log file

            try
            {
                int winWidth = Console.WindowWidth;
                int winHeight = Console.WindowHeight;

                // Prevent crashes if the window is minimized or shrunk to extreme bounds
                if (winWidth < 20 || winHeight < 5) return;

                int targetTop = Console.WindowTop + winHeight - DashboardHeight;

                if (targetTop + DashboardHeight > Console.BufferHeight)
                    return;

                int savedLeft = Console.CursorLeft;
                int savedTop = Console.CursorTop;
                ConsoleColor savedColor = Console.ForegroundColor;

                if (savedTop >= targetTop)
                {
                    savedTop = Math.Max(0, targetTop - 1);
                }

                // The magic here: use the maximum safe width (winWidth - 1) and force strings 
                // to EXACTLY that size to overwrite any OS auto-wrap ghost characters.
                int maxSafeWidth = winWidth - 1;
                int innerWidth = maxSafeWidth - 2; // -2 to account for the left and right borders ║

                string topBorder = "╔" + new string('═', innerWidth) + "╗";
                string bottomBorder = "╚" + new string('═', innerWidth) + "╝";

                TimeSpan uptime = DateTime.Now - _bootStartTime;
                string statusText = _serverOnline ? "ONLINE" : "STARTING";
                string targetVer = string.IsNullOrEmpty(ConfigManager.Config.TargetVersion) ? "AUTO" : ConfigManager.Config.TargetVersion;

                string content = $" STATUS: {statusText,-8} | UPTIME: {uptime:hh\\:mm\\:ss} | PLAYERS: {_onlinePlayers}/{_maxPlayers} | TARGET: {targetVer}";

                if (content.Length > innerWidth)
                    content = content.Substring(0, innerWidth);
                else
                    content = content.PadRight(innerWidth); // Pad with spaces until it hits the right border

                string midLine = "║" + content + "║";

                Console.ForegroundColor = _serverOnline ? ConsoleColor.Green : ConsoleColor.Yellow;

                // Draw the perfectly aligned lines over the previous buffer
                Console.SetCursorPosition(0, targetTop);
                Console.Write(topBorder);

                Console.SetCursorPosition(0, targetTop + 1);
                Console.Write(midLine);

                Console.SetCursorPosition(0, targetTop + 2);
                Console.Write(bottomBorder);

                _lastDrawnTop = targetTop;
                _lastWindowWidth = winWidth;

                Console.ForegroundColor = savedColor;
                Console.SetCursorPosition(savedLeft, savedTop);
            }
            catch { }
            finally
            {
                Logger.Paused = wasPaused;
            }
        }

        // Deletes specified files or patterns after the server process has fully terminated
        private static void ExecutePostShutdownCleanup()
        {
            if (ConfigManager.Config.PostShutdownDeletes.Count == 0) return;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n🧹 [SYSTEM] Executing post-shutdown file cleanup...");
            Console.ResetColor();

            string baseDir = Directory.GetCurrentDirectory();

            foreach (var pattern in ConfigManager.Config.PostShutdownDeletes)
            {
                try
                {
                    string directoryToSearch = baseDir;
                    string searchPattern = pattern;

                    // Support for targeting files inside subdirectories (e.g., "logs\*.gz")
                    if (pattern.Contains("\\") || pattern.Contains("/"))
                    {
                        string normalizedPattern = pattern.Replace("/", "\\");
                        string directoryPart = Path.GetDirectoryName(normalizedPattern) ?? "";
                        searchPattern = Path.GetFileName(normalizedPattern);
                        directoryToSearch = Path.Combine(baseDir, directoryPart);
                    }

                    if (!Directory.Exists(directoryToSearch)) continue;

                    string[] filesToDelete = Directory.GetFiles(directoryToSearch, searchPattern);
                    foreach (var file in filesToDelete)
                    {
                        try
                        {
                            if (File.Exists(file))
                            {
                                File.Delete(file);
                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                Console.WriteLine($"🗑️  [CLEANUP] Deleted: {Path.GetFileName(file)}");
                                Console.ResetColor();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"❌ [CLEANUP ERROR] Could not delete '{Path.GetFileName(file)}': {ex.Message}");
                            Console.ResetColor();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ [CLEANUP ERROR] Failed processing pattern '{pattern}': {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ [SYSTEM] Cleanup complete.\n");
            Console.ResetColor();

            Thread.Sleep(2000); // Small delay to let the user see the cleanup results
        }

        // Executes external programs sequentially before starting the Minecraft server
        private static void RunPreLaunchPrograms()
        {
            var programs = ConfigManager.Config.PreLaunchPrograms;
            if (programs == null || programs.Count == 0) return;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🧩 [SYSTEM] Starting {programs.Count} pre-launch program(s)...");
            Console.ResetColor();

            string baseDir = Directory.GetCurrentDirectory();

            foreach (var prog in programs)
            {
                if (string.IsNullOrWhiteSpace(prog.Path)) continue;

                try
                {
                    // Relative paths are resolved against the server folder
                    string fullPath = Path.IsPathRooted(prog.Path) ? prog.Path : Path.Combine(baseDir, prog.Path);

                    if (!File.Exists(fullPath))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ [PRE-LAUNCH ERROR] File not found: {fullPath}");
                        Console.ResetColor();
                        continue;
                    }

                    var psi = new ProcessStartInfo(fullPath)
                    {
                        Arguments = prog.Arguments ?? "",
                        WorkingDirectory = Path.GetDirectoryName(fullPath) ?? baseDir,
                        UseShellExecute = true // Required to launch .bat or UI-based applications properly
                    };

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"▶️  [PRE-LAUNCH] {Path.GetFileName(fullPath)} {prog.Arguments}{(prog.WaitForExit ? " (waiting for exit)" : "")}");
                    Console.ResetColor();

                    using (var p = Process.Start(psi))
                    {
                        if (p != null && prog.WaitForExit) p.WaitForExit(); // Blocks the thread if WaitForExit is enabled
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ [PRE-LAUNCH ERROR] Could not start '{prog.Path}': {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ [SYSTEM] Pre-launch programs done.\n");
            Console.ResetColor();
        }

        // Starts the Minecraft server Java process and handles standard I/O redirection
        public static void RunServer(string jarName = "")
        {
            // If no specific jar was provided (e.g., auto-update is off), find the first paper jar available
            if (string.IsNullOrEmpty(jarName))
            {
                var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "paper-*.jar");
                if (files.Length > 0) jarName = Path.GetFileName(files[0]);
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ [FATAL ERROR] No Paper executable found to start.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ResetColor();
                    Console.ReadKey();
                    return;
                }
            }

            PrepareFilters();
            LoadMaxPlayersFromProperties();
            RunPreLaunchPrograms();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"🚀 [SYSTEM] Engine Ignited. Starting the server with {jarName}...\n");
            Console.ResetColor();

            // Prepare the Java process configuration
            ProcessStartInfo psi = new ProcessStartInfo("java", $"{ConfigManager.Config.JavaArgs} -jar {jarName} nogui")
            {
                UseShellExecute = false, // Required for I/O redirection
                RedirectStandardOutput = true, // Capture server logs
                RedirectStandardError = true,  // Capture server errors
                RedirectStandardInput = true,  // Allow sending commands to the server
                CreateNoWindow = false
            };

            Process? process;
            try
            {
                process = Process.Start(psi);
                if (process == null) return;
            }
            catch (Exception ex)
            {
                // This usually happens if Java is not installed or not in the Windows PATH
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ [FATAL ERROR] Failed to start Java.");
                Console.WriteLine($"Is Java installed and added to the Windows PATH environment variables?");
                Console.WriteLine($"Details: {ex.Message}");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            // Initialize Bottom TUI state
            _serverOnline = false;
            _onlinePlayers = 0;
            _bootStartTime = DateTime.Now;
            _lastDrawnTop = -1;
            _tuiActive = true;

            try { Console.CursorVisible = false; } catch { }

            lock (ConsoleLock)
            {
                DrawDashboardInternal();
            }

            // Start 1Hz (1 update per second) background task for the bottom dashboard
            using CancellationTokenSource tuiCts = new CancellationTokenSource();
            CancellationToken token = tuiCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(1000, token);
                        lock (ConsoleLock)
                        {
                            DrawDashboardInternal();
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }, token);

            // Hook up the output and error streams to our filtering method asynchronously
            process.OutputDataReceived += (sender, e) => FilterAndPrint(e.Data);
            process.ErrorDataReceived += (sender, e) => FilterAndPrint(e.Data);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Background task to read user input from the console and forward it to the server process
            Task.Run(() =>
            {
                while (!process.HasExited)
                {
                    string? input = Console.ReadLine(); // Blocks waiting for user input
                    if (!string.IsNullOrEmpty(input))
                    {
                        try
                        {
                            // Ensure we do not write to a closed pipeline
                            if (!process.HasExited)
                            {
                                process.StandardInput.WriteLine(input); // Send command to the Minecraft server
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            // Silence the error if the process closes exactly during write
                            break;
                        }
                    }
                }
            });

            process.WaitForExit(); // Block the main thread until the Minecraft server shuts down

            // Shutdown TUI cleanly and restore standard console behavior
            tuiCts.Cancel();
            lock (ConsoleLock)
            {
                ClearDashboardRegionInternal();
                _tuiActive = false;
                try { Console.CursorVisible = true; } catch { }
            }

            ExecutePostShutdownCleanup(); // Run cleanup tasks after shutdown
        }
    }
}