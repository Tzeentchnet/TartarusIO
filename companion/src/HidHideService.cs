using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using Microsoft.Win32;

namespace TartarusIO.Companion
{
    public sealed class HidHideService
    {
        public const string DownloadUrl = "https://github.com/nefarius/HidHide/releases/latest";
        private const string DependencyKey = @"Installer\Dependencies\NSS.Drivers.HidHide.x64";
        private const string ProductKey =
            @"SOFTWARE\Nefarius Software Solutions e.U.\Nefarius Software Solutions e.U. HidHide";
        private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string DriverServiceKey = @"SYSTEM\CurrentControlSet\Services\HidHide";
        private const string CliFileName = "HidHideCLI.exe";
        private readonly string _executablePath;
        private readonly object _statusSync = new object();

        public HidHideService(string executablePath)
        {
            _executablePath = executablePath;
        }

        public HidHideStatus GetStatus()
        {
            lock (_statusSync)
            {
                return GetStatusLocked();
            }
        }

        private HidHideStatus GetStatusLocked()
        {
            bool registeredInstallation;
            var installLocations = GetRegisteredInstallLocations(out registeredInstallation);
            var cliPath = FindCliPath(installLocations);
            var installed = registeredInstallation
                            || IsRegistryKeyPresent(DependencyKey)
                            || IsRegistryKeyPresent(ProductKey)
                            || IsDriverServicePresent()
                            || cliPath != null;
            var status = new HidHideStatus
            {
                installed = installed,
                cliAvailable = cliPath != null,
                downloadUrl = DownloadUrl
            };

            if (!installed)
            {
                status.message = "HidHide is required to prevent duplicate input.";
                return status;
            }

            if (cliPath == null)
            {
                status.message = "HidHide is installed, but HidHideCLI.exe was not found.";
                return status;
            }

            try
            {
                var cloakOutput = RunCli(cliPath, "--cloak-state", false);
                var applicationOutput = RunCli(cliPath, "--app-list", false);
                var deviceOutput = RunCli(cliPath, "--dev-list", false);
                status.cloakActive = IsPositiveState(cloakOutput);
                status.appRegistered =
                    applicationOutput.IndexOf(Path.GetFileName(_executablePath), StringComparison.OrdinalIgnoreCase) >= 0;
                status.deviceHidden =
                    deviceOutput.IndexOf("VID_1532&PID_0208", StringComparison.OrdinalIgnoreCase) >= 0;
                status.ready = status.cloakActive && status.appRegistered && status.deviceHidden;
                status.message = status.ready
                    ? "HidHide is configured for Tartarus IO."
                    : BuildNotReadyMessage(status);
            }
            catch (Exception exception)
            {
                Logger.Error("HidHide status could not be queried.", exception);
                status.message = "HidHide status could not be verified.";
            }

            return status;
        }

        public ApiResult LaunchElevatedConfiguration()
        {
            if (FindCliPath() == null)
            {
                return new ApiResult
                {
                    ok = false,
                    message = "Install HidHide from its official release page, reboot, then try again."
                };
            }

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = _executablePath,
                    Arguments = "--configure-hidhide",
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Path.GetDirectoryName(_executablePath)
                });
                if (process == null)
                {
                    throw new InvalidOperationException("The elevated configuration process did not start.");
                }

                process.WaitForExit(90000);
                if (!process.HasExited)
                {
                    return new ApiResult
                    {
                        ok = false,
                        message = "HidHide setup is still waiting for confirmation."
                    };
                }

                return new ApiResult
                {
                    ok = process.ExitCode == 0,
                    message = process.ExitCode == 0
                        ? "HidHide was configured. Unplug and reconnect the Tartarus Chroma."
                        : "HidHide setup did not complete. Check the companion log for details."
                };
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                if (exception.NativeErrorCode == 1223)
                {
                    return new ApiResult { ok = false, message = "HidHide setup was cancelled." };
                }

                Logger.Error("HidHide setup could not start.", exception);
                return new ApiResult { ok = false, message = "HidHide setup could not start." };
            }
            catch (Exception exception)
            {
                Logger.Error("HidHide setup failed.", exception);
                return new ApiResult { ok = false, message = exception.Message };
            }
        }

        public int ConfigureElevated()
        {
            if (!IsAdministrator())
            {
                Logger.Info("HidHide configuration was started without administrator rights.");
                return 5;
            }

            var cliPath = FindCliPath();
            if (cliPath == null)
            {
                Logger.Info("HidHideCLI.exe is not installed.");
                return 2;
            }

            try
            {
                RunCli(cliPath, "--app-reg " + Quote(_executablePath), true);
                RunCli(cliPath, "--inv-off", true);

                var deviceIds = GetTartarusDeviceIds();
                if (deviceIds.Count == 0)
                {
                    Logger.Info("No Tartarus Chroma device instances were found for HidHide.");
                    return 3;
                }

                var hiddenCount = 0;
                foreach (var deviceId in deviceIds)
                {
                    try
                    {
                        RunCli(cliPath, "--dev-hide " + Quote(deviceId), true);
                        hiddenCount += 1;
                    }
                    catch (Exception exception)
                    {
                        Logger.Error("HidHide rejected device " + deviceId, exception);
                    }
                }

                if (hiddenCount == 0)
                {
                    return 4;
                }

                RunCli(cliPath, "--cloak-on", true);
                Logger.Info("HidHide configuration completed for " + hiddenCount + " device interfaces.");
                return 0;
            }
            catch (Exception exception)
            {
                Logger.Error("Elevated HidHide configuration failed.", exception);
                return 1;
            }
        }

        private static bool IsRegistryKeyPresent(string path)
        {
            using (var key = Registry.ClassesRoot.OpenSubKey(path))
            {
                return key != null;
            }
        }

        private static bool IsDriverServicePresent()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(DriverServiceKey))
            {
                return key != null;
            }
        }

        private static List<string> GetRegisteredInstallLocations(out bool installed)
        {
            var locations = new List<string>();
            installed = false;

            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var uninstallKey = baseKey.OpenSubKey(UninstallKey))
                {
                    if (uninstallKey == null)
                    {
                        continue;
                    }

                    foreach (var productKeyName in uninstallKey.GetSubKeyNames())
                    {
                        using (var productKey = uninstallKey.OpenSubKey(productKeyName))
                        {
                            if (productKey == null)
                            {
                                continue;
                            }

                            var displayName = productKey.GetValue("DisplayName") as string;
                            if (!String.Equals(displayName, "HidHide", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            installed = true;
                            var installLocation = productKey.GetValue("InstallLocation") as string;
                            if (!String.IsNullOrWhiteSpace(installLocation))
                            {
                                locations.Add(installLocation);
                            }
                        }
                    }
                }
            }

            return locations;
        }

        private static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private string FindCliPath()
        {
            bool registeredInstallation;
            return FindCliPath(GetRegisteredInstallLocations(out registeredInstallation));
        }

        private string FindCliPath(IEnumerable<string> registeredInstallLocations)
        {
            var candidates = new List<string>(registeredInstallLocations);
            using (var key = Registry.ClassesRoot.OpenSubKey(ProductKey))
            {
                if (key != null)
                {
                    var productPath = key.GetValue("Path") as string;
                    if (!String.IsNullOrWhiteSpace(productPath))
                    {
                        candidates.Add(productPath);
                    }
                }
            }

            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Nefarius Software Solutions",
                "HidHide"));
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Nefarius Software Solutions e.U",
                "HidHide"));

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(candidate)
                    && String.Equals(Path.GetFileName(candidate), CliFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }

                if (!Directory.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    var file = Directory.GetFiles(candidate, CliFileName, SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (file != null)
                    {
                        return file;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Logger.Info("A HidHide installation folder could not be searched: " + candidate);
                }
            }

            return null;
        }

        private static string RunCli(string cliPath, string arguments, bool requireSuccess)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("HidHideCLI.exe did not start.");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(15000);
                if (!process.HasExited)
                {
                    process.Kill();
                    throw new TimeoutException("HidHideCLI.exe did not finish.");
                }

                if (requireSuccess && process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "HidHideCLI exited with code " + process.ExitCode + ": " + error);
                }

                return output + Environment.NewLine + error;
            }
        }

        private static List<string> GetTartarusDeviceIds()
        {
            var ids = new List<string>();
            using (var searcher = new ManagementObjectSearcher(
                       "SELECT DeviceID FROM Win32_PnPEntity WHERE DeviceID LIKE '%VID_1532&PID_0208%'"))
            {
                foreach (ManagementObject device in searcher.Get())
                {
                    var deviceId = device["DeviceID"] as string;
                    if (!String.IsNullOrWhiteSpace(deviceId))
                    {
                        ids.Add(deviceId);
                    }
                }
            }

            return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool IsPositiveState(string output)
        {
            if (String.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            var normalized = output.ToLowerInvariant();
            return (normalized.Contains("active") || normalized.Contains("enabled") || normalized.Contains("true")
                    || normalized.Contains("on"))
                   && !normalized.Contains("inactive")
                   && !normalized.Contains("disabled")
                   && !normalized.Contains("false")
                   && !normalized.Contains("off");
        }

        private static string BuildNotReadyMessage(HidHideStatus status)
        {
            if (!status.appRegistered)
            {
                return "Tartarus IO is not allowed through the HidHide cloak.";
            }
            if (!status.deviceHidden)
            {
                return "The Tartarus Chroma is not hidden from other applications.";
            }
            if (!status.cloakActive)
            {
                return "HidHide device hiding is not enabled.";
            }
            return "HidHide setup is incomplete.";
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
