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
            // Ensures the executable is in the correct directory before proceeding to avoid dumping files everywhere
            Engine.VerifyLocation();

            // Creates PaperLauncherLogs\ (inside the verified server folder) and starts logging standard output
            Logger.Init();

            // 2. Configuration System (Load or Setup)
            // If the configuration file is missing, trigger the first-time setup wizard
            if (!File.Exists(ConfigManager.ConfigFileName))
            {
                UI.RunSetupWizard();
            }
            else
            {
                // Load existing settings into memory
                ConfigManager.LoadConfig();
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