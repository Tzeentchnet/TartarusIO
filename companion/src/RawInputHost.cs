using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TartarusIO.Companion
{
    public sealed class TartarusInputEventArgs : EventArgs
    {
        public TartarusInputEventArgs(string controlId, bool pressed)
        {
            ControlId = controlId;
            Pressed = pressed;
        }

        public string ControlId { get; private set; }
        public bool Pressed { get; private set; }
    }

    public sealed class RawInputHost : NativeWindow, IDisposable
    {
        private const string TargetDeviceMarker = "VID_1532&PID_0208";
        private readonly Dictionary<IntPtr, string> _devicePaths = new Dictionary<IntPtr, string>();
        private readonly HashSet<string> _pressedControls = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<int> _pressedDirections = new HashSet<int>();
        private readonly Dictionary<int, string> _keyboardControls;
        private readonly object _statusSync = new object();
        private InputEventStatus _lastInput;
        private long _inputSequence;
        private bool _disposed;

        public RawInputHost()
        {
            _keyboardControls = CreateKeyboardControlMap();
            var createParams = new CreateParams();
            createParams.Caption = "Tartarus IO Raw Input";
            createParams.Parent = new IntPtr(-3);
            CreateHandle(createParams);
            RegisterDevices();
            RefreshDevices();
        }

        public event EventHandler<TartarusInputEventArgs> InputReceived;
        public event EventHandler DeviceChanged;

        public bool IsConnected { get; private set; }
        public string DevicePath { get; private set; }
        public int InterfaceCount { get; private set; }

        public InputEventStatus LastInput
        {
            get
            {
                lock (_statusSync)
                {
                    return _lastInput;
                }
            }
        }

        public void RefreshDevices()
        {
            var paths = new Dictionary<IntPtr, string>();
            uint count = 0;
            var entrySize = (uint)Marshal.SizeOf(typeof(NativeMethods.RAWINPUTDEVICELIST));
            NativeMethods.GetRawInputDeviceList(null, ref count, entrySize);
            if (count > 0)
            {
                var devices = new NativeMethods.RAWINPUTDEVICELIST[count];
                var result = NativeMethods.GetRawInputDeviceList(devices, ref count, entrySize);
                if (result != UInt32.MaxValue)
                {
                    foreach (var device in devices)
                    {
                        var path = GetDevicePath(device.device);
                        if (!String.IsNullOrWhiteSpace(path))
                        {
                            paths[device.device] = path;
                        }
                    }
                }
            }

            _devicePaths.Clear();
            foreach (var item in paths)
            {
                _devicePaths[item.Key] = item.Value;
            }

            var targetPaths = new List<string>();
            foreach (var path in paths.Values)
            {
                if (path.IndexOf(TargetDeviceMarker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetPaths.Add(path);
                }
            }

            var wasConnected = IsConnected;
            IsConnected = targetPaths.Count > 0;
            InterfaceCount = targetPaths.Count;
            DevicePath = targetPaths.Count > 0 ? targetPaths[0] : null;
            if (wasConnected != IsConnected && DeviceChanged != null)
            {
                DeviceChanged(this, EventArgs.Empty);
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WM_INPUT)
            {
                ProcessRawInput(message.LParam);
            }
            else if (message.Msg == NativeMethods.WM_INPUT_DEVICE_CHANGE)
            {
                RefreshDevices();
            }

            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DestroyHandle();
        }

        private void RegisterDevices()
        {
            var devices = new[]
            {
                new NativeMethods.RAWINPUTDEVICE
                {
                    usagePage = 0x01,
                    usage = 0x06,
                    flags = NativeMethods.RIDEV_INPUTSINK | NativeMethods.RIDEV_DEVNOTIFY,
                    target = Handle
                },
                new NativeMethods.RAWINPUTDEVICE
                {
                    usagePage = 0x01,
                    usage = 0x02,
                    flags = NativeMethods.RIDEV_INPUTSINK | NativeMethods.RIDEV_DEVNOTIFY,
                    target = Handle
                }
            };

            if (!NativeMethods.RegisterRawInputDevices(
                    devices,
                    (uint)devices.Length,
                    (uint)Marshal.SizeOf(typeof(NativeMethods.RAWINPUTDEVICE))))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Raw Input registration failed.");
            }
        }

        private void ProcessRawInput(IntPtr rawInputHandle)
        {
            uint size = 0;
            var headerSize = (uint)Marshal.SizeOf(typeof(NativeMethods.RAWINPUTHEADER));
            NativeMethods.GetRawInputData(rawInputHandle, NativeMethods.RID_INPUT, IntPtr.Zero, ref size, headerSize);
            if (size == 0)
            {
                return;
            }

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                var bytesRead = NativeMethods.GetRawInputData(
                    rawInputHandle,
                    NativeMethods.RID_INPUT,
                    buffer,
                    ref size,
                    headerSize);
                if (bytesRead != size)
                {
                    return;
                }

                var header = (NativeMethods.RAWINPUTHEADER)Marshal.PtrToStructure(
                    buffer,
                    typeof(NativeMethods.RAWINPUTHEADER));
                if (!IsTargetDevice(header.device) || header.type != NativeMethods.RIM_TYPEKEYBOARD)
                {
                    return;
                }

                var keyboardPointer = new IntPtr(buffer.ToInt64() + Marshal.SizeOf(typeof(NativeMethods.RAWINPUTHEADER)));
                var keyboard = (NativeMethods.RAWKEYBOARD)Marshal.PtrToStructure(
                    keyboardPointer,
                    typeof(NativeMethods.RAWKEYBOARD));
                var pressed = (keyboard.flags & NativeMethods.RI_KEY_BREAK) == 0;
                ProcessKeyboardEvent(keyboard.virtualKey, pressed);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void ProcessKeyboardEvent(int virtualKey, bool pressed)
        {
            if (virtualKey == 0x25 || virtualKey == 0x26 || virtualKey == 0x27 || virtualKey == 0x28)
            {
                ProcessDirection(virtualKey, pressed);
                return;
            }

            string controlId;
            if (!_keyboardControls.TryGetValue(virtualKey, out controlId))
            {
                return;
            }

            if (pressed)
            {
                if (!_pressedControls.Add(controlId))
                {
                    return;
                }
            }
            else if (!_pressedControls.Remove(controlId))
            {
                return;
            }

            Publish(controlId, pressed);
        }

        private void ProcessDirection(int virtualKey, bool pressed)
        {
            var previousControl = ComposeDirectionControl();
            var changed = pressed ? _pressedDirections.Add(virtualKey) : _pressedDirections.Remove(virtualKey);
            if (!changed)
            {
                return;
            }

            var nextControl = ComposeDirectionControl();
            if (!String.IsNullOrWhiteSpace(previousControl) && !String.Equals(previousControl, nextControl, StringComparison.Ordinal))
            {
                Publish(previousControl, false);
            }

            if (!String.IsNullOrWhiteSpace(nextControl) && !String.Equals(previousControl, nextControl, StringComparison.Ordinal))
            {
                Publish(nextControl, true);
            }
        }

        private string ComposeDirectionControl()
        {
            var up = _pressedDirections.Contains(0x26);
            var right = _pressedDirections.Contains(0x27);
            var down = _pressedDirections.Contains(0x28);
            var left = _pressedDirections.Contains(0x25);

            if (up && right) return "dpad-ne";
            if (down && right) return "dpad-se";
            if (down && left) return "dpad-sw";
            if (up && left) return "dpad-nw";
            if (up) return "dpad-n";
            if (right) return "dpad-e";
            if (down) return "dpad-s";
            if (left) return "dpad-w";
            return null;
        }

        private void Publish(string controlId, bool pressed)
        {
            lock (_statusSync)
            {
                _inputSequence += 1;
                _lastInput = new InputEventStatus
                {
                    sequence = _inputSequence,
                    controlId = controlId,
                    pressed = pressed,
                    timestamp = DateTime.UtcNow.ToString("o")
                };
            }

            var handler = InputReceived;
            if (handler != null)
            {
                handler(this, new TartarusInputEventArgs(controlId, pressed));
            }
        }

        private bool IsTargetDevice(IntPtr device)
        {
            string path;
            if (!_devicePaths.TryGetValue(device, out path))
            {
                path = GetDevicePath(device);
                if (!String.IsNullOrWhiteSpace(path))
                {
                    _devicePaths[device] = path;
                }
            }

            return !String.IsNullOrWhiteSpace(path)
                   && path.IndexOf(TargetDeviceMarker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetDevicePath(IntPtr device)
        {
            uint characterCount = 0;
            NativeMethods.GetRawInputDeviceInfo(device, NativeMethods.RIDI_DEVICENAME, null, ref characterCount);
            if (characterCount == 0)
            {
                return null;
            }

            var builder = new StringBuilder((int)characterCount);
            var result = NativeMethods.GetRawInputDeviceInfo(
                device,
                NativeMethods.RIDI_DEVICENAME,
                builder,
                ref characterCount);
            return result == UInt32.MaxValue ? null : builder.ToString();
        }

        private static Dictionary<int, string> CreateKeyboardControlMap()
        {
            return new Dictionary<int, string>
            {
                { 0x09, "01" },
                { 0x51, "02" },
                { 0x57, "03" },
                { 0x45, "04" },
                { 0x52, "05" },
                { 0x14, "06" },
                { 0x41, "07" },
                { 0x53, "08" },
                { 0x44, "09" },
                { 0x46, "10" },
                { 0x10, "11" },
                { 0x5A, "12" },
                { 0x58, "13" },
                { 0x43, "14" },
                { 0x56, "15" },
                { 0x12, "thumb" },
                { 0x20, "paddle" }
            };
        }
    }
}
