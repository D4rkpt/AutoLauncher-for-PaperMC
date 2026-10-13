using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PaperAutoUpdater
{
    // Handles version detection, communication with the PaperMC API, and downloading updates
    public static class Updater
    {
        private const string UserAgent = "AutoLauncher-for-Paper/2.1 (Contact: Discord Kranivox)";

        // OPTIMIZATION: Reusing HttpClient prevents socket exhaustion.
        private static readonly HttpClient SharedClient = new HttpClient();

        static Updater()
        {
            SharedClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        }

        // Validates if the specified version exists on the PaperMC API and retrieves its release channel (e.g., default, experimental)
        public static async Task<(bool isValid, string channel)> ValidateVersionAndGetChannelAsync(string version)
        {
            var (build, _, channel) = await GetLatestBuildInfoAsync(version);
            return (build != -1, channel); // Returns true if a valid build number was found
        }

        // The main update sequence executed before server launch
        public static async Task RunUpdateAndLaunchSequence()
        {
            string mcVersion = ConfigManager.Config.TargetVersion;

            if (string.IsNullOrEmpty(mcVersion))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  [WARNING] Could not determine the Minecraft version from '.paper' or 'versions' folders.");
                Console.ResetColor();
                Engine.RunServer(); // Launch blindly if version is unknown
                return;
            }

            if (ConfigManager.Config.AutoUpdate)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"🌐 [INFO] Minecraft version detected: {mcVersion}. Checking for updates...");
                Console.ResetColor();

                // We ignore the 'channel' return here because auto-update acts instantly without asking.
                var (latestBuild, downloadUrl, _) = await GetLatestBuildInfoAsync(mcVersion);

                if (latestBuild != -1 && !string.IsNullOrEmpty(downloadUrl))
                {
                    string expectedJarName = $"paper-{mcVersion}-{latestBuild}.jar";

                    // If the expected JAR does not exist, an update is required
                    if (!File.Exists(expectedJarName))
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"🌐 [INFO] New build ({latestBuild}) found! Downloading automatically...");
                        Console.ResetColor();

                        // Clean up any old paper jars to save disk space
                        foreach (var file in Directory.GetFiles(Directory.GetCurrentDirectory(), "paper-*.jar"))
                        {
                            try
                            {
                                File.Delete(file);
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.WriteLine($"🗑️  [CLEANUP] Old file deleted: {Path.GetFileName(file)}");
                                Console.ResetColor();
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"⚠️  [WARNING] Could not delete old file {Path.GetFileName(file)}: {ex.Message}");
                                Console.ResetColor();
                            }
                        }

                        await DownloadFileAsync(downloadUrl, expectedJarName);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✅ [INFO] Download completed successfully!");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✅ [INFO] The server is already running the latest available version.");
                        Console.ResetColor();
                    }
                    Engine.RunServer(expectedJarName); // Start the server using the verified/updated JAR
                    return;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ [ERROR] Failed to fetch PaperMC version from API. Attempting to start existing .jar...");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("⚙️  [INFO] Auto-Update is disabled. Skipping version check...");
                Console.ResetColor();
            }

            Engine.RunServer(); // Start the server with whatever is available
        }

        // Allows the UI to fetch the currently installed version for comparison (e.g., during upgrades/downgrades)
        public static string? GetInstalledVersion()
        {
            return GetVersionFromPaperHistory() ?? GetVersionFromFolder(); // Tries history JSON first, falls back to folder scanning
        }

        // Attempts to extract the exact installed Minecraft version from Paper's internal history file
        private static string? GetVersionFromPaperHistory()
        {
            // ... (keep internal logic unchanged)
            string historyPath = Path.Combine(Directory.GetCurrentDirectory(), ".paper", "version_history.json");
            if (File.Exists(historyPath))
            {
                try
                {
                    string json = File.ReadAllText(historyPath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        using JsonDocument doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("currentVersion", out JsonElement currentVersionElement))
                        {
                            string? currentVersion = currentVersionElement.GetString();
                            // Extracts the version number from string formats like 'git-Paper-xxx (MC: 1.20.4)'
                            if (!string.IsNullOrEmpty(currentVersion) && currentVersion.Contains("(MC: "))
                            {
                                int startIndex = currentVersion.IndexOf("(MC: ") + 5;
                                int endIndex = currentVersion.IndexOf(")", startIndex);
                                if (startIndex > 4 && endIndex > startIndex)
                                    return currentVersion.Substring(startIndex, endIndex - startIndex);
                            }
                        }
                    }
                }
                catch { } // Silently fail and fallback to folder method
            }
            return null;
        }

        // Fallback method: Scans the 'versions' folder to guess the installed version based on directory names
        private static string? GetVersionFromFolder()
        {
            string versionsPath = Path.Combine(Directory.GetCurrentDirectory(), "versions");
            if (!Directory.Exists(versionsPath)) return null;

            string[] directories = Directory.GetDirectories(versionsPath);
            if (directories.Length == 0) return null;

            string? highestVersion = null;
            Version? maxVer = null;

            foreach (var dir in directories)
            {
                string folderName = new DirectoryInfo(dir).Name;

                // Parses semantic versions like 1.20, 1.21.1, 26.3 to properly sort them mathematically
                if (Version.TryParse(folderName, out Version? currentVer))
                {
                    if (maxVer == null || currentVer > maxVer)
                    {
                        maxVer = currentVer;
                        highestVersion = folderName; // Keeps track of the highest numerical version found
                    }
                }
            }

            // Fallbacks to alphabetical if no strict numerical version format is found
            return highestVersion ?? new DirectoryInfo(directories[0]).Name;
        }

        // Queries the PaperMC API to retrieve the latest build number, download URL, and release channel for a given version
        private static async Task<(int, string?, string)> GetLatestBuildInfoAsync(string version)
        {
            try
            {
                // Revertemos para a tua API original (fill.papermc.io/v3) que tem as tuas versões customizadas (ex: 26.2, 26.3)
                string apiUrl = $"https://fill.papermc.io/v3/projects/paper/versions/{version}/builds";

                // Usamos GetAsync para capturar a resposta HTTP. Se der 404 ou 410, ele não dá 'crash'.
                using var response = await SharedClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return (-1, null, "ERROR"); // Falha silenciosamente. A UI trata de mostrar a mensagem bonita de erro.
                }

                string json = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);

                // A estrutura JSON da tua API V3 devolve a array diretamente na Root Element
                var builds = doc.RootElement.EnumerateArray();

                int highestBuild = -1;
                string? bestDownloadUrl = null;
                string latestChannel = "UNKNOWN";

                // Iterates through all builds to find the latest one (highest ID)
                foreach (var build in builds)
                {
                    // A tua API usa "id" para a build e não "build"
                    int currentBuild = build.GetProperty("id").GetInt32();
                    if (currentBuild > highestBuild)
                    {
                        highestBuild = currentBuild;
                        latestChannel = build.GetProperty("channel").GetString()?.ToUpper() ?? "UNKNOWN";

                        // A tua API devolve o link de download formatado desta maneira específica
                        bestDownloadUrl = build.GetProperty("downloads")
                                               .GetProperty("server:default")
                                               .GetProperty("url")
                                               .GetString();
                    }
                }
                return (highestBuild, bestDownloadUrl, latestChannel);
            }
            catch (Exception ex)
            {
                // Apenas dispara este erro vermelho em caso de falha de parsing JSON grave ou se a tua internet for a baixo
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ [SYSTEM ERROR] API Parsing failed: {ex.Message}");
                Console.ResetColor();
                return (-1, null, "ERROR");
            }
        }

        // OPTIMIZATION: Streams the download directly to the SSD to prevent RAM usage spikes
        // Prevents loading the entire multi-megabyte JAR file into memory before saving it
        private static async Task DownloadFileAsync(string url, string fileName)
        {
            using Stream stream = await SharedClient.GetStreamAsync(url);
            using FileStream fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream); // Directly buffers chunks from the network to the disk
        }

        // Checks GitHub for a new release of the launcher itself
        public static async Task CheckForLauncherUpdatesAsync()
        {
            try
            {
                string apiUrl = "https://api.github.com/repos/D4rkpt/AutoLauncher-for-PaperMC/releases/latest";
                using var response = await SharedClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(json);

                    // Extrai a tag da versão mais recente no GitHub (remove o "v" se usares "v2.2" nas tuas releases)
                    string? latestTag = doc.RootElement.GetProperty("tag_name").GetString();
                    string latestVersion = latestTag?.Replace("v", "") ?? "";

                    // A tua versão atual programada
                    string currentVersion = "2.2.1";

                    // Se a versão for diferente da atual, avisa o utilizador
                    if (!string.IsNullOrEmpty(latestVersion) && latestVersion != currentVersion)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("\n✨ [UPDATE AVAILABLE] A new version of AutoLauncher is available!");
                        Console.WriteLine($"✨ Current version: {currentVersion}  |  Latest version: {latestVersion}");
                        Console.WriteLine($"✨ Download here: https://github.com/D4rkpt/AutoLauncher-for-PaperMC/releases/latest\n");
                        Console.ResetColor();

                        // Dá 4 segundos para o utilizador conseguir ler a mensagem antes de o servidor arrancar
                        await Task.Delay(4000);
                    }
                }
            }
            catch
            {
                // Falha silenciosamente se o utilizador estiver sem internet, para não interromper o arranque
            }
        }
    }
}