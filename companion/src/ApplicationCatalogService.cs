using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TartarusIO.Companion
{
    public sealed class ApplicationCatalogService
    {
        private readonly object _sync = new object();
        private ApplicationCatalog _cachedCatalog;
        private DateTime _cacheExpiresUtc;

        public ApplicationCatalog GetCatalog()
        {
            lock (_sync)
            {
                if (_cachedCatalog != null && DateTime.UtcNow < _cacheExpiresUtc)
                {
                    return _cachedCatalog;
                }

                _cachedCatalog = BuildCatalog();
                _cacheExpiresUtc = DateTime.UtcNow.AddMinutes(5);
                return _cachedCatalog;
            }
        }

        public ApplicationBrowseResult BrowseForExecutable(string initialDirectory)
        {
            ApplicationBrowseResult result = null;
            Exception failure = null;
            var thread = new Thread(new ThreadStart(
                delegate
                {
                    try
                    {
                        using (var dialog = new OpenFileDialog())
                        {
                            dialog.Title = "Select an application executable";
                            dialog.Filter = "Applications (*.exe)|*.exe";
                            dialog.CheckFileExists = true;
                            dialog.Multiselect = false;
                            dialog.RestoreDirectory = true;
                            if (!String.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
                            {
                                dialog.InitialDirectory = initialDirectory;
                            }

                            if (dialog.ShowDialog() != DialogResult.OK)
                            {
                                result = new ApplicationBrowseResult
                                {
                                    selected = false,
                                    message = "No application was selected."
                                };
                                return;
                            }

                            var application = CreateApplicationItem(
                                "custom",
                                GetExecutableDisplayName(dialog.FileName),
                                dialog.FileName,
                                Path.GetDirectoryName(dialog.FileName),
                                null);
                            result = new ApplicationBrowseResult
                            {
                                selected = true,
                                application = application,
                                message = application.name + " selected."
                            };
                        }
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                }));
            thread.IsBackground = true;
            thread.Name = "Tartarus IO Executable Picker";
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new InvalidOperationException("The executable picker could not be opened.", failure);
            }

            return result ?? new ApplicationBrowseResult
            {
                selected = false,
                message = "No application was selected."
            };
        }

        private static ApplicationCatalog BuildCatalog()
        {
            var steamGames = DiscoverSteamGames();
            var steamPaths = new HashSet<string>(
                steamGames.Where(item => !String.IsNullOrWhiteSpace(item.path)).Select(item => item.path),
                StringComparer.OrdinalIgnoreCase);
            var installedApplications = DiscoverInstalledApplications()
                .Where(item => String.IsNullOrWhiteSpace(item.path) || !steamPaths.Contains(item.path))
                .ToList();

            return new ApplicationCatalog
            {
                generatedAt = DateTime.UtcNow.ToString("o"),
                steamGames = steamGames.OrderBy(item => item.name, StringComparer.CurrentCultureIgnoreCase).ToList(),
                installedApplications = installedApplications
                    .OrderBy(item => item.name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
            };
        }

        private static List<ApplicationCatalogItem> DiscoverSteamGames()
        {
            var results = new List<ApplicationCatalogItem>();
            var steamRoots = GetSteamRoots();
            var libraryRoots = new HashSet<string>(steamRoots, StringComparer.OrdinalIgnoreCase);

            foreach (var steamRoot in steamRoots)
            {
                var libraryFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(libraryFile))
                {
                    continue;
                }

                try
                {
                    var content = File.ReadAllText(libraryFile);
                    foreach (Match match in Regex.Matches(
                                 content,
                                 "\"path\"\\s+\"([^\"]+)\"",
                                 RegexOptions.IgnoreCase))
                    {
                        AddExistingDirectory(libraryRoots, UnescapeVdfPath(match.Groups[1].Value));
                    }

                    foreach (Match match in Regex.Matches(
                                 content,
                                 "\"\\d+\"\\s+\"([A-Za-z]:\\\\[^\"]+)\"",
                                 RegexOptions.IgnoreCase))
                    {
                        AddExistingDirectory(libraryRoots, UnescapeVdfPath(match.Groups[1].Value));
                    }
                }
                catch (IOException exception)
                {
                    Logger.Error("A Steam library list could not be read.", exception);
                }
                catch (UnauthorizedAccessException exception)
                {
                    Logger.Error("A Steam library list could not be accessed.", exception);
                }
            }

            foreach (var libraryRoot in libraryRoots)
            {
                var steamAppsDirectory = Path.Combine(libraryRoot, "steamapps");
                if (!Directory.Exists(steamAppsDirectory))
                {
                    continue;
                }

                string[] manifests;
                try
                {
                    manifests = Directory.GetFiles(steamAppsDirectory, "appmanifest_*.acf");
                }
                catch (IOException exception)
                {
                    Logger.Error("Steam application manifests could not be listed.", exception);
                    continue;
                }
                catch (UnauthorizedAccessException exception)
                {
                    Logger.Error("Steam application manifests could not be accessed.", exception);
                    continue;
                }

                foreach (var manifest in manifests)
                {
                    try
                    {
                        var content = File.ReadAllText(manifest);
                        var appId = ReadVdfValue(content, "appid");
                        var name = ReadVdfValue(content, "name");
                        var installDirectoryName = ReadVdfValue(content, "installdir");
                        if (String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(installDirectoryName))
                        {
                            continue;
                        }
                        if (IsIgnoredSteamApplication(appId, name))
                        {
                            continue;
                        }

                        var installDirectory = Path.Combine(steamAppsDirectory, "common", installDirectoryName);
                        var executable = FindBestExecutable(installDirectory, name, installDirectoryName, 3);
                        results.Add(CreateApplicationItem(
                            "steam",
                            name,
                            executable,
                            installDirectory,
                            appId));
                    }
                    catch (IOException exception)
                    {
                        Logger.Error("A Steam application manifest could not be read.", exception);
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        Logger.Error("A Steam application manifest could not be accessed.", exception);
                    }
                }
            }

            return results
                .GroupBy(item => item.steamAppId + "|" + item.installDirectory, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static List<ApplicationCatalogItem> DiscoverInstalledApplications()
        {
            var applications = new Dictionary<string, ApplicationCatalogItem>(StringComparer.OrdinalIgnoreCase);
            var locations = new[]
            {
                new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Registry64),
                new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Registry32),
                new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64),
                new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry32)
            };

            foreach (var location in locations)
            {
                DiscoverAppPaths(location, applications);
                DiscoverUninstallEntries(location, applications);
            }

            return applications.Values
                .GroupBy(
                    item => item.name + "|" + item.executableName,
                    StringComparer.CurrentCultureIgnoreCase)
                .Select(group => group.OrderBy(item => item.path.Length).First())
                .ToList();
        }

        private static void DiscoverAppPaths(
            RegistryLocation location,
            IDictionary<string, ApplicationCatalogItem> applications)
        {
            const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\App Paths";
            using (var baseKey = OpenBaseKey(location))
            using (var appPaths = OpenSubKey(baseKey, keyPath))
            {
                if (appPaths == null)
                {
                    return;
                }

                foreach (var subKeyName in GetSubKeyNames(appPaths, keyPath))
                {
                    using (var appKey = OpenSubKey(appPaths, subKeyName))
                    {
                        if (appKey == null)
                        {
                            continue;
                        }

                        var executable = NormalizeExecutablePath(appKey.GetValue(null) as string);
                        AddInstalledApplication(applications, executable, null);
                    }
                }
            }
        }

        private static void DiscoverUninstallEntries(
            RegistryLocation location,
            IDictionary<string, ApplicationCatalogItem> applications)
        {
            const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
            using (var baseKey = OpenBaseKey(location))
            using (var uninstallKey = OpenSubKey(baseKey, keyPath))
            {
                if (uninstallKey == null)
                {
                    return;
                }

                foreach (var subKeyName in GetSubKeyNames(uninstallKey, keyPath))
                {
                    using (var applicationKey = OpenSubKey(uninstallKey, subKeyName))
                    {
                        if (applicationKey == null || Convert.ToInt32(applicationKey.GetValue("SystemComponent", 0)) == 1)
                        {
                            continue;
                        }

                        var displayName = applicationKey.GetValue("DisplayName") as string;
                        var displayIcon = NormalizeExecutablePath(applicationKey.GetValue("DisplayIcon") as string);
                        AddInstalledApplication(applications, displayIcon, displayName);
                    }
                }
            }
        }

        private static void AddInstalledApplication(
            IDictionary<string, ApplicationCatalogItem> applications,
            string executable,
            string displayName)
        {
            if (String.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            {
                return;
            }

            var fileName = Path.GetFileName(executable);
            if (IsUtilityExecutable(fileName))
            {
                return;
            }

            var name = String.IsNullOrWhiteSpace(displayName)
                ? GetExecutableDisplayName(executable)
                : displayName.Trim();
            applications[executable] = CreateApplicationItem(
                "windows",
                name,
                executable,
                Path.GetDirectoryName(executable),
                null);
        }

        private static List<string> GetSteamRoots()
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            TryAddRegistryDirectory(
                roots,
                RegistryHive.CurrentUser,
                RegistryView.Registry64,
                @"Software\Valve\Steam",
                "SteamPath");
            TryAddRegistryDirectory(
                roots,
                RegistryHive.CurrentUser,
                RegistryView.Registry32,
                @"Software\Valve\Steam",
                "SteamPath");
            TryAddRegistryDirectory(
                roots,
                RegistryHive.LocalMachine,
                RegistryView.Registry64,
                @"Software\Valve\Steam",
                "InstallPath");
            TryAddRegistryDirectory(
                roots,
                RegistryHive.LocalMachine,
                RegistryView.Registry32,
                @"Software\Valve\Steam",
                "InstallPath");

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            AddExistingDirectory(roots, Path.Combine(programFilesX86, "Steam"));
            return roots.ToList();
        }

        private static void TryAddRegistryDirectory(
            ISet<string> results,
            RegistryHive hive,
            RegistryView view,
            string keyPath,
            string valueName)
        {
            using (var baseKey = OpenBaseKey(new RegistryLocation(hive, view)))
            using (var key = OpenSubKey(baseKey, keyPath))
            {
                if (key != null)
                {
                    AddExistingDirectory(results, key.GetValue(valueName) as string);
                }
            }
        }

        private static RegistryKey OpenBaseKey(RegistryLocation location)
        {
            try
            {
                return RegistryKey.OpenBaseKey(location.Hive, location.View);
            }
            catch (SecurityException exception)
            {
                Logger.Error("A registry hive could not be accessed.", exception);
                return null;
            }
            catch (UnauthorizedAccessException exception)
            {
                Logger.Error("A registry hive could not be accessed.", exception);
                return null;
            }
        }

        private static RegistryKey OpenSubKey(RegistryKey parent, string path)
        {
            if (parent == null)
            {
                return null;
            }

            try
            {
                return parent.OpenSubKey(path, false);
            }
            catch (SecurityException exception)
            {
                Logger.Error("The registry key " + path + " could not be accessed.", exception);
                return null;
            }
            catch (UnauthorizedAccessException exception)
            {
                Logger.Error("The registry key " + path + " could not be accessed.", exception);
                return null;
            }
        }

        private static string[] GetSubKeyNames(RegistryKey key, string keyPath)
        {
            try
            {
                return key.GetSubKeyNames();
            }
            catch (SecurityException exception)
            {
                Logger.Error("The registry key " + keyPath + " could not be enumerated.", exception);
                return new string[0];
            }
            catch (UnauthorizedAccessException exception)
            {
                Logger.Error("The registry key " + keyPath + " could not be enumerated.", exception);
                return new string[0];
            }
        }

        private static void AddExistingDirectory(ISet<string> directories, string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var normalized = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'))
                .Replace('/', Path.DirectorySeparatorChar);
            if (Directory.Exists(normalized))
            {
                directories.Add(Path.GetFullPath(normalized));
            }
        }

        private static string NormalizeExecutablePath(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return String.Empty;
            }

            var expanded = Environment.ExpandEnvironmentVariables(value.Trim());
            string candidate;
            if (expanded.StartsWith("\"", StringComparison.Ordinal))
            {
                var closingQuote = expanded.IndexOf('"', 1);
                candidate = closingQuote > 1 ? expanded.Substring(1, closingQuote - 1) : expanded.Trim('"');
            }
            else
            {
                var executableEnd = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                candidate = executableEnd >= 0
                    ? expanded.Substring(0, executableEnd + 4)
                    : expanded.Split(',')[0];
            }

            candidate = candidate.Trim();
            return candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : String.Empty;
        }

        private static string FindBestExecutable(
            string installDirectory,
            string displayName,
            string installDirectoryName,
            int maxDepth)
        {
            if (!Directory.Exists(installDirectory))
            {
                return String.Empty;
            }

            var bestPath = String.Empty;
            var bestScore = Int32.MinValue;
            var directories = new Queue<DirectoryDepth>();
            directories.Enqueue(new DirectoryDepth(installDirectory, 0));
            var inspectedDirectories = 0;
            var inspectedExecutables = 0;

            while (directories.Count > 0 && inspectedDirectories < 120 && inspectedExecutables < 400)
            {
                var current = directories.Dequeue();
                inspectedDirectories += 1;
                string[] executables;
                try
                {
                    executables = Directory.GetFiles(current.Path, "*.exe", SearchOption.TopDirectoryOnly);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var executable in executables)
                {
                    inspectedExecutables += 1;
                    var score = ScoreExecutable(executable, displayName, installDirectoryName, current.Depth);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPath = executable;
                    }
                }

                if (current.Depth >= maxDepth)
                {
                    continue;
                }

                string[] childDirectories;
                try
                {
                    childDirectories = Directory.GetDirectories(current.Path);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var childDirectory in childDirectories)
                {
                    if (!IsExcludedDirectory(Path.GetFileName(childDirectory)))
                    {
                        directories.Enqueue(new DirectoryDepth(childDirectory, current.Depth + 1));
                    }
                }
            }

            return bestPath;
        }

        private static int ScoreExecutable(
            string executable,
            string displayName,
            string installDirectoryName,
            int depth)
        {
            var executableName = Path.GetFileNameWithoutExtension(executable);
            var normalizedExecutable = NormalizeName(executableName);
            var normalizedDisplayName = NormalizeName(displayName);
            var normalizedDirectory = NormalizeName(installDirectoryName);
            var score = Math.Max(0, 35 - depth * 8);

            if (normalizedExecutable == normalizedDirectory)
            {
                score += 120;
            }
            if (normalizedExecutable == normalizedDisplayName)
            {
                score += 110;
            }
            if ((!String.IsNullOrWhiteSpace(normalizedDirectory) &&
                 normalizedExecutable.Contains(normalizedDirectory)) ||
                (!String.IsNullOrWhiteSpace(normalizedExecutable) &&
                 normalizedDirectory.Contains(normalizedExecutable)))
            {
                score += 65;
            }
            if ((!String.IsNullOrWhiteSpace(normalizedDisplayName) &&
                 normalizedExecutable.Contains(normalizedDisplayName)) ||
                (!String.IsNullOrWhiteSpace(normalizedExecutable) &&
                 normalizedDisplayName.Contains(normalizedExecutable)))
            {
                score += 55;
            }

            var lowered = executableName.ToLowerInvariant();
            if (lowered.Contains("launcher"))
            {
                score -= 20;
            }
            if (IsUtilityExecutable(executableName))
            {
                score -= 180;
            }

            try
            {
                score += (int)Math.Min(25, new FileInfo(executable).Length / (8 * 1024 * 1024));
            }
            catch (IOException)
            {
                // File size is only a ranking hint.
            }
            catch (UnauthorizedAccessException)
            {
                // File size is only a ranking hint.
            }

            return score;
        }

        private static bool IsUtilityExecutable(string executableName)
        {
            var lowered = (executableName ?? String.Empty).ToLowerInvariant();
            return lowered.Contains("unins") ||
                   lowered.Contains("uninstall") ||
                   lowered.Contains("setup") ||
                   lowered.Contains("installer") ||
                   lowered.Contains("crashreport") ||
                   lowered.Contains("crashpad") ||
                   lowered.Contains("reporter") ||
                   lowered.Contains("updater") ||
                   lowered.Contains("update") ||
                   lowered.Contains("updatehelper") ||
                   lowered.Contains("helper") ||
                   lowered.Contains("bootstrap") ||
                   lowered.Contains("repair") ||
                   lowered.Contains("service") ||
                   lowered.Contains("server") ||
                   lowered.Contains("diag") ||
                   lowered.Contains("vc_redist") ||
                   lowered.Contains("dxsetup") ||
                   lowered.Contains("dotnet");
        }

        private static bool IsIgnoredSteamApplication(string appId, string name)
        {
            return String.Equals(appId, "228980", StringComparison.Ordinal) ||
                   (name ?? String.Empty).IndexOf(
                       "Steamworks Common Redistributables",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsExcludedDirectory(string directoryName)
        {
            var lowered = (directoryName ?? String.Empty).ToLowerInvariant();
            return lowered == "_commonredist" ||
                   lowered == "redist" ||
                   lowered == "redistributables" ||
                   lowered == "directx" ||
                   lowered == "dotnet";
        }

        private static string GetExecutableDisplayName(string executable)
        {
            try
            {
                var version = FileVersionInfo.GetVersionInfo(executable);
                if (!String.IsNullOrWhiteSpace(version.ProductName))
                {
                    return version.ProductName.Trim();
                }
                if (!String.IsNullOrWhiteSpace(version.FileDescription))
                {
                    return version.FileDescription.Trim();
                }
            }
            catch (FileNotFoundException)
            {
                // The executable name remains a valid fallback label.
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // The executable name remains a valid fallback label.
            }

            return Path.GetFileNameWithoutExtension(executable);
        }

        private static ApplicationCatalogItem CreateApplicationItem(
            string source,
            string name,
            string executable,
            string installDirectory,
            string steamAppId)
        {
            var executableName = String.IsNullOrWhiteSpace(executable)
                ? String.Empty
                : Path.GetFileName(executable);
            var identity = !String.IsNullOrWhiteSpace(steamAppId)
                ? steamAppId
                : !String.IsNullOrWhiteSpace(executable)
                    ? executable.ToLowerInvariant()
                    : (name + "|" + installDirectory).ToLowerInvariant();
            return new ApplicationCatalogItem
            {
                id = source + ":" + identity,
                name = name,
                executableName = executableName,
                path = executable ?? String.Empty,
                installDirectory = installDirectory ?? String.Empty,
                source = source,
                steamAppId = steamAppId ?? String.Empty,
                selectable = !String.IsNullOrWhiteSpace(executableName)
            };
        }

        private static string ReadVdfValue(string content, string key)
        {
            var match = Regex.Match(
                content,
                "\"" + Regex.Escape(key) + "\"\\s+\"([^\"]*)\"",
                RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Replace("\\\"", "\"") : String.Empty;
        }

        private static string UnescapeVdfPath(string path)
        {
            return path.Replace("\\\\", "\\");
        }

        private static string NormalizeName(string value)
        {
            return Regex.Replace((value ?? String.Empty).ToLowerInvariant(), "[^a-z0-9]", String.Empty);
        }

        private sealed class RegistryLocation
        {
            public RegistryLocation(RegistryHive hive, RegistryView view)
            {
                Hive = hive;
                View = view;
            }

            public RegistryHive Hive { get; private set; }
            public RegistryView View { get; private set; }
        }

        private sealed class DirectoryDepth
        {
            public DirectoryDepth(string path, int depth)
            {
                Path = path;
                Depth = depth;
            }

            public string Path { get; private set; }
            public int Depth { get; private set; }
        }
    }
}
