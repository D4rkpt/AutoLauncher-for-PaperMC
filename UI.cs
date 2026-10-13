using System;
using System.Threading;

namespace PaperAutoUpdater
{
    public static class UI
    {
        public static void PrintHeader()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(new string('═', 75));
            Console.WriteLine(@"   ___                          __                        _                ");
            Console.WriteLine(@"  / _ \__ _ _ __   ___ _ __    / /  __ _ _   _ _ __   ___| |__   ___ _ __  ");
            Console.WriteLine(@" / /_)/ _` | '_ \ / _ \ '__|  / /  / _` | | | | '_ \ / __| '_ \ / _ \ '__| ");
            Console.WriteLine(@"/ ___/ (_| | |_) |  __/ |    / /__| (_| | |_| | | | | (__| | | |  __/ |    ");
            Console.WriteLine(@"\/    \__,_| .__/ \___|_|    \____/\__,_|\__,_|_| |_|\___|_| |_|\___|_|    ");
            Console.WriteLine(@"           |_|                                                             ");
            Console.WriteLine(new string('═', 75));
            Console.ResetColor();
        }

        public static void RunSetupWizard()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("✨ [SETUP WIZARD] Welcome! First-time execution detected.");
            Console.WriteLine("✨ [SETUP WIZARD] Let's configure your server environment.\n");
            Console.ResetColor();

            // Auto-Update Setup
            Console.Write("🔄 Do you want to enable Auto-Updates on startup? (Y/N): ");
            var key = Console.ReadKey().Key;
            ConfigManager.Config.AutoUpdate = (key == ConsoleKey.Y);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(ConfigManager.Config.AutoUpdate ? "\n-> Auto-Updates: ENABLED\n" : "\n-> Auto-Updates: DISABLED\n");
            Console.ResetColor();

            // Boot Delay Setup
            Console.Write("⏱️  Enter boot delay in seconds before server launches (e.g., 5): ");
            if (int.TryParse(Console.ReadLine(), out int delay) && delay >= 0)
            {
                ConfigManager.Config.BootDelaySeconds = delay;
                if (delay == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n⚠️  [WARNING] You set the delay to 0. You will NOT be able to interrupt the boot process to open the menu.");
                    Console.WriteLine("To change settings in the future, you must manually edit the 'launcher_config.json' file.");
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ResetColor();
                    Console.ReadKey();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n⚠️  [WARNING] Invalid input. Defaulting to 5 seconds.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ResetColor();
                ConfigManager.Config.BootDelaySeconds = 5;
                Console.ReadKey();
            }

            // Java Arguments Setup
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n✨ [SETUP WIZARD] Default Java Arguments (Aikar's Flags) are highly recommended.");
            Console.ResetColor();
            Console.WriteLine("Current Default: " + ConfigManager.Config.JavaArgs);
            Console.Write("\n☕ Do you want to use the default arguments? (Y/N): ");
            var argsKey = Console.ReadKey().Key;

            if (argsKey == ConsoleKey.N)
            {
                Console.WriteLine("\n\nEnter your custom Java arguments (including RAM allocation, Velocity flags, etc):");
                Console.Write("> ");
                string? customArgs = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(customArgs))
                {
                    ConfigManager.Config.JavaArgs = customArgs;
                }
            }

            // Auto-detect currently installed version on first run to avoid asking the user
            string? detectedVersion = Updater.GetInstalledVersion();
            if (!string.IsNullOrEmpty(detectedVersion))
            {
                ConfigManager.Config.TargetVersion = detectedVersion;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\\n🌐 [SETUP WIZARD] Automatically detected currently installed version: {detectedVersion}.");
                Console.ResetColor();
            }
            else
            {
                // Fallback to a common recent version if the folder is completely empty/fresh
                ConfigManager.Config.TargetVersion = "1.21.1";
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\\n⚠️  [SETUP WIZARD] No existing version detected. Defaulting Target Version to 1.21.1.");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\\n✅ [SETUP WIZARD] Initial configuration is complete. All systems nominal.");
            Console.ResetColor();

            ConfigManager.SaveConfigInstant();
            Thread.Sleep(3000); // Give user time to read the auto-detection message
            Console.Clear();
            PrintHeader();
        }

        public static bool StartLaunchCountdown()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"⚙️  [CONFIG] Target Version: {ConfigManager.Config.TargetVersion}");
            Console.WriteLine($"⚙️  [CONFIG] Auto-Update: {(ConfigManager.Config.AutoUpdate ? "ON" : "OFF")}");
            Console.WriteLine($"⚙️  [CONFIG] Boot Delay: {ConfigManager.Config.BootDelaySeconds} second(s)");
            Console.WriteLine($"⚙️  [CONFIG] Active Filters: {ConfigManager.Config.ConsoleFilters.Count}");
            Console.WriteLine($"⚙️  [CONFIG] Cleanup Files: {ConfigManager.Config.PostShutdownDeletes.Count}");
            Console.WriteLine($"⚙️  [CONFIG] Java Args: {ConfigManager.Config.JavaArgs.Substring(0, Math.Min(ConfigManager.Config.JavaArgs.Length, 40))}...\n");
            Console.ResetColor();

            if (ConfigManager.Config.BootDelaySeconds == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  [WARNING] Boot delay is 0. Launching instantly.");
                Console.WriteLine("⚠️  [WARNING] To edit settings, modify 'launcher_config.json' manually.");
                Console.ResetColor();
                return false; // Cannot be interrupted
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("👉 [ACTION] Press 'C' to interrupt boot sequence and open Settings.");
            Console.ResetColor();

            for (int i = ConfigManager.Config.BootDelaySeconds; i > 0; i--)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\r🚀 [BOOT] Launching server in {i} second(s)... ");
                Console.ResetColor();

                DateTime start = DateTime.Now;
                while ((DateTime.Now - start).TotalSeconds < 1)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true);
                        if (key.Key == ConsoleKey.C)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("\n\n⚠️  [SYSTEM] Boot sequence aborted by user.");
                            Console.ResetColor();
                            return true; // Boot interrupted
                        }
                    }
                    Thread.Sleep(50);
                }
            }
            Console.WriteLine("\n");
            return false;
        }

        public static void OpenConfigurationMenu()
        {
            while (true)
            {
                Console.Clear();
                PrintHeader();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                    CONFIGURATION MENU                       ║");
                Console.WriteLine("╚═════════════════════════════════════════════════════════════╝");
                Console.ResetColor();

                Console.WriteLine($"  1. 🎯 Change Target Version (Current: {ConfigManager.Config.TargetVersion})");
                Console.WriteLine($"  2. 🔄 Toggle Auto-Update (Current: {(ConfigManager.Config.AutoUpdate ? "ON" : "OFF")})");
                Console.WriteLine($"  3. ⏱️ Change Boot Delay  (Current: {ConfigManager.Config.BootDelaySeconds}s)");
                Console.WriteLine("  4. ☕ Edit Java Arguments & RAM");
                Console.WriteLine($"  5. 🛡️ Manage Console Filters ({ConfigManager.Config.ConsoleFilters.Count} active)");
                Console.WriteLine($"  6. 🧹 Manage Post-Shutdown File Cleanup ({ConfigManager.Config.PostShutdownDeletes.Count} active)");
                Console.WriteLine("  7. 🚀 Launch Server");
                Console.WriteLine("  8. ❌ Exit Launcher");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\n👉 Select an option (1-8): ");
                Console.ResetColor();

                var key = Console.ReadKey(intercept: true).KeyChar;

                switch (key)
                {
                    case '1':
                        Console.Clear();
                        PrintHeader();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\\n=== 🎯 CHANGE TARGET VERSION ===");
                        Console.ResetColor();
                        Console.WriteLine("Type the exact Minecraft version (e.g., '1.20.4', '1.21.1') you want to target.");
                        Console.WriteLine($"Current Target: {ConfigManager.Config.TargetVersion}");
                        Console.Write("\\nEnter target version: ");

                        string? inputVersion = Console.ReadLine()?.Trim();

                        if (string.IsNullOrWhiteSpace(inputVersion)) break;

                        // Removed the 'recent' logic block completely. Straight to validation.
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"\\n🌐 [NETWORK] Validating version '{inputVersion}' with PaperMC API...");
                            Console.ResetColor();

                            // Validate with API synchronously to block UI and extract channel metadata
                            var (isValid, channel) = Updater.ValidateVersionAndGetChannelAsync(inputVersion).GetAwaiter().GetResult();

                            if (!isValid)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"❌ [ERROR] Version '{inputVersion}' does not exist on PaperMC or the API is unreachable.");
                                Console.WriteLine("Please check for typos (e.g., use '1.21.1' instead of '1.21').");
                                Console.WriteLine("\\nPress any key to continue...");
                                Console.ResetColor();
                                Console.ReadKey();
                            }
                            else
                            {
                                // Detect currently installed version to compare
                                string? currentInstalledVersion = Updater.GetInstalledVersion();
                                bool isDowngrade = false;
                                bool isUpgrade = false;

                                if (!string.IsNullOrEmpty(currentInstalledVersion) && Version.TryParse(inputVersion, out Version? targetVer) && Version.TryParse(currentInstalledVersion, out Version? currentVer))
                                {
                                    if (targetVer < currentVer) isDowngrade = true;
                                    else if (targetVer > currentVer) isUpgrade = true;
                                }

                                if (isDowngrade)
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"\\n⚠️  [CRITICAL DOWNGRADE WARNING] ⚠️");
                                    Console.WriteLine($"You are downgrading from {currentInstalledVersion} to {inputVersion}.");
                                    Console.WriteLine("- DOWNGRADING WILL CORRUPT YOUR EXISTING WORLDS.");
                                    Console.WriteLine("- Minecraft does NOT support converting worlds backwards.");
                                    Console.WriteLine("- You MUST wipe your world folder or restore a backup before starting.");
                                    Console.ResetColor();
                                }
                                else if (isUpgrade)
                                {
                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    Console.WriteLine($"\\n✨ [UPGRADE DETECTED] Upgrading from {currentInstalledVersion} to {inputVersion}.");
                                    Console.WriteLine("- New Minecraft versions may break outdated plugins.");
                                    Console.WriteLine("- It is highly recommended to create a backup before proceeding.");
                                    Console.ResetColor();
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"\\n✅ [INFO] You are already on version {inputVersion}.");
                                    Console.WriteLine("Applying this target will force the launcher to stick to this version.");
                                    Console.ResetColor();
                                }

                                // Only show Alpha/Beta warnings for Upgrades or same-version targets
                                if (!isDowngrade && channel != "DEFAULT" && channel != "STABLE")
                                {
                                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                                    Console.WriteLine($"\\n⚠️  [EXPERIMENTAL BUILD WARNING] ⚠️");
                                    Console.WriteLine($"Version {inputVersion} is currently marked as {channel}.");
                                    Console.WriteLine("- Experimental builds contain bleeding-edge features but may have severe bugs.");
                                    Console.WriteLine("- NEVER use experimental builds on a production server without backups.");
                                    Console.ResetColor();
                                }

                                Console.Write("\\nAre you absolutely sure you want to apply this target version? (Y/N): ");
                                var confirmKey = Console.ReadKey().Key;

                                if (confirmKey == ConsoleKey.Y)
                                {
                                    ConfigManager.Config.TargetVersion = inputVersion;
                                    Console.WriteLine();
                                    ConfigManager.SaveConfigInstant();
                                }
                                Thread.Sleep(1000);
                            }
                        }
                        break;

                    case '2':
                        ConfigManager.Config.AutoUpdate = !ConfigManager.Config.AutoUpdate;
                        ConfigManager.SaveConfigInstant();
                        break;

                    case '3':
                        Console.Clear();
                        PrintHeader();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\n=== ⏱️  CHANGE BOOT DELAY ===");
                        Console.ResetColor();
                        Console.WriteLine($"Current Delay: {ConfigManager.Config.BootDelaySeconds} seconds");
                        Console.Write("Enter new delay in seconds: ");
                        if (int.TryParse(Console.ReadLine(), out int newDelay) && newDelay >= 0)
                        {
                            ConfigManager.Config.BootDelaySeconds = newDelay;
                            if (newDelay == 0)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("\n⚠️  [WARNING] Delay set to 0. You will NOT be able to open this menu again via UI.");
                                Console.WriteLine("You will have to edit 'launcher_config.json' directly.");
                                Console.WriteLine("\nPress any key to continue...");
                                Console.ResetColor();
                                Console.ReadKey();
                            }
                            ConfigManager.SaveConfigInstant();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\n❌ [ERROR] Invalid input. Must be a positive number.");
                            Console.WriteLine("\nPress any key to continue...");
                            Console.ResetColor();
                            Console.ReadKey();
                        }
                        break;

                    case '4':
                        Console.Clear();
                        PrintHeader();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\n=== ☕ EDIT JAVA ARGUMENTS ===");
                        Console.ResetColor();
                        Console.WriteLine("Current: " + ConfigManager.Config.JavaArgs);
                        Console.WriteLine("\nEnter new arguments (or press Enter to cancel):");
                        Console.Write("> ");
                        string? newArgs = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(newArgs))
                        {
                            ConfigManager.Config.JavaArgs = newArgs;
                            ConfigManager.SaveConfigInstant();
                        }
                        Thread.Sleep(1000);
                        break;

                    case '5':
                        ManageFiltersMenu();
                        break;

                    case '6':
                        ManageCleanupMenu();
                        break;

                    case '7':
                        Console.Clear();
                        Thread.Sleep(100);
                        PrintHeader();
                        Updater.RunUpdateAndLaunchSequence().GetAwaiter().GetResult();
                        return;

                    case '8':
                        Environment.Exit(0);
                        break;
                }
            }
        }

        static void ManageFiltersMenu()
        {
            while (true)
            {
                Console.Clear();
                PrintHeader();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n=== 🛡️  CONSOLE FILTERS ===");
                Console.ResetColor();
                Console.WriteLine("Filters hide specific messages from the console.");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n--- Active Filters ---");
                Console.ResetColor();

                if (ConfigManager.Config.ConsoleFilters.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("   (No filters applied)");
                    Console.ResetColor();
                }
                else
                {
                    for (int i = 0; i < ConfigManager.Config.ConsoleFilters.Count; i++)
                    {
                        Console.WriteLine($"   {i + 1}. {ConfigManager.Config.ConsoleFilters[i]}");
                    }
                }

                Console.WriteLine("\n----------------------");
                Console.WriteLine("  1. ➕ Add Filter");
                Console.WriteLine("  2. ➖ Remove Filter");
                Console.WriteLine("  3. 🔙 Back to Main Menu");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\n👉 Select an option (1-3): ");
                Console.ResetColor();

                var key = Console.ReadKey(intercept: true).KeyChar;

                switch (key)
                {
                    case '1':
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n\n[ ADD NEW FILTER ]");
                        Console.ResetColor();
                        Console.WriteLine("How to use patterns (The '*' wildcard means 'anything'):");
                        Console.WriteLine("  *INFO*       -> Hides ANY line containing 'INFO'");
                        Console.WriteLine("  * joined     -> Hides lines ending with ' joined' (e.g., 'Notch joined')");
                        Console.WriteLine("  Exact Text   -> Hides lines matching exactly 'Exact Text' and nothing else");
                        Console.Write("\nEnter pattern to filter: ");

                        string? newFilter = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(newFilter) && !ConfigManager.Config.ConsoleFilters.Contains(newFilter))
                        {
                            ConfigManager.Config.ConsoleFilters.Add(newFilter);
                            ConfigManager.SaveConfigInstant();
                        }
                        break;

                    case '2':
                        if (ConfigManager.Config.ConsoleFilters.Count == 0) break;
                        Console.Write($"\n\nEnter filter number to remove (1-{ConfigManager.Config.ConsoleFilters.Count}): ");
                        if (int.TryParse(Console.ReadLine(), out int idx) && idx >= 1 && idx <= ConfigManager.Config.ConsoleFilters.Count)
                        {
                            ConfigManager.Config.ConsoleFilters.RemoveAt(idx - 1);
                            ConfigManager.SaveConfigInstant();
                        }
                        break;

                    case '3':
                        return;
                }
            }
        }

        static void ManageCleanupMenu()
        {
            while (true)
            {
                Console.Clear();
                PrintHeader();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n=== 🧹 FILE CLEANUP MANAGER ===");
                Console.ResetColor();
                Console.WriteLine("Files/patterns added here will be automatically deleted every time the server shuts down.");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n--- Active Cleanup Tasks ---");
                Console.ResetColor();

                if (ConfigManager.Config.PostShutdownDeletes.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("   (No cleanup tasks applied)");
                    Console.ResetColor();
                }
                else
                {
                    for (int i = 0; i < ConfigManager.Config.PostShutdownDeletes.Count; i++)
                    {
                        Console.WriteLine($"   {i + 1}. {ConfigManager.Config.PostShutdownDeletes[i]}");
                    }
                }

                Console.WriteLine("\n----------------------");
                Console.WriteLine("  1. ➕ Add File/Pattern to Delete");
                Console.WriteLine("  2. ➖ Remove Task");
                Console.WriteLine("  3. 🔙 Back to Main Menu");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\n👉 Select an option (1-3): ");
                Console.ResetColor();

                var key = Console.ReadKey(intercept: true).KeyChar;

                switch (key)
                {
                    case '1':
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n\n[ ADD CLEANUP TASK ]");
                        Console.ResetColor();
                        Console.WriteLine("How to use patterns (The '*' wildcard means 'anything'):");
                        Console.WriteLine("  ops.json    -> Deletes only the exact file 'ops.json'");
                        Console.WriteLine("  *.log       -> Deletes ALL files ending with '.log' in the main folder");
                        Console.WriteLine("  logs\\*.gz   -> Deletes ALL '.gz' files inside the 'logs' folder");
                        Console.Write("\nEnter file name or pattern to delete on shutdown: ");

                        string? newCleanup = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(newCleanup) && !ConfigManager.Config.PostShutdownDeletes.Contains(newCleanup))
                        {
                            ConfigManager.Config.PostShutdownDeletes.Add(newCleanup);
                            ConfigManager.SaveConfigInstant();
                        }
                        break;

                    case '2':
                        if (ConfigManager.Config.PostShutdownDeletes.Count == 0) break;
                        Console.Write($"\n\nEnter task number to remove (1-{ConfigManager.Config.PostShutdownDeletes.Count}): ");
                        if (int.TryParse(Console.ReadLine(), out int idx) && idx >= 1 && idx <= ConfigManager.Config.PostShutdownDeletes.Count)
                        {
                            ConfigManager.Config.PostShutdownDeletes.RemoveAt(idx - 1);
                            ConfigManager.SaveConfigInstant();
                        }
                        break;

                    case '3':
                        return;
                }
            }
        }
    }
}