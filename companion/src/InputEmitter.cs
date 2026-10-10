using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TartarusIO.Companion
{
    public sealed class InputEmitter : IDisposable
    {
        private readonly ConfigurationStore _configurationStore;
        private readonly Func<string, bool> _activateProfileByName;
        private readonly object _sync = new object();
        private readonly Dictionary<string, BindingConfiguration> _heldBindings =
            new Dictionary<string, BindingConfiguration>(StringComparer.Ordinal);
        private readonly HashSet<string> _toggledControls = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Timer> _repeatTimers =
            new Dictionary<string, Timer>(StringComparer.Ordinal);
        private bool _disposed;

        public InputEmitter(ConfigurationStore configurationStore, Func<string, bool> activateProfileByName)
        {
            _configurationStore = configurationStore;
            _activateProfileByName = activateProfileByName;
        }

        public bool Enabled { get; set; }

        public void HandleControl(string controlId, bool pressed)
        {
            if (!Enabled || _disposed)
            {
                return;
            }

            var profile = _configurationStore.GetActiveProfile();
            BindingConfiguration binding;
            if (profile == null || profile.mappings == null || !profile.mappings.TryGetValue(controlId, out binding))
            {
                return;
            }

            var trigger = String.IsNullOrWhiteSpace(binding.trigger) ? "Press" : binding.trigger;
            if (String.Equals(trigger, "Hold", StringComparison.OrdinalIgnoreCase))
            {
                HandleHold(controlId, binding, pressed);
            }
            else if (String.Equals(trigger, "Toggle", StringComparison.OrdinalIgnoreCase))
            {
                if (pressed)
                {
                    HandleToggle(controlId, binding);
                }
            }
            else if (String.Equals(trigger, "Repeat", StringComparison.OrdinalIgnoreCase))
            {
                HandleRepeat(controlId, binding, pressed);
            }
            else if (pressed)
            {
                EmitPress(binding);
            }
        }

        public void StopAll()
        {
            lock (_sync)
            {
                foreach (var item in _heldBindings)
                {
                    EmitUp(item.Value);
                }
                _heldBindings.Clear();

                foreach (var controlId in _toggledControls)
                {
                    var profile = _configurationStore.GetActiveProfile();
                    BindingConfiguration binding;
                    if (profile != null && profile.mappings != null && profile.mappings.TryGetValue(controlId, out binding))
                    {
                        EmitUp(binding);
                    }
                }
                _toggledControls.Clear();

                foreach (var timer in _repeatTimers.Values)
                {
                    timer.Dispose();
                }
                _repeatTimers.Clear();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopAll();
        }

        private void HandleHold(string controlId, BindingConfiguration binding, bool pressed)
        {
            lock (_sync)
            {
                if (pressed)
                {
                    if (_heldBindings.ContainsKey(controlId))
                    {
                        return;
                    }
                    _heldBindings[controlId] = binding;
                    EmitDown(binding);
                }
                else
                {
                    BindingConfiguration heldBinding;
                    if (_heldBindings.TryGetValue(controlId, out heldBinding))
                    {
                        EmitUp(heldBinding);
                        _heldBindings.Remove(controlId);
                    }
                }
            }
        }

        private void HandleToggle(string controlId, BindingConfiguration binding)
        {
            lock (_sync)
            {
                if (_toggledControls.Remove(controlId))
                {
                    EmitUp(binding);
                }
                else
                {
                    _toggledControls.Add(controlId);
                    EmitDown(binding);
                }
            }
        }

        private void HandleRepeat(string controlId, BindingConfiguration binding, bool pressed)
        {
            lock (_sync)
            {
                if (pressed)
                {
                    if (_repeatTimers.ContainsKey(controlId))
                    {
                        return;
                    }

                    EmitPress(binding);
                    _repeatTimers[controlId] = new Timer(
                        delegate { EmitPress(binding); },
                        null,
                        350,
                        55);
                }
                else
                {
                    Timer timer;
                    if (_repeatTimers.TryGetValue(controlId, out timer))
                    {
                        timer.Dispose();
                        _repeatTimers.Remove(controlId);
                    }
                }
            }
        }

        private void EmitPress(BindingConfiguration binding)
        {
            if (String.Equals(binding.type, "Macro", StringComparison.OrdinalIgnoreCase))
            {
                var macro = _configurationStore.GetMacro(binding.value);
                if (macro != null)
                {
                    Task.Factory.StartNew(delegate { PlayMacro(macro); });
                }
                return;
            }

            if (String.Equals(binding.type, "Profile", StringComparison.OrdinalIgnoreCase))
            {
                _activateProfileByName(binding.value);
                return;
            }

            EmitDown(binding);
            EmitUp(binding);
        }

        private static void EmitDown(BindingConfiguration binding)
        {
            if (binding == null)
            {
                return;
            }

            if (String.Equals(binding.type, "Mouse", StringComparison.OrdinalIgnoreCase))
            {
                SendMouse(binding.value, true);
                return;
            }

            var modifiers = GetModifierKeys(binding.modifiers);
            foreach (var modifier in modifiers)
            {
                SendKey(modifier, true);
            }

            var actionKeys = GetActionKeys(binding.value);
            foreach (var virtualKey in actionKeys)
            {
                SendKey(virtualKey, true);
            }
        }

        private static void EmitUp(BindingConfiguration binding)
        {
            if (binding == null)
            {
                return;
            }

            if (String.Equals(binding.type, "Mouse", StringComparison.OrdinalIgnoreCase))
            {
                SendMouse(binding.value, false);
                return;
            }

            var actionKeys = GetActionKeys(binding.value);
            for (var index = actionKeys.Count - 1; index >= 0; index -= 1)
            {
                SendKey(actionKeys[index], false);
            }

            var modifiers = GetModifierKeys(binding.modifiers);
            for (var index = modifiers.Count - 1; index >= 0; index -= 1)
            {
                SendKey(modifiers[index], false);
            }
        }

        private void PlayMacro(MacroConfiguration macro)
        {
            if (macro.steps == null)
            {
                return;
            }

            foreach (var step in macro.steps)
            {
                if (!Enabled)
                {
                    return;
                }

                PlayMacroAction(step.action);
                if (step.delay > 0)
                {
                    Thread.Sleep(Math.Min(step.delay, 5000));
                }
            }
        }

        private static void PlayMacroAction(string action)
        {
            if (String.IsNullOrWhiteSpace(action))
            {
                return;
            }

            var separator = action.IndexOf(':');
            if (separator < 0)
            {
                return;
            }

            var operation = action.Substring(0, separator).Trim();
            var value = action.Substring(separator + 1).Trim();
            var binding = new BindingConfiguration
            {
                type = value.StartsWith("Mouse", StringComparison.OrdinalIgnoreCase) ? "Mouse" : "Keyboard",
                value = value,
                modifiers = new List<string>()
            };

            if (operation.EndsWith("down", StringComparison.OrdinalIgnoreCase))
            {
                EmitDown(binding);
            }
            else if (operation.EndsWith("up", StringComparison.OrdinalIgnoreCase))
            {
                EmitUp(binding);
            }
            else
            {
                EmitDown(binding);
                EmitUp(binding);
            }
        }

        private static List<ushort> GetModifierKeys(List<string> modifiers)
        {
            var keys = new List<ushort>();
            if (modifiers == null)
            {
                return keys;
            }

            foreach (var modifier in modifiers)
            {
                ushort key;
                if (TryGetVirtualKey(modifier, out key))
                {
                    keys.Add(key);
                }
            }
            return keys;
        }

        private static List<ushort> GetActionKeys(string value)
        {
            var keys = new List<ushort>();
            if (String.IsNullOrWhiteSpace(value))
            {
                return keys;
            }

            var parts = value.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                ushort key;
                if (TryGetVirtualKey(part.Trim(), out key))
                {
                    keys.Add(key);
                }
            }
            return keys;
        }

        private static bool TryGetVirtualKey(string value, out ushort virtualKey)
        {
            virtualKey = 0;
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            if (normalized.Length == 1)
            {
                var character = Char.ToUpperInvariant(normalized[0]);
                if ((character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9'))
                {
                    virtualKey = character;
                    return true;
                }

                switch (character)
                {
                    case '[': virtualKey = 0xDB; return true;
                    case ']': virtualKey = 0xDD; return true;
                    case '-': virtualKey = 0xBD; return true;
                    case '+': virtualKey = 0xBB; return true;
                }
            }

            var keys = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
            {
                { "Esc", 0x1B },
                { "Escape", 0x1B },
                { "Tab", 0x09 },
                { "Caps", 0x14 },
                { "Caps Lock", 0x14 },
                { "Shift", 0x10 },
                { "Ctrl", 0x11 },
                { "Control", 0x11 },
                { "Alt", 0x12 },
                { "Meta", 0x5B },
                { "Win", 0x5B },
                { "Space", 0x20 },
                { "Enter", 0x0D },
                { "Up", 0x26 },
                { "Right", 0x27 },
                { "Down", 0x28 },
                { "Left", 0x25 },
                { "Page Up", 0x21 },
                { "Page Down", 0x22 },
                { "Home", 0x24 },
                { "End", 0x23 },
                { "Backspace", 0x08 },
                { "Delete", 0x2E },
                { "Insert", 0x2D }
            };

            ushort mapped;
            if (keys.TryGetValue(normalized, out mapped))
            {
                virtualKey = mapped;
                return true;
            }

            if (normalized.Length >= 2 && (normalized[0] == 'F' || normalized[0] == 'f'))
            {
                int functionNumber;
                if (Int32.TryParse(normalized.Substring(1), out functionNumber)
                    && functionNumber >= 1
                    && functionNumber <= 24)
                {
                    virtualKey = (ushort)(0x70 + functionNumber - 1);
                    return true;
                }
            }

            return false;
        }

        private static void SendKey(ushort virtualKey, bool pressed)
        {
            var input = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                data = new NativeMethods.INPUTUNION
                {
                    keyboard = new NativeMethods.KEYBDINPUT
                    {
                        virtualKey = virtualKey,
                        scanCode = 0,
                        flags = pressed ? 0 : NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        extraInfo = IntPtr.Zero
                    }
                }
            };
            NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }

        private static void SendMouse(string value, bool pressed)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return;
            }

            uint flags;
            uint mouseData = 0;
            if (String.Equals(value, "Mouse 1", StringComparison.OrdinalIgnoreCase))
            {
                flags = pressed ? NativeMethods.MOUSEEVENTF_LEFTDOWN : NativeMethods.MOUSEEVENTF_LEFTUP;
            }
            else if (String.Equals(value, "Mouse 2", StringComparison.OrdinalIgnoreCase))
            {
                flags = pressed ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : NativeMethods.MOUSEEVENTF_RIGHTUP;
            }
            else if (String.Equals(value, "Mouse 3", StringComparison.OrdinalIgnoreCase))
            {
                flags = pressed ? NativeMethods.MOUSEEVENTF_MIDDLEDOWN : NativeMethods.MOUSEEVENTF_MIDDLEUP;
            }
            else if (String.Equals(value, "Wheel Up", StringComparison.OrdinalIgnoreCase))
            {
                if (!pressed) return;
                flags = NativeMethods.MOUSEEVENTF_WHEEL;
                mouseData = NativeMethods.WHEEL_DELTA;
            }
            else if (String.Equals(value, "Wheel Down", StringComparison.OrdinalIgnoreCase))
            {
                if (!pressed) return;
                flags = NativeMethods.MOUSEEVENTF_WHEEL;
                mouseData = unchecked((uint)-NativeMethods.WHEEL_DELTA);
            }
            else
            {
                return;
            }

            var input = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_MOUSE,
                data = new NativeMethods.INPUTUNION
                {
                    mouse = new NativeMethods.MOUSEINPUT
                    {
                        flags = flags,
                        mouseData = mouseData
                    }
                }
            };
            NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }
    }
}
