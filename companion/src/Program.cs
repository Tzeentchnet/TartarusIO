using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TartarusIO.Companion
{
    internal static class Program
    {
        private const string ApplicationId = "TartarusIO.Companion";

        [STAThread]
        private static int Main(string[] args)
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TartarusIO");
            Logger.Initialize(dataDirectory);

            if (args.Length > 0 && String.Equals(args[0], "--configure-hidhide", StringComparison.OrdinalIgnoreCase))
            {
                return new HidHideService(Process.GetCurrentProcess().MainModule.FileName).ConfigureElevated();
            }

            bool created;
            using (var mutex = new Mutex(true, ApplicationId, out created))
            {
                if (!created)
                {
                    TryOpenExistingInterface();
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    var webRoot = ResolveWebRoot(args);
                    Application.Run(new CompanionApplicationContext(webRoot, dataDirectory));
                    return 0;
                }
                catch (Exception exception)
                {
                    Logger.Error("The companion stopped unexpectedly.", exception);
                    MessageBox.Show(
                        "Tartarus IO Companion could not start.\r\n\r\n" + exception.Message,
                        "Tartarus IO",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return 1;
                }
            }
        }

        private static string ResolveWebRoot(string[] args)
        {
            for (var index = 0; index < args.Length - 1; index += 1)
            {
                if (String.Equals(args[index], "--root", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(args[index + 1]);
                }
            }

            var executableDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var inferredRoot = Path.GetFullPath(Path.Combine(executableDirectory, "..", ".."));
            if (File.Exists(Path.Combine(inferredRoot, "index.html")))
            {
                return inferredRoot;
            }

            throw new DirectoryNotFoundException(
                "The Tartarus IO web files were not found. Start the companion with --root <project folder>.");
        }

        private static void TryOpenExistingInterface()
        {
            try
            {
                Process.Start("http://127.0.0.1:" + LoopbackServer.Port + "/");
            }
            catch
            {
                // The existing instance remains available from its tray icon.
            }
        }
    }

    public sealed class CompanionApplicationContext : ApplicationContext
    {
        private readonly ConfigurationStore _configurationStore;
        private readonly RawInputHost _rawInput;
        private readonly ChromaLightingService _lighting;
        private readonly InputEmitter _inputEmitter;
        private readonly ApplicationCatalogService _applicationCatalog;
        private readonly ProfileAutoSwitcher _profileAutoSwitcher;
        private readonly HidHideService _hidHide;
        private readonly LoopbackServer _server;
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _remappingMenuItem;
        private readonly System.Windows.Forms.Timer _deviceTimer;
        private volatile HidHideStatus _lastDriverStatus;
        private int _maintenanceRefreshActive;

        public CompanionApplicationContext(string webRoot, string dataDirectory)
        {
            _configurationStore = new ConfigurationStore(dataDirectory);
            _rawInput = new RawInputHost();
            _profileAutoSwitcher = new ProfileAutoSwitcher(_configurationStore);
            _lighting = new ChromaLightingService(_configurationStore, _rawInput);
            _inputEmitter = new InputEmitter(
                _configurationStore,
                ActivateProfileByName);
            _applicationCatalog = new ApplicationCatalogService();
            _hidHide = new HidHideService(Process.GetCurrentProcess().MainModule.FileName);
            _lastDriverStatus = _hidHide.GetStatus();
            _profileAutoSwitcher.Refresh();
            _lighting.ApplyActiveProfile(true);

            _rawInput.InputReceived += OnInputReceived;
            _rawInput.DeviceChanged += OnDeviceChanged;

            _remappingMenuItem = new ToolStripMenuItem("Enable remapping");
            _remappingMenuItem.Click += delegate
            {
                var result = SetRemapping(!_inputEmitter.Enabled);
                if (!result.ok)
                {
                    ShowBalloon("Remapping unavailable", result.message, ToolTipIcon.Warning);
                }
                UpdateTrayState();
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Tartarus IO", null, delegate { OpenInterface(); });
            menu.Items.Add(_remappingMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitThread(); });

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Text = "Tartarus IO Companion";
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += delegate { OpenInterface(); };

            _server = new LoopbackServer(
                webRoot,
                GetStatus,
                _configurationStore.GetRawConfiguration,
                SaveConfiguration,
                _applicationCatalog.GetCatalog,
                _applicationCatalog.BrowseForExecutable,
                ActivateProfile,
                SetRemapping,
                _hidHide.LaunchElevatedConfiguration);
            _server.Start();

            _deviceTimer = new System.Windows.Forms.Timer();
            _deviceTimer.Interval = 2000;
            _deviceTimer.Tick += delegate
            {
                _rawInput.RefreshDevices();
                QueueMaintenanceRefresh();
                if (_inputEmitter.Enabled && (!_rawInput.IsConnected || !_lastDriverStatus.ready))
                {
                    SetRemapping(false);
                }
                UpdateTrayState();
            };
            _deviceTimer.Start();

            UpdateTrayState();
            OpenInterface();
            if (_rawInput.IsConnected)
            {
                ShowBalloon(
                    "Tartarus Chroma connected",
                    "The keypad is available. Profiles are stored by Tartarus IO because this model has no onboard memory.",
                    ToolTipIcon.Info);
            }
        }

        protected override void ExitThreadCore()
        {
            _deviceTimer.Stop();
            _inputEmitter.Enabled = false;
            _inputEmitter.StopAll();
            _lighting.Dispose();
            _server.Dispose();
            _rawInput.Dispose();
            _inputEmitter.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            base.ExitThreadCore();
        }

        private void QueueMaintenanceRefresh()
        {
            if (Interlocked.CompareExchange(ref _maintenanceRefreshActive, 1, 0) != 0)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var driverStatus = _hidHide.GetStatus();
                    _lastDriverStatus = driverStatus;
                    if (_inputEmitter.Enabled && !driverStatus.ready)
                    {
                        _inputEmitter.Enabled = false;
                        _inputEmitter.StopAll();
                    }

                    if (_profileAutoSwitcher.Refresh())
                    {
                        _inputEmitter.StopAll();
                        _lighting.ApplyActiveProfile(true);
                    }
                }
                catch (Exception exception)
                {
                    Logger.Error("Companion maintenance refresh failed.", exception);
                }
                finally
                {
                    Interlocked.Exchange(ref _maintenanceRefreshActive, 0);
                }
            });
        }

        private CompanionStatus GetStatus()
        {
            return new CompanionStatus
            {
                version = "0.4.0",
                companion = true,
                device = new DeviceStatus
                {
                    connected = _rawInput.IsConnected,
                    name = "Razer Tartarus Chroma",
                    vendorId = "1532",
                    productId = "0208",
                    devicePath = _rawInput.DevicePath,
                    interfaceCount = _rawInput.InterfaceCount
                },
                lighting = _lighting.GetStatus(),
                driver = _lastDriverStatus,
                remapping = new RemappingStatus
                {
                    enabled = _inputEmitter.Enabled,
                    canEnable = _rawInput.IsConnected && _lastDriverStatus.ready && _configurationStore.HasConfiguration,
                    message = GetRemappingMessage()
                },
                profileSwitch = _profileAutoSwitcher.GetStatus(),
                lastInput = _rawInput.LastInput,
                noOnboardMemory = true,
                profileSource = "Tartarus IO software profile"
            };
        }

        private void SaveConfiguration(string json)
        {
            _configurationStore.Save(json);
            var profileChanged = _profileAutoSwitcher.Refresh();
            if (profileChanged)
            {
                _inputEmitter.StopAll();
            }
            _lighting.ApplyActiveProfile(profileChanged);
            UpdateTrayState();
        }

        private ApiResult ActivateProfile(string profileId)
        {
            var result = _profileAutoSwitcher.ActivateManually(profileId);
            if (result.ok)
            {
                _inputEmitter.StopAll();
                _lighting.ApplyActiveProfile(true);
                UpdateTrayState();
            }
            return result;
        }

        private bool ActivateProfileByName(string profileName)
        {
            var result = _profileAutoSwitcher.ActivateManuallyByName(profileName);
            if (result)
            {
                _inputEmitter.StopAll();
                _lighting.ApplyActiveProfile(true);
                UpdateTrayState();
            }
            return result;
        }

        private ApiResult SetRemapping(bool enabled)
        {
            if (!enabled)
            {
                _inputEmitter.Enabled = false;
                _inputEmitter.StopAll();
                return new ApiResult { ok = true, message = "Remapping stopped." };
            }

            _rawInput.RefreshDevices();
            _lastDriverStatus = _hidHide.GetStatus();
            if (!_rawInput.IsConnected)
            {
                return new ApiResult { ok = false, message = "The Tartarus Chroma is not connected." };
            }
            if (!_configurationStore.HasConfiguration)
            {
                return new ApiResult { ok = false, message = "Save a profile before enabling remapping." };
            }
            if (!_lastDriverStatus.ready)
            {
                return new ApiResult { ok = false, message = _lastDriverStatus.message };
            }

            _inputEmitter.Enabled = true;
            return new ApiResult { ok = true, message = "System-wide remapping is active." };
        }

        private void OnInputReceived(object sender, TartarusInputEventArgs eventArgs)
        {
            _inputEmitter.HandleControl(eventArgs.ControlId, eventArgs.Pressed);
            _lighting.HandleInput(eventArgs.Pressed);
        }

        private void OnDeviceChanged(object sender, EventArgs eventArgs)
        {
            _lighting.HandleDeviceChanged();
            UpdateTrayState();
            if (!_rawInput.IsConnected)
            {
                SetRemapping(false);
                ShowBalloon("Tartarus Chroma disconnected", "Remapping was stopped safely.", ToolTipIcon.Warning);
            }
        }

        private string GetRemappingMessage()
        {
            if (_inputEmitter.Enabled)
            {
                return "System-wide remapping is active.";
            }
            if (!_rawInput.IsConnected)
            {
                return "Connect the Tartarus Chroma to continue.";
            }
            if (!_lastDriverStatus.ready)
            {
                return _lastDriverStatus.message;
            }
            if (!_configurationStore.HasConfiguration)
            {
                return "Save a profile to the companion.";
            }
            return "Ready to enable system-wide remapping.";
        }

        private void UpdateTrayState()
        {
            _remappingMenuItem.Checked = _inputEmitter.Enabled;
            _remappingMenuItem.Text = _inputEmitter.Enabled ? "Disable remapping" : "Enable remapping";
            _notifyIcon.Text = _inputEmitter.Enabled
                ? "Tartarus IO - Remapping active"
                : _rawInput.IsConnected
                    ? "Tartarus IO - Chroma connected"
                    : "Tartarus IO - Device disconnected";
        }

        private void OpenInterface()
        {
            try
            {
                Process.Start("http://127.0.0.1:" + LoopbackServer.Port + "/");
            }
            catch (Exception exception)
            {
                Logger.Error("The interface could not be opened.", exception);
            }
        }

        private void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = text;
            _notifyIcon.BalloonTipIcon = icon;
            _notifyIcon.ShowBalloonTip(3500);
        }
    }
}
