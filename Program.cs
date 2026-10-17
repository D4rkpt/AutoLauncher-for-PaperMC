using System;
using System.IO;
using System.Threading.Tasks;

namespace PaperAutoUpdater
{
    // The main entry point for the launcher application
    class Program
    {
        static async Task Main(string[] args)
        {
            // Forces the console to render Emojis correctly (otherwise they might appear as question marks)
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "AutoLauncher-for-Paper"; // Sets the console window title

            UI.PrintHeader(); // Displays the ASCII art banner

            // 1. Safety Verification (Location Interlock)
            // Verifies the directory files to ensure it's a valid server environment
            Engine.VerifyLocation();

            // Load the configuration FIRST to check if logs are enabled
            bool isFirstRun = !File.Exists(ConfigManager.ConfigFileName);
            if (!isFirstRun)
            {
                ConfigManager.LoadConfig();
            }

            // Start the logging system ONLY if enabled in the config
            if (ConfigManager.Config.EnableLogs)
            {
                Logger.Init();
            }

            // Proceed with the setup wizard or update checks
            if (isFirstRun)
            {
                UI.RunSetupWizard();
            }
            else
            {
                await Updater.CheckForLauncherUpdatesAsync();
            }

            // 3. Main Screen & Interruption Countdown
            // Returns true if the user pressed 'C' to interrupt the boot sequence
            bool interrupted = UI.StartLaunchCountdown();

            if (interrupted)
            {
                UI.OpenConfigurationMenu(); // Open the interactive settings UI
            }
            else
            {
                // Proceed to boot if not interrupted (or if delay is 0, skipping the prompt entirely)
                await Updater.RunUpdateAndLaunchSequence();
            }
        }
    }
}