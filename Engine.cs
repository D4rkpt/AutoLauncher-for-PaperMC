using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PaperAutoUpdater
{
    public static class Engine
    {
        private static List<Regex> CompiledFilters = new List<Regex>();

        public static void VerifyLocation()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("⚙️  [SYSTEM] Verifying installation directory...");
            Console.ResetColor();

            bool hasEula = File.Exists("eula.txt");
            bool hasServerProps = File.Exists("server.properties");
            bool hasPlugins = Directory.Exists("plugins");
            bool hasVersions = Directory.Exists("versions");
            bool hasPaper = Directory.Exists(".paper");

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

        private static void PrepareFilters()
        {
            CompiledFilters.Clear();
            foreach (var filter in ConfigManager.Config.ConsoleFilters)
            {
                string regexPattern = "^" + Regex.Escape(filter).Replace("\\*", ".*") + "$";
                CompiledFilters.Add(new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
            }
        }

        private static void FilterAndPrint(string? line)
        {
            if (string.IsNullOrEmpty(line)) return;

            foreach (var regex in CompiledFilters)
            {
                if (regex.IsMatch(line)) return; 
            }
            Console.WriteLine(line);
        }

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
                            if(File.Exists(file)) 
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

            Thread.Sleep(2000); 
        }

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
                        UseShellExecute = true
                    };

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"▶️  [PRE-LAUNCH] {Path.GetFileName(fullPath)} {prog.Arguments}{(prog.WaitForExit ? " (waiting for exit)" : "")}");
                    Console.ResetColor();

                    using (var p = Process.Start(psi))
                    {
                        if (p != null && prog.WaitForExit) p.WaitForExit();
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

        public static void RunServer(string jarName = "")
        {
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
            RunPreLaunchPrograms();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"🚀 [SYSTEM] Engine Ignited. Starting the server with {jarName}...\n");
            Console.ResetColor();

            ProcessStartInfo psi = new ProcessStartInfo("java", $"{ConfigManager.Config.JavaArgs} -jar {jarName} nogui")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = false
            };

            var process = Process.Start(psi);
            if (process == null) return;

            process.OutputDataReceived += (sender, e) => FilterAndPrint(e.Data);
            process.ErrorDataReceived += (sender, e) => FilterAndPrint(e.Data);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Task.Run(() =>
            {
                while (!process.HasExited)
                {
                    string? input = Console.ReadLine();
                    if (!string.IsNullOrEmpty(input) && !process.HasExited)
                    {
                        process.StandardInput.WriteLine(input);
                    }
                }
            });

            process.WaitForExit();
            ExecutePostShutdownCleanup();
        }
    }
}