using System;
using System.Collections.Generic;

namespace TartarusIO.Companion
{
    public sealed class AppConfiguration
    {
        public int schemaVersion { get; set; }
        public List<ProfileConfiguration> profiles { get; set; }
        public string activeProfileId { get; set; }
        public string selectedProfileId { get; set; }
        public string defaultProfileId { get; set; }
        public string selectedControlId { get; set; }
        public string activeTab { get; set; }
        public string bindingType { get; set; }
        public string selectedMacroId { get; set; }
        public bool dirty { get; set; }
        public List<MacroConfiguration> macros { get; set; }
    }

    public sealed class ProfileConfiguration
    {
        public string id { get; set; }
        public string name { get; set; }
        public string category { get; set; }
        public string description { get; set; }
        public string app { get; set; }
        public string appPath { get; set; }
        public string appDisplayName { get; set; }
        public bool autoSwitch { get; set; }
        public string accent { get; set; }
        public string accentRgb { get; set; }
        public string glyph { get; set; }
        public Dictionary<string, BindingConfiguration> mappings { get; set; }
        public LightingConfiguration lighting { get; set; }
    }

    public sealed class BindingConfiguration
    {
        public string type { get; set; }
        public string value { get; set; }
        public string description { get; set; }
        public List<string> modifiers { get; set; }
        public string trigger { get; set; }
    }

    public sealed class LightingConfiguration
    {
        public bool enabled { get; set; }
        public string effect { get; set; }
        public string color { get; set; }
        public string colorName { get; set; }
        public int brightness { get; set; }
        public bool saveWithProfile { get; set; }
    }

    public sealed class MacroConfiguration
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public List<MacroStepConfiguration> steps { get; set; }
    }

    public sealed class MacroStepConfiguration
    {
        public string action { get; set; }
        public int delay { get; set; }
    }

    public sealed class CompanionStatus
    {
        public string version { get; set; }
        public bool companion { get; set; }
        public DeviceStatus device { get; set; }
        public LightingStatus lighting { get; set; }
        public HidHideStatus driver { get; set; }
        public RemappingStatus remapping { get; set; }
        public ProfileSwitchStatus profileSwitch { get; set; }
        public InputEventStatus lastInput { get; set; }
        public bool noOnboardMemory { get; set; }
        public string profileSource { get; set; }
    }

    public sealed class DeviceStatus
    {
        public bool connected { get; set; }
        public string name { get; set; }
        public string vendorId { get; set; }
        public string productId { get; set; }
        public string devicePath { get; set; }
        public int interfaceCount { get; set; }
    }

    public sealed class HidHideStatus
    {
        public bool installed { get; set; }
        public bool cliAvailable { get; set; }
        public bool cloakActive { get; set; }
        public bool appRegistered { get; set; }
        public bool deviceHidden { get; set; }
        public bool ready { get; set; }
        public string message { get; set; }
        public string downloadUrl { get; set; }
    }

    public sealed class LightingStatus
    {
        public bool available { get; set; }
        public bool applied { get; set; }
        public string profileId { get; set; }
        public string effect { get; set; }
        public string message { get; set; }
    }

    public sealed class RemappingStatus
    {
        public bool enabled { get; set; }
        public bool canEnable { get; set; }
        public string message { get; set; }
    }

    public sealed class ProfileSwitchStatus
    {
        public string activeProfileId { get; set; }
        public string fallbackProfileId { get; set; }
        public bool automatic { get; set; }
        public bool manualOverride { get; set; }
        public bool usingFallback { get; set; }
        public string matchedApplication { get; set; }
        public string message { get; set; }
    }

    public sealed class InputEventStatus
    {
        public long sequence { get; set; }
        public string controlId { get; set; }
        public bool pressed { get; set; }
        public string timestamp { get; set; }
    }

    public sealed class ApiResult
    {
        public bool ok { get; set; }
        public string message { get; set; }
    }

    public sealed class RemappingRequest
    {
        public bool enabled { get; set; }
    }

    public sealed class ProfileActivationRequest
    {
        public string profileId { get; set; }
    }

    public sealed class ApplicationCatalog
    {
        public string generatedAt { get; set; }
        public List<ApplicationCatalogItem> steamGames { get; set; }
        public List<ApplicationCatalogItem> installedApplications { get; set; }
    }

    public sealed class ApplicationCatalogItem
    {
        public string id { get; set; }
        public string name { get; set; }
        public string executableName { get; set; }
        public string path { get; set; }
        public string installDirectory { get; set; }
        public string source { get; set; }
        public string steamAppId { get; set; }
        public bool selectable { get; set; }
    }

    public sealed class ApplicationBrowseRequest
    {
        public string initialDirectory { get; set; }
    }

    public sealed class ApplicationBrowseResult
    {
        public bool selected { get; set; }
        public ApplicationCatalogItem application { get; set; }
        public string message { get; set; }
    }
}
