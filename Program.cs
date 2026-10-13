using System;
using System.IO;
using System.Threading.Tasks;

namespace PaperAutoUpdater
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Forces the console to render Emojis correctly
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "AutoLauncher-for-Paper";
            
            UI.PrintHeader();

            // 1. Safety Verification (Location Interlock)
            Engine.VerifyLocation();

            // 2. Configuration System (Load or Setup)
            if (!File.Exists(ConfigManager.ConfigFileName))
            {
                UI.RunSetupWizard();
            }
            else
            {
                ConfigManager.LoadConfig();
            }

            // 3. Main Screen & Interruption Countdown
            bool interrupted = UI.StartLaunchCountdown();

            if (interrupted)
            {
                UI.OpenConfigurationMenu();
            }
            else
            {
                // Proceed to boot if not interrupted (or if delay is 0)
                await Updater.RunUpdateAndLaunchSequence();
            }
        }
    }
}