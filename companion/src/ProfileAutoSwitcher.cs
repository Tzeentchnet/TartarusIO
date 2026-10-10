using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TartarusIO.Companion
{
    public sealed class ProfileAutoSwitcher
    {
        private readonly ConfigurationStore _configurationStore;
        private readonly object _sync = new object();
        private readonly object _refreshSync = new object();
        private ProfileSwitchStatus _status;
        private string _manualProfileId;

        public ProfileAutoSwitcher(ConfigurationStore configurationStore)
        {
            _configurationStore = configurationStore;
            _status = new ProfileSwitchStatus
            {
                message = "Save a profile to configure automatic switching."
            };
        }

        public bool Refresh()
        {
            lock (_refreshSync)
            {
                return RefreshLocked();
            }
        }

        private bool RefreshLocked()
        {
            var configuration = _configurationStore.GetConfiguration();
            if (configuration == null || configuration.profiles == null || configuration.profiles.Count == 0)
            {
                SetStatus(new ProfileSwitchStatus
                {
                    message = "Save a profile to configure automatic switching."
                });
                return false;
            }

            var runningApplications = GetRunningApplications();
            ProfileConfiguration selectedProfile = null;
            DateTime selectedStartTime = DateTime.MinValue;

            foreach (var profile in configuration.profiles)
            {
                if (!profile.autoSwitch)
                {
                    continue;
                }

                var executableName = GetExecutableName(profile);
                DateTime startTime;
                if (String.IsNullOrWhiteSpace(executableName) ||
                    !TryGetRunningStart(profile, executableName, runningApplications, out startTime))
                {
                    continue;
                }

                if (selectedProfile == null || startTime > selectedStartTime)
                {
                    selectedProfile = profile;
                    selectedStartTime = startTime;
                    continue;
                }

                if (startTime == selectedStartTime &&
                    String.Equals(profile.id, configuration.activeProfileId, StringComparison.Ordinal))
                {
                    selectedProfile = profile;
                }
            }

            var usingFallback = selectedProfile == null;
            string manualProfileId;
            lock (_sync)
            {
                if (selectedProfile != null)
                {
                    _manualProfileId = null;
                }
                manualProfileId = _manualProfileId;
            }

            var manualProfile = selectedProfile == null && !String.IsNullOrWhiteSpace(manualProfileId)
                ? configuration.profiles.FirstOrDefault(
                    item => String.Equals(item.id, manualProfileId, StringComparison.Ordinal))
                : null;
            if (manualProfile != null)
            {
                selectedProfile = manualProfile;
                usingFallback = false;
            }
            else if (usingFallback)
            {
                selectedProfile = configuration.profiles.FirstOrDefault(
                                      item => String.Equals(
                                          item.id,
                                          configuration.defaultProfileId,
                                          StringComparison.Ordinal))
                                  ?? configuration.profiles[0];
            }

            var changed = _configurationStore.ActivateProfileById(selectedProfile.id);
            var applicationName = usingFallback || manualProfile != null
                ? String.Empty
                : !String.IsNullOrWhiteSpace(selectedProfile.appDisplayName)
                    ? selectedProfile.appDisplayName
                    : GetExecutableName(selectedProfile);

            SetStatus(new ProfileSwitchStatus
            {
                activeProfileId = selectedProfile.id,
                fallbackProfileId = configuration.defaultProfileId,
                automatic = !usingFallback && manualProfile == null,
                manualOverride = manualProfile != null,
                usingFallback = usingFallback,
                matchedApplication = applicationName,
                message = manualProfile != null
                    ? selectedProfile.name + " was activated manually."
                    : usingFallback
                        ? selectedProfile.name + " is active because no linked application is running."
                        : selectedProfile.name + " matched " + applicationName + "."
            });

            if (changed)
            {
                Logger.Info("Active profile changed to " + selectedProfile.name + ".");
            }

            return changed;
        }

        public ApiResult ActivateManually(string profileId)
        {
            var configuration = _configurationStore.GetConfiguration();
            var profile = configuration == null || configuration.profiles == null
                ? null
                : configuration.profiles.FirstOrDefault(
                    item => String.Equals(item.id, profileId, StringComparison.Ordinal));
            if (profile == null)
            {
                return new ApiResult { ok = false, message = "The selected profile no longer exists." };
            }

            lock (_sync)
            {
                _manualProfileId = profile.id;
            }
            _configurationStore.ActivateProfileById(profile.id);
            SetStatus(new ProfileSwitchStatus
            {
                activeProfileId = profile.id,
                fallbackProfileId = configuration.defaultProfileId,
                automatic = false,
                manualOverride = true,
                usingFallback = false,
                matchedApplication = String.Empty,
                message = profile.name + " was activated manually."
            });
            Logger.Info("Active profile changed manually to " + profile.name + ".");
            return new ApiResult { ok = true, message = profile.name + " is active until a linked application takes over." };
        }

        public bool ActivateManuallyByName(string profileName)
        {
            var configuration = _configurationStore.GetConfiguration();
            var profile = configuration == null || configuration.profiles == null
                ? null
                : configuration.profiles.FirstOrDefault(
                    item => String.Equals(item.name, profileName, StringComparison.OrdinalIgnoreCase));
            return profile != null && ActivateManually(profile.id).ok;
        }

        public ProfileSwitchStatus GetStatus()
        {
            lock (_sync)
            {
                return new ProfileSwitchStatus
                {
                    activeProfileId = _status.activeProfileId,
                    fallbackProfileId = _status.fallbackProfileId,
                    automatic = _status.automatic,
                    manualOverride = _status.manualOverride,
                    usingFallback = _status.usingFallback,
                    matchedApplication = _status.matchedApplication,
                    message = _status.message
                };
            }
        }

        private void SetStatus(ProfileSwitchStatus status)
        {
            lock (_sync)
            {
                _status = status;
            }
        }

        private static Dictionary<string, DateTime> GetRunningApplications()
        {
            var applications = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    string executableName;
                    try
                    {
                        executableName = process.ProcessName + ".exe";
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }
                    catch (Win32Exception)
                    {
                        continue;
                    }

                    DateTime startTime;
                    try
                    {
                        startTime = process.StartTime.ToUniversalTime();
                    }
                    catch (InvalidOperationException)
                    {
                        startTime = DateTime.MinValue;
                    }
                    catch (Win32Exception)
                    {
                        startTime = DateTime.MinValue;
                    }
                    catch (NotSupportedException)
                    {
                        startTime = DateTime.MinValue;
                    }

                    AddRunningApplication(applications, executableName, startTime);

                    try
                    {
                        var executablePath = process.MainModule.FileName;
                        if (!String.IsNullOrWhiteSpace(executablePath))
                        {
                            AddRunningApplication(
                                applications,
                                Path.GetFullPath(executablePath),
                                startTime);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // The executable name remains available for matching.
                    }
                    catch (Win32Exception)
                    {
                        // Protected processes can still be matched by executable name.
                    }
                    catch (NotSupportedException)
                    {
                        // The executable name remains available for matching.
                    }
                }
            }

            return applications;
        }

        private static bool TryGetRunningStart(
            ProfileConfiguration profile,
            string executableName,
            IDictionary<string, DateTime> runningApplications,
            out DateTime startTime)
        {
            if (!String.IsNullOrWhiteSpace(profile.appPath))
            {
                try
                {
                    var fullPath = Path.GetFullPath(profile.appPath);
                    if (runningApplications.TryGetValue(fullPath, out startTime))
                    {
                        return true;
                    }
                }
                catch (ArgumentException)
                {
                    // Fall back to executable-name matching for a malformed legacy path.
                }
                catch (NotSupportedException)
                {
                    // Fall back to executable-name matching for a malformed legacy path.
                }
            }

            return runningApplications.TryGetValue(executableName, out startTime);
        }

        private static void AddRunningApplication(
            IDictionary<string, DateTime> applications,
            string key,
            DateTime startTime)
        {
            DateTime existingStartTime;
            if (!applications.TryGetValue(key, out existingStartTime) || startTime > existingStartTime)
            {
                applications[key] = startTime;
            }
        }

        private static string GetExecutableName(ProfileConfiguration profile)
        {
            var value = !String.IsNullOrWhiteSpace(profile.appPath) ? profile.appPath : profile.app;
            if (String.IsNullOrWhiteSpace(value))
            {
                return String.Empty;
            }

            string executableName;
            try
            {
                executableName = Path.GetFileName(value.Trim());
            }
            catch (ArgumentException)
            {
                executableName = value.Trim();
            }

            if (!executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                executableName += ".exe";
            }

            return executableName;
        }
    }
}
