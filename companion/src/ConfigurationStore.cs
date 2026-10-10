using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace TartarusIO.Companion
{
    public sealed class ConfigurationStore
    {
        private readonly object _sync = new object();
        private readonly JavaScriptSerializer _serializer;
        private readonly string _configurationPath;
        private AppConfiguration _configuration;
        private string _rawConfiguration;

        public ConfigurationStore(string dataDirectory)
        {
            Directory.CreateDirectory(dataDirectory);
            _configurationPath = Path.Combine(dataDirectory, "config.json");
            _serializer = new JavaScriptSerializer();
            _serializer.MaxJsonLength = Int32.MaxValue;
            Load();
        }

        public bool HasConfiguration
        {
            get
            {
                lock (_sync)
                {
                    return _configuration != null && _configuration.profiles != null && _configuration.profiles.Count > 0;
                }
            }
        }

        public string GetRawConfiguration()
        {
            lock (_sync)
            {
                return String.IsNullOrWhiteSpace(_rawConfiguration) ? "{}" : _rawConfiguration;
            }
        }

        public AppConfiguration GetConfiguration()
        {
            lock (_sync)
            {
                return _configuration;
            }
        }

        public void Save(string json)
        {
            if (String.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("The configuration body is empty.");
            }

            AppConfiguration parsed;
            try
            {
                parsed = _serializer.Deserialize<AppConfiguration>(json);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("The configuration is not valid JSON.", exception);
            }

            Validate(parsed);
            parsed.schemaVersion = Math.Max(parsed.schemaVersion, 3);
            var normalized = _serializer.Serialize(parsed);

            lock (_sync)
            {
                WriteConfigurationLocked(normalized);
                _configuration = parsed;
                _rawConfiguration = normalized;
            }
        }

        public bool ActivateProfileByName(string profileName)
        {
            lock (_sync)
            {
                if (_configuration == null || _configuration.profiles == null)
                {
                    return false;
                }

                var profile = _configuration.profiles.FirstOrDefault(
                    item => String.Equals(item.name, profileName, StringComparison.OrdinalIgnoreCase));
                if (profile == null)
                {
                    return false;
                }

                return ActivateProfileByIdLocked(profile.id);
            }
        }

        public bool ActivateProfileById(string profileId)
        {
            lock (_sync)
            {
                return ActivateProfileByIdLocked(profileId);
            }
        }

        public ProfileConfiguration GetActiveProfile()
        {
            lock (_sync)
            {
                if (_configuration == null || _configuration.profiles == null)
                {
                    return null;
                }

                return _configuration.profiles.FirstOrDefault(
                           item => String.Equals(item.id, _configuration.activeProfileId, StringComparison.Ordinal))
                       ?? _configuration.profiles.FirstOrDefault();
            }
        }

        public MacroConfiguration GetMacro(string macroName)
        {
            lock (_sync)
            {
                if (_configuration == null || _configuration.macros == null)
                {
                    return null;
                }

                return _configuration.macros.FirstOrDefault(
                    item => String.Equals(item.name, macroName, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void Load()
        {
            if (!File.Exists(_configurationPath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_configurationPath, Encoding.UTF8);
                var parsed = _serializer.Deserialize<AppConfiguration>(json);
                Validate(parsed);
                _configuration = parsed;
                _rawConfiguration = _serializer.Serialize(parsed);
            }
            catch (Exception exception)
            {
                Logger.Error("The saved configuration could not be loaded.", exception);
                var invalidPath = _configurationPath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                File.Move(_configurationPath, invalidPath);
            }
        }

        private static void Validate(AppConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new InvalidDataException("The configuration object is missing.");
            }

            if (configuration.profiles == null || configuration.profiles.Count == 0)
            {
                throw new InvalidDataException("At least one profile is required.");
            }

            foreach (var profile in configuration.profiles)
            {
                if (profile == null || String.IsNullOrWhiteSpace(profile.id) || String.IsNullOrWhiteSpace(profile.name))
                {
                    throw new InvalidDataException("Every profile needs an id and name.");
                }

                if (profile.mappings == null)
                {
                    profile.mappings = new Dictionary<string, BindingConfiguration>();
                }

                if (profile.lighting == null)
                {
                    throw new InvalidDataException("Every profile needs lighting settings.");
                }
                if (!String.Equals(profile.lighting.effect, "Static", StringComparison.OrdinalIgnoreCase)
                    && !String.Equals(profile.lighting.effect, "Breathing", StringComparison.OrdinalIgnoreCase)
                    && !String.Equals(profile.lighting.effect, "Reactive", StringComparison.OrdinalIgnoreCase)
                    && !String.Equals(profile.lighting.effect, "Spectrum", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Lighting effects must be Static, Breathing, Reactive, or Spectrum.");
                }
                if (!IsHexColor(profile.lighting.color))
                {
                    throw new InvalidDataException("Lighting colors must use #RRGGBB format.");
                }
                if (profile.lighting.brightness < 0 || profile.lighting.brightness > 100)
                {
                    throw new InvalidDataException("Lighting brightness must be between 0 and 100.");
                }

                profile.app = profile.app ?? String.Empty;
                profile.appPath = profile.appPath ?? String.Empty;
                profile.appDisplayName = profile.appDisplayName ?? String.Empty;
            }

            if (configuration.macros == null)
            {
                configuration.macros = new List<MacroConfiguration>();
            }

            if (!configuration.profiles.Any(
                    item => String.Equals(item.id, configuration.defaultProfileId, StringComparison.Ordinal)))
            {
                var fallback = configuration.profiles.FirstOrDefault(
                                   item => String.Equals(item.id, "desktop", StringComparison.Ordinal))
                               ?? configuration.profiles.FirstOrDefault(
                                   item => String.IsNullOrWhiteSpace(item.app) && String.IsNullOrWhiteSpace(item.appPath))
                               ?? configuration.profiles[0];
                configuration.defaultProfileId = fallback.id;
            }

            if (!configuration.profiles.Any(
                    item => String.Equals(item.id, configuration.activeProfileId, StringComparison.Ordinal)))
            {
                configuration.activeProfileId = configuration.defaultProfileId;
            }

            if (!configuration.profiles.Any(
                    item => String.Equals(item.id, configuration.selectedProfileId, StringComparison.Ordinal)))
            {
                configuration.selectedProfileId = configuration.activeProfileId;
            }
        }

        private static bool IsHexColor(string value)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#')
            {
                return false;
            }

            for (var index = 1; index < value.Length; index += 1)
            {
                var character = value[index];
                var isDigit = character >= '0' && character <= '9';
                var isLowerHex = character >= 'a' && character <= 'f';
                var isUpperHex = character >= 'A' && character <= 'F';
                if (!isDigit && !isLowerHex && !isUpperHex)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ActivateProfileByIdLocked(string profileId)
        {
            if (_configuration == null || _configuration.profiles == null)
            {
                return false;
            }

            var profileExists = _configuration.profiles.Any(
                item => String.Equals(item.id, profileId, StringComparison.Ordinal));
            if (!profileExists || String.Equals(_configuration.activeProfileId, profileId, StringComparison.Ordinal))
            {
                return false;
            }

            _configuration.activeProfileId = profileId;
            _rawConfiguration = _serializer.Serialize(_configuration);
            WriteConfigurationLocked(_rawConfiguration);
            return true;
        }

        private void WriteConfigurationLocked(string normalized)
        {
            var temporaryPath = _configurationPath + ".tmp";
            File.WriteAllText(temporaryPath, normalized, new UTF8Encoding(false));

            if (File.Exists(_configurationPath))
            {
                File.Replace(temporaryPath, _configurationPath, null);
            }
            else
            {
                File.Move(temporaryPath, _configurationPath);
            }
        }
    }

    public static class Logger
    {
        private static readonly object Sync = new object();
        private static string _logPath;

        public static void Initialize(string dataDirectory)
        {
            Directory.CreateDirectory(dataDirectory);
            _logPath = Path.Combine(dataDirectory, "companion.log");
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        private static void Write(string level, string message, Exception exception)
        {
            if (String.IsNullOrWhiteSpace(_logPath))
            {
                return;
            }

            var line = String.Format(
                "{0:o} [{1}] {2}{3}",
                DateTime.UtcNow,
                level,
                message,
                exception == null ? String.Empty : Environment.NewLine + exception);

            lock (Sync)
            {
                File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
    }
}
