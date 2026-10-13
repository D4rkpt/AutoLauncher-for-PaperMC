using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PaperAutoUpdater
{
    // A program (.exe, .bat, .cmd, ...) started before the server boots
    public class PreLaunchProgram
    {
        public string Path { get; set; } = "";
        public string Arguments { get; set; } = "";
        // true = wait for the program to exit before continuing; false = start it and move on
        public bool WaitForExit { get; set; } = false;
    }

    // Configuration class loaded directly into RAM for ultra-fast read/write operations
    public class LauncherConfig
    {
        public string TargetVersion { get; set; } = "";
        public bool AutoUpdate { get; set; } = true;
        public int BootDelaySeconds { get; set; } = 5;
        public string JavaArgs { get; set; } = "-Xms16384M -Xmx16384M -XX:+DisableExplicitGC -XX:+ParallelRefProcEnabled -XX:+PerfDisableSharedMem -XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1HeapRegionSize=8M -XX:G1HeapWastePercent=5 -XX:G1MaxNewSizePercent=40 -XX:G1MixedGCCountTarget=4 -XX:G1MixedGCLiveThresholdPercent=90 -XX:G1NewSizePercent=30 -XX:G1RSetUpdatingPauseTimePercent=5 -XX:G1ReservePercent=20 -XX:InitiatingHeapOccupancyPercent=15 -XX:MaxGCPauseMillis=200 -XX:MaxTenuringThreshold=1 -XX:SurvivorRatio=32 -Dusing.aikars.flags=https://mcflags.emc.gs -Dlog4j2.configurationFile=log4j2.xml -Daikars.new.flags=true";

        // List of active console filters
        public List<string> ConsoleFilters { get; set; } = new List<string>();

        // Programs started (in order) right before the server launches
        public List<PreLaunchProgram> PreLaunchPrograms { get; set; } = new List<PreLaunchProgram>();

        // List of files/patterns to delete after the server shuts down
        public List<string> PostShutdownDeletes { get; set; } = new List<string>();
    }

    public static class ConfigManager
    {
        public const string ConfigFileName = "launcher_config.json";
        public static LauncherConfig Config { get; set; } = new LauncherConfig();

        // OPTIMIZATION: Cached options prevent CPU allocation overhead on every instant save
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public static void LoadConfig()
        {
            try
            {
                string json = File.ReadAllText(ConfigFileName);
                Config = JsonSerializer.Deserialize<LauncherConfig>(json) ?? new LauncherConfig();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("⚙️  [SYSTEM] Configuration loaded into RAM.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠️  [WARNING] Failed to load config: {ex.Message}. Using defaults.");
                Console.ResetColor();
            }
        }

        // Instant save method (No artificial delays)
        public static void SaveConfigInstant()
        {
            try
            {
                string json = JsonSerializer.Serialize(Config, JsonOptions);
                File.WriteAllText(ConfigFileName, json);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ [ERROR] Failed to save configuration: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}