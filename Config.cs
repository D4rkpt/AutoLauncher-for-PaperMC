using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PaperAutoUpdater
{
    // Represents a program (.exe, .bat, .cmd, ...) executed before the server boots
    public class PreLaunchProgram
    {
        public string Path { get; set; } = "";
        public string Arguments { get; set; } = "";
        // true = execution will block until the program finishes; false = fire and forget
        public bool WaitForExit { get; set; } = false;
    }

    // Configuration class loaded directly into RAM for ultra-fast read/write operations
    // This defines the structure of the 'launcher_config.json' file
    public class LauncherConfig
    {
        public string TargetVersion { get; set; } = "";
        public bool AutoUpdate { get; set; } = true;
        public bool EnableLogs { get; set; } = true;
        public int BootDelaySeconds { get; set; } = 5; // Countdown before launch, gives time to interrupt via UI

        // Active JVM Garbage Collection profile name
        public string JavaProfile { get; set; } = "Aikar (G1GC - Balanced)";

        // Default flags optimize garbage collection (G1GC) for Minecraft servers
        public string JavaArgs { get; set; } = JavaProfiles.AikarFlags;

        // List of active console filters (wildcards matching lines to hide from output)
        public List<string> ConsoleFilters { get; set; } = new List<string>();

        // Programs started (in order) right before the server launches
        public List<PreLaunchProgram> PreLaunchPrograms { get; set; } = new List<PreLaunchProgram>();

        // List of files/patterns to delete automatically after the server shuts down
        public List<string> PostShutdownDeletes { get; set; } = new List<string>();
    }

    // Predefined JVM Garbage Collection profiles for different hardware tiers
    public static class JavaProfiles
    {
        public const string AikarFlags = "-Xms16384M -Xmx16384M -XX:+DisableExplicitGC -XX:+ParallelRefProcEnabled -XX:+PerfDisableSharedMem -XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1HeapRegionSize=8M -XX:G1HeapWastePercent=5 -XX:G1MaxNewSizePercent=40 -XX:G1MixedGCCountTarget=4 -XX:G1MixedGCLiveThresholdPercent=90 -XX:G1NewSizePercent=30 -XX:G1RSetUpdatingPauseTimePercent=5 -XX:G1ReservePercent=20 -XX:InitiatingHeapOccupancyPercent=15 -XX:MaxGCPauseMillis=200 -XX:MaxTenuringThreshold=1 -XX:SurvivorRatio=32 -Dusing.aikars.flags=https://mcflags.emc.gs -Dlog4j2.configurationFile=log4j2.xml -Daikars.new.flags=true";

        public const string ZgcFlags = "-Xms16384M -Xmx16384M -XX:+UnlockExperimentalVMOptions -XX:+UseZGC -XX:+ZGenerational -XX:+DisableExplicitGC -XX:+AlwaysPreTouch -XX:+PerfDisableSharedMem -Dlog4j2.configurationFile=log4j2.xml";

        public const string LowMemoryFlags = "-Xms4096M -Xmx4096M -XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=150 -XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:G1NewSizePercent=20 -XX:G1ReservePercent=15 -XX:G1HeapRegionSize=4M -XX:G1MixedGCCountTarget=4 -XX:InitiatingHeapOccupancyPercent=20 -Dlog4j2.configurationFile=log4j2.xml";
    }

    // Handles serialization and deserialization of the LauncherConfig object
    public static class ConfigManager
    {
        public const string ConfigFileName = "launcher_config.json";
        public static LauncherConfig Config { get; set; } = new LauncherConfig(); // Holds the active configuration in memory

        // OPTIMIZATION: Cached options prevent CPU allocation overhead on every instant save
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        // Reads the configuration file from disk. If missing or corrupted, uses default values.
        public static void LoadConfig()
        {
            try
            {
                string json = File.ReadAllText(ConfigFileName);
                Config = JsonSerializer.Deserialize<LauncherConfig>(json) ?? new LauncherConfig();
                Console.ForegroundColor = ConsoleColor.DarkGray;
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
        // Writes the current in-memory configuration object back to disk immediately
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