using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace TartarusIO.Companion
{
    public sealed class ChromaLightingService : IDisposable
    {
        private const uint GenericWrite = 0x40000000;
        private const uint ShareRead = 0x00000001;
        private const uint ShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const byte TransactionId = 0x1F;
        private const byte LightingCommandClass = 0x03;
        private const byte SetRgbCommand = 0x01;
        private const byte SetEffectCommand = 0x02;
        private const byte BacklightLed = 0x05;
        private const byte StaticEffect = 0x00;
        private const byte BreathingEffect = 0x02;
        private const int ReportLength = 91;
        private const int SpectrumIntervalMilliseconds = 80;
        private const int ReactiveDurationMilliseconds = 350;

        private readonly ConfigurationStore _configurationStore;
        private readonly RawInputHost _rawInput;
        private readonly object _sync = new object();
        private SafeFileHandle _deviceHandle;
        private string _devicePath;
        private Timer _spectrumTimer;
        private Timer _reactiveTimer;
        private LightingSnapshot _activeLighting;
        private string _lastSignature;
        private int _spectrumHue;
        private bool _disposed;
        private LightingStatus _status;

        public ChromaLightingService(ConfigurationStore configurationStore, RawInputHost rawInput)
        {
            _configurationStore = configurationStore;
            _rawInput = rawInput;
            _status = new LightingStatus
            {
                available = rawInput.IsConnected,
                applied = false,
                message = rawInput.IsConnected
                    ? "Device lighting has not been applied yet."
                    : "Connect the Tartarus Chroma to control its lighting."
            };
        }

        public LightingStatus GetStatus()
        {
            lock (_sync)
            {
                return new LightingStatus
                {
                    available = _status.available,
                    applied = _status.applied,
                    profileId = _status.profileId,
                    effect = _status.effect,
                    message = _status.message
                };
            }
        }

        public void ApplyActiveProfile(bool profileChanged)
        {
            var profile = _configurationStore.GetActiveProfile();
            if (profile == null || profile.lighting == null)
            {
                SetUnavailable("Save a profile with lighting settings before controlling the device.");
                return;
            }

            var snapshot = LightingSnapshot.FromProfile(profile);
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                if (String.IsNullOrWhiteSpace(_rawInput.DevicePath))
                {
                    CloseDeviceLocked();
                    StopTimersLocked();
                    _activeLighting = null;
                    _lastSignature = null;
                    SetStatusLocked(
                        false,
                        false,
                        snapshot.ProfileId,
                        snapshot.Effect,
                        "Connect the Tartarus Chroma to control its lighting.");
                    return;
                }

                if (profileChanged && !snapshot.SaveWithProfile)
                {
                    _lastSignature = "retained:" + snapshot.ProfileId;
                    SetStatusLocked(
                        true,
                        true,
                        snapshot.ProfileId,
                        snapshot.Effect,
                        "The previous device lighting was retained because Save with profile is off.");
                    return;
                }

                if (!profileChanged
                    && String.Equals(_lastSignature, snapshot.Signature, StringComparison.Ordinal)
                    && _status.applied)
                {
                    return;
                }

                try
                {
                    ApplySnapshotLocked(snapshot);
                    _activeLighting = snapshot;
                    _lastSignature = snapshot.Signature;
                    SetStatusLocked(
                        true,
                        true,
                        snapshot.ProfileId,
                        snapshot.Effect,
                        snapshot.Enabled && snapshot.Brightness > 0
                            ? snapshot.Effect + " lighting is active on the connected keypad."
                            : "Lighting is off on the connected keypad.");
                    Logger.Info(
                        "Applied " + snapshot.Effect + " lighting for profile " + snapshot.ProfileId + ".");
                }
                catch (Exception exception)
                {
                    HandleFailureLocked(snapshot, "Device lighting could not be applied.", exception);
                }
            }
        }

        public void HandleInput(bool pressed)
        {
            if (!pressed)
            {
                return;
            }

            lock (_sync)
            {
                if (_disposed
                    || _activeLighting == null
                    || !_activeLighting.Enabled
                    || _activeLighting.Brightness <= 0
                    || !String.Equals(_activeLighting.Effect, "Reactive", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                try
                {
                    SendRgbLocked(_activeLighting.Red, _activeLighting.Green, _activeLighting.Blue);
                    if (_reactiveTimer == null)
                    {
                        _reactiveTimer = new Timer(EndReactivePulse, null, Timeout.Infinite, Timeout.Infinite);
                    }
                    _reactiveTimer.Change(ReactiveDurationMilliseconds, Timeout.Infinite);
                }
                catch (Exception exception)
                {
                    HandleFailureLocked(_activeLighting, "Reactive lighting could not be updated.", exception);
                }
            }
        }

        public void HandleDeviceChanged()
        {
            lock (_sync)
            {
                CloseDeviceLocked();
                _lastSignature = null;
                if (!_rawInput.IsConnected)
                {
                    StopTimersLocked();
                    _activeLighting = null;
                    SetStatusLocked(
                        false,
                        false,
                        null,
                        null,
                        "Connect the Tartarus Chroma to control its lighting.");
                    return;
                }
            }

            ApplyActiveProfile(true);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                StopTimersLocked();
                CloseDeviceLocked();
            }
        }

        private void ApplySnapshotLocked(LightingSnapshot snapshot)
        {
            StopTimersLocked();

            if (!snapshot.Enabled || snapshot.Brightness <= 0)
            {
                SendRgbLocked(0, 0, 0);
                SendEffectLocked(StaticEffect);
                return;
            }

            if (String.Equals(snapshot.Effect, "Static", StringComparison.OrdinalIgnoreCase))
            {
                SendRgbLocked(snapshot.Red, snapshot.Green, snapshot.Blue);
                SendEffectLocked(StaticEffect);
                return;
            }

            if (String.Equals(snapshot.Effect, "Breathing", StringComparison.OrdinalIgnoreCase))
            {
                SendRgbLocked(snapshot.Red, snapshot.Green, snapshot.Blue);
                SendEffectLocked(BreathingEffect);
                return;
            }

            if (String.Equals(snapshot.Effect, "Reactive", StringComparison.OrdinalIgnoreCase))
            {
                SendRgbLocked(0, 0, 0);
                SendEffectLocked(StaticEffect);
                return;
            }

            if (String.Equals(snapshot.Effect, "Spectrum", StringComparison.OrdinalIgnoreCase))
            {
                _spectrumHue = 0;
                var color = ColorFromHue(_spectrumHue, snapshot.Brightness);
                SendRgbLocked(color[0], color[1], color[2]);
                SendEffectLocked(StaticEffect);
                _spectrumTimer = new Timer(
                    AdvanceSpectrum,
                    null,
                    SpectrumIntervalMilliseconds,
                    SpectrumIntervalMilliseconds);
                return;
            }

            throw new InvalidDataException("Unsupported lighting effect: " + snapshot.Effect + ".");
        }

        private void AdvanceSpectrum(object state)
        {
            lock (_sync)
            {
                if (_disposed
                    || _activeLighting == null
                    || !String.Equals(_activeLighting.Effect, "Spectrum", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                try
                {
                    _spectrumHue = (_spectrumHue + 3) % 360;
                    var color = ColorFromHue(_spectrumHue, _activeLighting.Brightness);
                    SendRgbLocked(color[0], color[1], color[2]);
                }
                catch (Exception exception)
                {
                    HandleFailureLocked(_activeLighting, "Spectrum lighting could not be updated.", exception);
                }
            }
        }

        private void EndReactivePulse(object state)
        {
            lock (_sync)
            {
                if (_disposed
                    || _activeLighting == null
                    || !String.Equals(_activeLighting.Effect, "Reactive", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                try
                {
                    SendRgbLocked(0, 0, 0);
                }
                catch (Exception exception)
                {
                    HandleFailureLocked(_activeLighting, "Reactive lighting could not be cleared.", exception);
                }
            }
        }

        private void SendRgbLocked(byte red, byte green, byte blue)
        {
            SendFeatureReportLocked(
                CreateReport(
                    SetRgbCommand,
                    new[] { (byte)0x00, BacklightLed, red, green, blue }));
        }

        private void SendEffectLocked(byte effect)
        {
            SendFeatureReportLocked(
                CreateReport(
                    SetEffectCommand,
                    new[] { (byte)0x00, BacklightLed, effect }));
        }

        private void SendFeatureReportLocked(byte[] report)
        {
            var lastError = 0;
            for (var attempt = 0; attempt < 2; attempt += 1)
            {
                EnsureDeviceLocked();
                if (HidD_SetFeature(_deviceHandle, report, report.Length))
                {
                    return;
                }

                lastError = Marshal.GetLastWin32Error();
                CloseDeviceLocked();
            }

            throw new Win32Exception(
                lastError,
                "Windows rejected a lighting report for the Tartarus Chroma.");
        }

        private void EnsureDeviceLocked()
        {
            var path = _rawInput.DevicePath;
            if (String.IsNullOrWhiteSpace(path))
            {
                throw new IOException("The Tartarus Chroma lighting interface is not connected.");
            }

            if (_deviceHandle != null
                && !_deviceHandle.IsInvalid
                && !String.Equals(_devicePath, path, StringComparison.OrdinalIgnoreCase))
            {
                CloseDeviceLocked();
            }

            if (_deviceHandle != null && !_deviceHandle.IsInvalid)
            {
                return;
            }

            _deviceHandle = CreateFile(
                path,
                GenericWrite,
                ShareRead | ShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);
            _devicePath = path;
            if (_deviceHandle == null || _deviceHandle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                CloseDeviceLocked();
                throw new Win32Exception(
                    error,
                    "The Tartarus Chroma lighting interface could not be opened.");
            }
        }

        private void CloseDeviceLocked()
        {
            if (_deviceHandle != null)
            {
                _deviceHandle.Dispose();
                _deviceHandle = null;
            }
            _devicePath = null;
        }

        private void StopTimersLocked()
        {
            if (_spectrumTimer != null)
            {
                _spectrumTimer.Dispose();
                _spectrumTimer = null;
            }
            if (_reactiveTimer != null)
            {
                _reactiveTimer.Dispose();
                _reactiveTimer = null;
            }
        }

        private void HandleFailureLocked(
            LightingSnapshot snapshot,
            string message,
            Exception exception)
        {
            StopTimersLocked();
            CloseDeviceLocked();
            _lastSignature = null;
            SetStatusLocked(
                _rawInput.IsConnected,
                false,
                snapshot == null ? null : snapshot.ProfileId,
                snapshot == null ? null : snapshot.Effect,
                message + " " + exception.Message);
            Logger.Error(message, exception);
        }

        private void SetUnavailable(string message)
        {
            lock (_sync)
            {
                SetStatusLocked(_rawInput.IsConnected, false, null, null, message);
            }
        }

        private void SetStatusLocked(
            bool available,
            bool applied,
            string profileId,
            string effect,
            string message)
        {
            _status = new LightingStatus
            {
                available = available,
                applied = applied,
                profileId = profileId,
                effect = effect,
                message = message
            };
        }

        private static byte[] CreateReport(byte commandId, byte[] arguments)
        {
            var report = new byte[ReportLength];
            report[2] = TransactionId;
            report[6] = (byte)arguments.Length;
            report[7] = LightingCommandClass;
            report[8] = commandId;
            Array.Copy(arguments, 0, report, 9, arguments.Length);

            byte checksum = 0;
            for (var index = 3; index < 89; index += 1)
            {
                checksum ^= report[index];
            }
            report[89] = checksum;
            return report;
        }

        private static byte[] ColorFromHue(int hue, int brightness)
        {
            var normalizedHue = ((hue % 360) + 360) % 360;
            var sector = normalizedHue / 60;
            var offset = normalizedHue % 60;
            var rising = (byte)Math.Round(255.0 * offset / 60.0);
            var falling = (byte)(255 - rising);
            byte red;
            byte green;
            byte blue;

            switch (sector)
            {
                case 0:
                    red = 255;
                    green = rising;
                    blue = 0;
                    break;
                case 1:
                    red = falling;
                    green = 255;
                    blue = 0;
                    break;
                case 2:
                    red = 0;
                    green = 255;
                    blue = rising;
                    break;
                case 3:
                    red = 0;
                    green = falling;
                    blue = 255;
                    break;
                case 4:
                    red = rising;
                    green = 0;
                    blue = 255;
                    break;
                default:
                    red = 255;
                    green = 0;
                    blue = falling;
                    break;
            }

            return new[]
            {
                Scale(red, brightness),
                Scale(green, brightness),
                Scale(blue, brightness)
            };
        }

        private static byte Scale(byte value, int brightness)
        {
            return (byte)Math.Round(value * Math.Max(0, Math.Min(100, brightness)) / 100.0);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool HidD_SetFeature(
            SafeFileHandle device,
            byte[] reportBuffer,
            int reportBufferLength);

        private sealed class LightingSnapshot
        {
            public string ProfileId { get; private set; }
            public bool Enabled { get; private set; }
            public string Effect { get; private set; }
            public int Brightness { get; private set; }
            public bool SaveWithProfile { get; private set; }
            public byte Red { get; private set; }
            public byte Green { get; private set; }
            public byte Blue { get; private set; }
            public string Signature { get; private set; }

            public static LightingSnapshot FromProfile(ProfileConfiguration profile)
            {
                var lighting = profile.lighting;
                var color = ParseColor(lighting.color);
                var brightness = Math.Max(0, Math.Min(100, lighting.brightness));
                var snapshot = new LightingSnapshot
                {
                    ProfileId = profile.id,
                    Enabled = lighting.enabled,
                    Effect = lighting.effect ?? String.Empty,
                    Brightness = brightness,
                    SaveWithProfile = lighting.saveWithProfile,
                    Red = Scale(color[0], brightness),
                    Green = Scale(color[1], brightness),
                    Blue = Scale(color[2], brightness)
                };
                snapshot.Signature = String.Join(
                    "|",
                    new[]
                    {
                        snapshot.ProfileId,
                        snapshot.Enabled.ToString(),
                        snapshot.Effect,
                        lighting.color ?? String.Empty,
                        snapshot.Brightness.ToString(CultureInfo.InvariantCulture),
                        snapshot.SaveWithProfile.ToString()
                    });
                return snapshot;
            }

            private static byte[] ParseColor(string color)
            {
                if (String.IsNullOrWhiteSpace(color)
                    || color.Length != 7
                    || color[0] != '#')
                {
                    throw new InvalidDataException("Lighting colors must use #RRGGBB format.");
                }

                byte red;
                byte green;
                byte blue;
                if (!Byte.TryParse(
                        color.Substring(1, 2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out red)
                    || !Byte.TryParse(
                        color.Substring(3, 2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out green)
                    || !Byte.TryParse(
                        color.Substring(5, 2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out blue))
                {
                    throw new InvalidDataException("Lighting colors must use #RRGGBB format.");
                }

                return new[] { red, green, blue };
            }
        }
    }
}
