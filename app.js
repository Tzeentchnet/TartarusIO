"use strict";

const STORAGE_KEY = "tartarus-io-state-v3";
const LEGACY_STORAGE_KEYS = ["tartarus-io-state-v2", "tartarus-io-state-v1"];

const controls = [
  { id: "01", number: "01", name: "Key 01", defaultValue: "Tab", defaultDescription: "Tab" },
  { id: "02", number: "02", name: "Key 02", defaultValue: "Q", defaultDescription: "Q" },
  { id: "03", number: "03", name: "Key 03", defaultValue: "W", defaultDescription: "W" },
  { id: "04", number: "04", name: "Key 04", defaultValue: "E", defaultDescription: "E" },
  { id: "05", number: "05", name: "Key 05", defaultValue: "R", defaultDescription: "R" },
  { id: "06", number: "06", name: "Key 06", defaultValue: "Caps", defaultDescription: "Caps Lock" },
  { id: "07", number: "07", name: "Key 07", defaultValue: "A", defaultDescription: "A" },
  { id: "08", number: "08", name: "Key 08", defaultValue: "S", defaultDescription: "S" },
  { id: "09", number: "09", name: "Key 09", defaultValue: "D", defaultDescription: "D" },
  { id: "10", number: "10", name: "Key 10", defaultValue: "F", defaultDescription: "F" },
  { id: "11", number: "11", name: "Key 11", defaultValue: "Shift", defaultDescription: "Shift" },
  { id: "12", number: "12", name: "Key 12", defaultValue: "Z", defaultDescription: "Z" },
  { id: "13", number: "13", name: "Key 13", defaultValue: "X", defaultDescription: "X" },
  { id: "14", number: "14", name: "Key 14", defaultValue: "C", defaultDescription: "C" },
  { id: "15", number: "15", name: "Key 15", defaultValue: "V", defaultDescription: "V" },
  { id: "dpad-n", number: "D1", name: "Thumbpad up", defaultValue: "Up", defaultDescription: "Navigate up", angle: 0 },
  { id: "dpad-ne", number: "D2", name: "Thumbpad up-right", defaultValue: "Up + Right", defaultDescription: "Diagonal", angle: 45 },
  { id: "dpad-e", number: "D3", name: "Thumbpad right", defaultValue: "Right", defaultDescription: "Navigate right", angle: 90 },
  { id: "dpad-se", number: "D4", name: "Thumbpad down-right", defaultValue: "Down + Right", defaultDescription: "Diagonal", angle: 135 },
  { id: "dpad-s", number: "D5", name: "Thumbpad down", defaultValue: "Down", defaultDescription: "Navigate down", angle: 180 },
  { id: "dpad-sw", number: "D6", name: "Thumbpad down-left", defaultValue: "Down + Left", defaultDescription: "Diagonal", angle: 225 },
  { id: "dpad-w", number: "D7", name: "Thumbpad left", defaultValue: "Left", defaultDescription: "Navigate left", angle: 270 },
  { id: "dpad-nw", number: "D8", name: "Thumbpad up-left", defaultValue: "Up + Left", defaultDescription: "Diagonal", angle: 315 },
  { id: "thumb", number: "16", name: "Upper thumb button", defaultValue: "Alt", defaultDescription: "Alternate action" },
  { id: "paddle", number: "P", name: "Thumb paddle", defaultValue: "Space", defaultDescription: "Jump" },
];

const palette = [
  { color: "#68f59a", name: "Signal green" },
  { color: "#83f3ff", name: "Ion blue" },
  { color: "#a989ff", name: "Ultraviolet" },
  { color: "#ff6b8b", name: "Ember pink" },
  { color: "#ffb55f", name: "Warm amber" },
];

const quickBindingsByType = {
  Keyboard: ["W", "Space", "Shift", "E", "Tab"],
  Mouse: ["Mouse 1", "Mouse 2", "Mouse 3", "Wheel Up", "Wheel Down"],
  Macro: ["Quick ping", "Armor swap", "Push to talk"],
  Profile: ["Night Ops", "Raid Healer", "Creative Suite"],
};

const macroActionOptions = [
  "Key down: Alt",
  "Key up: Alt",
  "Key down: V",
  "Key up: V",
  "Key press: Mouse 1",
  "Key press: Mouse 2",
  "Key press: Q",
  "Key press: E",
  "Key press: R",
  "Key press: Space",
  "Key press: V",
];

function createBaseMappings() {
  return Object.fromEntries(
    controls.map((control) => [
      control.id,
      {
        type: "Keyboard",
        value: control.defaultValue,
        description: control.defaultDescription,
        modifiers: [],
        trigger: "Press",
      },
    ]),
  );
}

function overrideMappings(base, overrides) {
  const copy = structuredClone(base);
  Object.entries(overrides).forEach(([key, value]) => {
    copy[key] = { ...copy[key], ...value };
  });
  return copy;
}

function createDefaultProfiles() {
  const base = createBaseMappings();
  return [
    {
      id: "night-ops",
      name: "Night Ops",
      category: "First-person shooter",
      description: "Fast movement, clean comms, zero reach.",
      app: "game-client.exe",
      appPath: "",
      appDisplayName: "game-client.exe",
      autoSwitch: true,
      accent: "#68f59a",
      accentRgb: "104, 245, 154",
      glyph: "NO",
      mappings: overrideMappings(base, {
        "01": { value: "Esc", description: "Game menu" },
        "02": { value: "1", description: "Primary weapon" },
        "03": { value: "2", description: "Secondary weapon" },
        "04": { value: "3", description: "Utility" },
        "05": { type: "Macro", value: "Quick ping", description: "Quick ping macro" },
        "06": { value: "Tab", description: "Scoreboard" },
        "07": { value: "Q", description: "Tactical ability" },
        "08": { value: "W", description: "Move forward" },
        "09": { value: "E", description: "Interact / use" },
        "10": { value: "R", description: "Reload" },
        "11": { value: "Shift", description: "Sprint", trigger: "Hold" },
        "12": { value: "A", description: "Strafe left", trigger: "Hold" },
        "13": { value: "S", description: "Move backward", trigger: "Hold" },
        "14": { value: "D", description: "Strafe right", trigger: "Hold" },
        "15": { type: "Macro", value: "Push to talk", description: "Team voice" },
        "dpad-n": { value: "Up", description: "Inventory up" },
        "dpad-e": { value: "Right", description: "Inventory right" },
        "dpad-s": { value: "Down", description: "Inventory down" },
        "dpad-w": { value: "Left", description: "Inventory left" },
        thumb: { type: "Mouse", value: "Wheel Down", description: "Cycle weapon" },
        paddle: { value: "Space", description: "Jump / mantle", trigger: "Hold" },
      }),
      lighting: {
        enabled: true,
        effect: "Reactive",
        color: "#68f59a",
        colorName: "Signal green",
        brightness: 74,
        saveWithProfile: true,
      },
    },
    {
      id: "raid-healer",
      name: "Raid Healer",
      category: "Massively multiplayer",
      description: "Core heals, targeting, and cooldowns at a glance.",
      app: "mmo-client.exe",
      appPath: "",
      appDisplayName: "mmo-client.exe",
      autoSwitch: true,
      accent: "#83f3ff",
      accentRgb: "131, 243, 255",
      glyph: "RH",
      mappings: overrideMappings(base, {
        "01": { value: "Esc", description: "Game menu" },
        "02": { value: "1", description: "Fast heal" },
        "03": { value: "2", description: "Main heal" },
        "04": { value: "3", description: "Group heal" },
        "05": { value: "4", description: "Cleanse" },
        "06": { value: "F1", description: "Target self" },
        "07": { value: "F2", description: "Party member 1" },
        "08": { value: "F3", description: "Party member 2" },
        "09": { value: "F4", description: "Party member 3" },
        "10": { value: "F5", description: "Party member 4" },
        "11": { value: "Shift", description: "Alternate bar", trigger: "Hold" },
        "12": { value: "Q", description: "Defensive cooldown" },
        "13": { value: "E", description: "Emergency heal" },
        "14": { value: "R", description: "Raid cooldown" },
        "15": { value: "T", description: "Target focus" },
        paddle: { value: "Space", description: "Jump" },
      }),
      lighting: {
        enabled: true,
        effect: "Breathing",
        color: "#83f3ff",
        colorName: "Ion blue",
        brightness: 62,
        saveWithProfile: true,
      },
    },
    {
      id: "creative-suite",
      name: "Creative Suite",
      category: "Productivity",
      description: "Editing shortcuts without leaving the canvas.",
      app: "design-app.exe",
      appPath: "",
      appDisplayName: "design-app.exe",
      autoSwitch: true,
      accent: "#a989ff",
      accentRgb: "169, 137, 255",
      glyph: "CS",
      mappings: overrideMappings(base, {
        "01": { value: "Z", description: "Undo", modifiers: ["Ctrl"] },
        "02": { value: "Z", description: "Redo", modifiers: ["Ctrl", "Shift"] },
        "03": { value: "S", description: "Save", modifiers: ["Ctrl"] },
        "04": { value: "C", description: "Copy", modifiers: ["Ctrl"] },
        "05": { value: "V", description: "Paste", modifiers: ["Ctrl"] },
        "06": { value: "B", description: "Brush tool" },
        "07": { value: "V", description: "Move tool" },
        "08": { value: "H", description: "Hand tool", trigger: "Hold" },
        "09": { value: "I", description: "Eyedropper" },
        "10": { value: "E", description: "Eraser" },
        "11": { value: "[", description: "Smaller brush" },
        "12": { value: "]", description: "Larger brush" },
        "13": { value: "-", description: "Zoom out" },
        "14": { value: "+", description: "Zoom in" },
        "15": { value: "0", description: "Fit canvas" },
        thumb: { type: "Mouse", value: "Wheel Up", description: "Zoom canvas" },
        paddle: { value: "Space", description: "Pan canvas", trigger: "Hold" },
      }),
      lighting: {
        enabled: true,
        effect: "Static",
        color: "#a989ff",
        colorName: "Ultraviolet",
        brightness: 54,
        saveWithProfile: true,
      },
    },
    {
      id: "desktop",
      name: "Desktop",
      category: "Everyday",
      description: "A quiet default for browsing and daily work.",
      app: "",
      appPath: "",
      appDisplayName: "",
      autoSwitch: false,
      accent: "#ffb55f",
      accentRgb: "255, 181, 95",
      glyph: "DT",
      mappings: overrideMappings(base, {
        "01": { value: "Esc", description: "Cancel / close" },
        "02": { value: "1", description: "Workspace 1", modifiers: ["Ctrl"] },
        "03": { value: "2", description: "Workspace 2", modifiers: ["Ctrl"] },
        "04": { value: "3", description: "Workspace 3", modifiers: ["Ctrl"] },
        "05": { value: "4", description: "Workspace 4", modifiers: ["Ctrl"] },
        "06": { value: "Tab", description: "Next field" },
        "07": { value: "T", description: "New tab", modifiers: ["Ctrl"] },
        "08": { value: "Page Up", description: "Page up" },
        "09": { value: "L", description: "Address bar", modifiers: ["Ctrl"] },
        "10": { value: "R", description: "Refresh", modifiers: ["Ctrl"] },
        "11": { value: "Shift", description: "Selection modifier", trigger: "Hold" },
        "12": { value: "Home", description: "Start of page" },
        "13": { value: "Page Down", description: "Page down" },
        "14": { value: "End", description: "End of page" },
        "15": { value: "F", description: "Find", modifiers: ["Ctrl"] },
        thumb: { type: "Mouse", value: "Wheel Down", description: "Scroll page" },
        paddle: { value: "Enter", description: "Open selection" },
      }),
      lighting: {
        enabled: true,
        effect: "Static",
        color: "#ffb55f",
        colorName: "Warm amber",
        brightness: 35,
        saveWithProfile: true,
      },
    },
  ];
}

function createDefaultMacros() {
  return [
    {
      id: "quick-ping",
      name: "Quick ping",
      description: "Open ping wheel and mark the current target.",
      steps: [
        { action: "Key down: Alt", delay: 60 },
        { action: "Key press: Mouse 1", delay: 60 },
        { action: "Key up: Alt", delay: 60 },
      ],
    },
    {
      id: "armor-swap",
      name: "Armor swap",
      description: "Preview a short inventory interaction sequence.",
      steps: [
        { action: "Key press: E", delay: 120 },
        { action: "Key press: Mouse 1", delay: 80 },
        { action: "Key press: E", delay: 80 },
        { action: "Key press: Mouse 1", delay: 60 },
      ],
    },
    {
      id: "push-to-talk",
      name: "Push to talk",
      description: "Hold the voice channel while the key is active.",
      steps: [
        { action: "Key down: V", delay: 100 },
        { action: "Key up: V", delay: 0 },
      ],
    },
  ];
}

function createDefaultState() {
  return {
    schemaVersion: 3,
    profiles: createDefaultProfiles(),
    activeProfileId: "night-ops",
    selectedProfileId: "night-ops",
    defaultProfileId: "desktop",
    selectedControlId: "08",
    activeTab: "mapping",
    bindingType: "Keyboard",
    selectedMacroId: "quick-ping",
    dirty: false,
    macros: createDefaultMacros(),
  };
}

function normalizeState(parsed) {
  const defaults = createDefaultState();
  const profiles = Array.isArray(parsed?.profiles) && parsed.profiles.length ? parsed.profiles : defaults.profiles;
  const baseMappings = createBaseMappings();

  profiles.forEach((profile) => {
    const defaultLighting =
      defaults.profiles.find((item) => item.id === profile.id)?.lighting || defaults.profiles[0].lighting;
    const savedLighting =
      profile.lighting && typeof profile.lighting === "object" ? profile.lighting : {};
    const brightness = Number(savedLighting.brightness);
    const effect = ["Static", "Breathing", "Reactive", "Spectrum"].includes(savedLighting.effect)
      ? savedLighting.effect
      : defaultLighting.effect;
    const color =
      typeof savedLighting.color === "string" && /^#[0-9a-f]{6}$/i.test(savedLighting.color)
        ? savedLighting.color
        : defaultLighting.color;
    profile.lighting = {
      ...structuredClone(defaultLighting),
      ...savedLighting,
      enabled: savedLighting.enabled !== false,
      effect,
      color,
      colorName:
        typeof savedLighting.colorName === "string" && savedLighting.colorName
          ? savedLighting.colorName
          : "Custom color",
      brightness: Number.isFinite(brightness) ? Math.max(0, Math.min(100, brightness)) : defaultLighting.brightness,
      saveWithProfile: savedLighting.saveWithProfile !== false,
    };
    profile.app = typeof profile.app === "string" ? profile.app : "";
    profile.appPath = typeof profile.appPath === "string" ? profile.appPath : "";
    profile.appDisplayName =
      typeof profile.appDisplayName === "string" && profile.appDisplayName
        ? profile.appDisplayName
        : profile.app;
    profile.autoSwitch = Boolean(profile.autoSwitch && profile.app);
    profile.mappings ||= {};
    if (profile.mappings.wheel && !profile.mappings.thumb) {
      const legacyBinding = profile.mappings.wheel;
      profile.mappings.thumb =
        legacyBinding.value === "Mouse Wheel" || legacyBinding.value === "Wheel Up / Down"
          ? structuredClone(baseMappings.thumb)
          : legacyBinding;
      delete profile.mappings.wheel;
    }
    controls.forEach((control) => {
      profile.mappings[control.id] ||= structuredClone(baseMappings[control.id]);
    });
  });

  const macros = Array.isArray(parsed?.macros) && parsed.macros.length ? parsed.macros : defaults.macros;
  const activeProfileExists = profiles.some((profile) => profile.id === parsed?.activeProfileId);
  const selectedProfileExists = profiles.some((profile) => profile.id === parsed?.selectedProfileId);
  const defaultProfileExists = profiles.some((profile) => profile.id === parsed?.defaultProfileId);
  const fallbackProfile =
    profiles.find((profile) => profile.id === "desktop") ||
    profiles.find((profile) => !profile.app) ||
    profiles[0];
  const selectedMacroExists = macros.some((macro) => macro.id === parsed?.selectedMacroId);
  const selectedControlId = parsed?.selectedControlId === "wheel" ? "thumb" : parsed?.selectedControlId;

  return {
    ...defaults,
    ...parsed,
    schemaVersion: 3,
    profiles,
    macros,
    activeProfileId: activeProfileExists ? parsed.activeProfileId : fallbackProfile.id,
    selectedProfileId: selectedProfileExists
      ? parsed.selectedProfileId
      : activeProfileExists
        ? parsed.activeProfileId
        : fallbackProfile.id,
    defaultProfileId: defaultProfileExists ? parsed.defaultProfileId : fallbackProfile.id,
    selectedControlId: controls.some((control) => control.id === selectedControlId) ? selectedControlId : "08",
    selectedMacroId: selectedMacroExists ? parsed.selectedMacroId : macros[0].id,
  };
}

function loadState() {
  const raw =
    localStorage.getItem(STORAGE_KEY) ||
    LEGACY_STORAGE_KEYS.map((key) => localStorage.getItem(key)).find(Boolean);
  if (!raw) {
    return createDefaultState();
  }

  try {
    return normalizeState(JSON.parse(raw));
  } catch (error) {
    console.warn("Tartarus IO could not restore the saved configuration.", error);
    localStorage.removeItem(STORAGE_KEY);
    LEGACY_STORAGE_KEYS.forEach((key) => localStorage.removeItem(key));
    return createDefaultState();
  }
}

let state = loadState();
if (!state.macros) {
  state.macros = createDefaultMacros();
}

let draft = null;
let toastTimer = null;
let recording = false;
let playingMacro = false;
let editingProfileId = null;
let profileSourceId = null;
let profileApplicationDraft = null;
let applicationCatalog = null;
let applicationCatalogPromise = null;
let applicationOptions = new Map();
let companionStatus = null;
let companionSyncTimer = null;
let companionPollTimer = null;
let lastInputSequence = 0;
let isHydratingFromCompanion = false;

const usesCompanion =
  location.protocol === "http:" &&
  (location.hostname === "127.0.0.1" || location.hostname === "localhost") &&
  location.port === "47651";

const elements = {
  profileList: document.querySelector("#profileList"),
  profileCount: document.querySelector("#profileCount"),
  profileSearch: document.querySelector("#profileSearch"),
  newProfileButton: document.querySelector("#newProfileButton"),
  profileTitle: document.querySelector("#profileTitle"),
  profileDescription: document.querySelector("#profileDescription"),
  profileCategory: document.querySelector("#profileCategory"),
  profileSwitchLabel: document.querySelector("#profileSwitchLabel"),
  profileAccentDot: document.querySelector("#profileAccentDot"),
  linkedApplication: document.querySelector("#linkedApplication"),
  editProfileButton: document.querySelector("#editProfileButton"),
  saveState: document.querySelector("#saveState"),
  saveStateLabel: document.querySelector("#saveStateLabel"),
  resetButton: document.querySelector("#resetButton"),
  exportButton: document.querySelector("#exportButton"),
  applyButton: document.querySelector("#applyButton"),
  deviceStatusButton: document.querySelector("#deviceStatusButton"),
  deviceName: document.querySelector("#deviceName"),
  deviceStatusDot: document.querySelector("#deviceStatusDot"),
  deviceStatusLabel: document.querySelector("#deviceStatusLabel"),
  connectionNote: document.querySelector("#connectionNote"),
  connectionNoteTitle: document.querySelector("#connectionNoteTitle"),
  connectionNoteBody: document.querySelector("#connectionNoteBody"),
  tabs: [...document.querySelectorAll(".workspace-tab")],
  panels: [...document.querySelectorAll(".tab-panel")],
  keyGrid: document.querySelector("#keyGrid"),
  dpad: document.querySelector("#dpad"),
  deviceStage: document.querySelector("#deviceStage"),
  deviceHint: document.querySelector("#deviceHint"),
  mappedCount: document.querySelector("#mappedCount"),
  macroMappedCount: document.querySelector("#macroMappedCount"),
  testModeButton: document.querySelector("#testModeButton"),
  selectedControlBadge: document.querySelector("#selectedControlBadge"),
  selectedControlTitle: document.querySelector("#selectedControlTitle"),
  currentBindingValue: document.querySelector("#currentBindingValue"),
  currentBindingDescription: document.querySelector("#currentBindingDescription"),
  bindingTypes: document.querySelector("#bindingTypes"),
  bindingValue: document.querySelector("#bindingValue"),
  bindingPreview: document.querySelector("#bindingPreview"),
  quickBindings: document.querySelector("#quickBindings"),
  modifierRow: document.querySelector("#modifierRow"),
  triggerMode: document.querySelector("#triggerMode"),
  recordButton: document.querySelector("#recordButton"),
  rulesNote: document.querySelector("#rulesNote"),
  assignButton: document.querySelector("#assignButton"),
  assignTarget: document.querySelector("#assignTarget"),
  lightingEnabled: document.querySelector("#lightingEnabled"),
  lightingStage: document.querySelector("#lightingStage"),
  lightingKeyGrid: document.querySelector("#lightingKeyGrid"),
  lightingEffectLabel: document.querySelector("#lightingEffectLabel"),
  lightingColorLabel: document.querySelector("#lightingColorLabel"),
  effectGrid: document.querySelector("#effectGrid"),
  colorPalette: document.querySelector("#colorPalette"),
  customColor: document.querySelector("#customColor"),
  brightnessRange: document.querySelector("#brightnessRange"),
  brightnessValue: document.querySelector("#brightnessValue"),
  saveLightingWithProfile: document.querySelector("#saveLightingWithProfile"),
  lightingHardwareState: document.querySelector("#lightingHardwareState"),
  lightingHardwareTitle: document.querySelector("#lightingHardwareTitle"),
  lightingHardwareMessage: document.querySelector("#lightingHardwareMessage"),
  macroCount: document.querySelector("#macroCount"),
  macroList: document.querySelector("#macroList"),
  macroTitle: document.querySelector("#macroTitle"),
  macroDescription: document.querySelector("#macroDescription"),
  macroDuration: document.querySelector("#macroDuration"),
  sequenceList: document.querySelector("#sequenceList"),
  newMacroButton: document.querySelector("#newMacroButton"),
  playMacroButton: document.querySelector("#playMacroButton"),
  deleteMacroButton: document.querySelector("#deleteMacroButton"),
  addStepButton: document.querySelector("#addStepButton"),
  saveMacroButton: document.querySelector("#saveMacroButton"),
  profileDialog: document.querySelector("#profileDialog"),
  profileForm: document.querySelector("#profileForm"),
  dialogCloseButton: document.querySelector("#dialogCloseButton"),
  dialogCancelButton: document.querySelector("#dialogCancelButton"),
  dialogKicker: document.querySelector("#dialogKicker"),
  dialogTitle: document.querySelector("#dialogTitle"),
  dialogDescription: document.querySelector("#dialogDescription"),
  dialogSubmitButton: document.querySelector("#dialogSubmitButton"),
  profileNameInput: document.querySelector("#profileNameInput"),
  profileAppSelect: document.querySelector("#profileAppSelect"),
  profileAppHint: document.querySelector("#profileAppHint"),
  browseApplicationButton: document.querySelector("#browseApplicationButton"),
  profileAutoSwitchInput: document.querySelector("#profileAutoSwitchInput"),
  profileFallbackInput: document.querySelector("#profileFallbackInput"),
  profileFallbackHint: document.querySelector("#profileFallbackHint"),
  deviceDialog: document.querySelector("#deviceDialog"),
  deviceDialogCloseButton: document.querySelector("#deviceDialogCloseButton"),
  deviceDialogIntro: document.querySelector("#deviceDialogIntro"),
  deviceDialogName: document.querySelector("#deviceDialogName"),
  deviceDialogIdentity: document.querySelector("#deviceDialogIdentity"),
  deviceDialogState: document.querySelector("#deviceDialogState"),
  driverStateTitle: document.querySelector("#driverStateTitle"),
  driverStateDescription: document.querySelector("#driverStateDescription"),
  driverCallout: document.querySelector("#driverCallout"),
  driverCalloutTitle: document.querySelector("#driverCalloutTitle"),
  driverCalloutBody: document.querySelector("#driverCalloutBody"),
  driverSetupButton: document.querySelector("#driverSetupButton"),
  remappingToggleButton: document.querySelector("#remappingToggleButton"),
  toast: document.querySelector("#toast"),
  toastTitle: document.querySelector("#toastTitle"),
  toastMessage: document.querySelector("#toastMessage"),
};

function getActiveProfile() {
  return state.profiles.find((profile) => profile.id === state.selectedProfileId) || state.profiles[0];
}

function getRuntimeProfile() {
  return state.profiles.find((profile) => profile.id === state.activeProfileId) || state.profiles[0];
}

function getProfileApplicationLabel(profile) {
  return profile.appDisplayName || profile.app || "";
}

function normalizeApplicationItem(item) {
  if (!item) {
    return null;
  }

  const executableName = String(item.executableName || "").trim();
  const path = String(item.path || "").trim();
  const name = String(item.name || executableName || "Selected application").trim();
  return {
    id: String(item.id || `custom:${path || executableName}`).trim(),
    name,
    executableName,
    path,
    installDirectory: String(item.installDirectory || "").trim(),
    source: String(item.source || "custom").trim(),
    steamAppId: String(item.steamAppId || "").trim(),
    selectable: item.selectable !== false && Boolean(executableName),
  };
}

function getProfileApplication(profile) {
  if (!profile?.app) {
    return null;
  }

  return normalizeApplicationItem({
    id: `profile:${profile.id}`,
    name: getProfileApplicationLabel(profile),
    executableName: profile.app,
    path: profile.appPath,
    installDirectory: profile.appPath ? profile.appPath.replace(/[\\/][^\\/]+$/, "") : "",
    source: "profile",
    selectable: true,
  });
}

function getApplicationIdentity(application) {
  if (!application) {
    return "";
  }
  return (application.path || application.executableName || "").toLowerCase();
}

function getApplicationOptionLabel(application) {
  if (!application.selectable) {
    return `${application.name} — executable not detected`;
  }
  return application.executableName
    ? `${application.name} — ${application.executableName}`
    : application.name;
}

function appendApplicationGroup(label, applications) {
  if (!applications.length) {
    return;
  }

  const group = document.createElement("optgroup");
  group.label = label;
  applications.forEach((rawApplication) => {
    const application = normalizeApplicationItem(rawApplication);
    const option = document.createElement("option");
    option.value = application.id;
    option.textContent = getApplicationOptionLabel(application);
    group.append(option);
    applicationOptions.set(application.id, application);
  });
  elements.profileAppSelect.append(group);
}

function updateApplicationControls() {
  const hasExecutable = Boolean(profileApplicationDraft?.executableName);
  elements.profileAutoSwitchInput.disabled = !hasExecutable;
  if (!hasExecutable) {
    elements.profileAutoSwitchInput.checked = false;
  }

  if (!profileApplicationDraft) {
    elements.profileAppHint.textContent =
      "Choose a Steam game, an installed application, or browse directly to an executable.";
    return;
  }

  if (!profileApplicationDraft.selectable) {
    elements.profileAppHint.textContent =
      "The game was found, but its executable was not detected. Use … to select it from the install folder.";
    return;
  }

  const sourceLabel =
    profileApplicationDraft.source === "steam"
      ? "Steam game"
      : profileApplicationDraft.source === "windows"
        ? "Installed application"
        : "Selected executable";
  elements.profileAppHint.textContent = profileApplicationDraft.path
    ? `${sourceLabel} · ${profileApplicationDraft.path}`
    : `${sourceLabel} · ${profileApplicationDraft.executableName}`;
}

function renderApplicationSelector(loading = false) {
  applicationOptions = new Map();
  elements.profileAppSelect.replaceChildren();

  const noApplication = document.createElement("option");
  noApplication.value = "";
  noApplication.textContent = "No linked application";
  elements.profileAppSelect.append(noApplication);

  if (loading) {
    const loadingOption = document.createElement("option");
    loadingOption.value = "__loading";
    loadingOption.textContent = "Loading installed applications…";
    loadingOption.disabled = true;
    elements.profileAppSelect.append(loadingOption);
  } else if (applicationCatalog) {
    appendApplicationGroup("Steam games", applicationCatalog.steamGames || []);
    appendApplicationGroup("Installed applications", applicationCatalog.installedApplications || []);
  }

  let selectedId = "";
  if (profileApplicationDraft) {
    const identity = getApplicationIdentity(profileApplicationDraft);
    const matchingApplication = [...applicationOptions.values()].find(
      (application) => getApplicationIdentity(application) === identity,
    );
    if (matchingApplication) {
      profileApplicationDraft = matchingApplication;
      selectedId = matchingApplication.id;
    } else {
      const currentGroup = document.createElement("optgroup");
      currentGroup.label = "Current selection";
      const currentOption = document.createElement("option");
      currentOption.value = profileApplicationDraft.id;
      currentOption.textContent = getApplicationOptionLabel(profileApplicationDraft);
      currentGroup.append(currentOption);
      elements.profileAppSelect.append(currentGroup);
      applicationOptions.set(profileApplicationDraft.id, profileApplicationDraft);
      selectedId = profileApplicationDraft.id;
    }
  }

  elements.profileAppSelect.value = selectedId;
  elements.profileAppSelect.disabled = loading;
  elements.browseApplicationButton.disabled = !usesCompanion;
  updateApplicationControls();
}

async function loadApplicationCatalog() {
  if (!usesCompanion) {
    renderApplicationSelector(false);
    return null;
  }
  if (applicationCatalog) {
    renderApplicationSelector(false);
    return applicationCatalog;
  }
  if (applicationCatalogPromise) {
    return applicationCatalogPromise;
  }

  renderApplicationSelector(true);
  applicationCatalogPromise = requestCompanion("/api/applications")
    .then((catalog) => {
      applicationCatalog = {
        steamGames: Array.isArray(catalog.steamGames) ? catalog.steamGames : [],
        installedApplications: Array.isArray(catalog.installedApplications)
          ? catalog.installedApplications
          : [],
      };
      renderApplicationSelector(false);
      return applicationCatalog;
    })
    .catch((error) => {
      console.error("Tartarus IO could not load installed applications.", error);
      renderApplicationSelector(false);
      showToast("Application list unavailable", error.message);
      return null;
    })
    .finally(() => {
      applicationCatalogPromise = null;
    });
  return applicationCatalogPromise;
}

function handleApplicationSelection() {
  const selectedId = elements.profileAppSelect.value;
  profileApplicationDraft = selectedId ? applicationOptions.get(selectedId) || null : null;
  updateApplicationControls();
}

async function browseForApplication() {
  if (!usesCompanion) {
    showToast("Companion not running", "Start Tartarus IO with Start-TartarusIO.cmd to browse for executables.");
    return;
  }

  elements.browseApplicationButton.disabled = true;
  elements.browseApplicationButton.textContent = "…";
  try {
    const result = await requestCompanion("/api/applications/browse", {
      method: "POST",
      body: JSON.stringify({
        initialDirectory: profileApplicationDraft?.installDirectory || "",
      }),
    });
    if (!result.selected || !result.application) {
      return;
    }
    profileApplicationDraft = normalizeApplicationItem(result.application);
    renderApplicationSelector(false);
  } catch (error) {
    console.error("Tartarus IO could not browse for an application.", error);
    showToast("Executable not selected", error.message);
  } finally {
    elements.browseApplicationButton.disabled = !usesCompanion;
    elements.browseApplicationButton.textContent = "…";
  }
}

function getSelectedControl() {
  return controls.find((control) => control.id === state.selectedControlId) || controls[0];
}

function getSelectedMacro() {
  return state.macros.find((macro) => macro.id === state.selectedMacroId) || state.macros[0];
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function slugify(value) {
  return value
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

function getGlyph(name) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase();
}

function formatBinding(binding, compact = false) {
  const modifiers = binding.modifiers?.length ? `${binding.modifiers.join("+")}+` : "";
  const value = `${modifiers}${binding.value}`;
  if (!compact) {
    return value;
  }
  const replacements = {
    "Mouse Wheel": "Wheel",
    "Wheel Up / Down": "Wheel",
    "Mouse 1": "M1",
    "Mouse 2": "M2",
    "Quick ping": "PING",
    "Push to talk": "VOICE",
    "Page Up": "PgUp",
    "Page Down": "PgDn",
  };
  return replacements[binding.value] || value;
}

function describeBinding(type, value) {
  const descriptions = {
    W: "Move forward",
    A: "Strafe left",
    S: "Move backward",
    D: "Strafe right",
    Space: "Jump / action",
    Shift: "Modifier / sprint",
    Tab: "Cycle / scoreboard",
    E: "Interact / use",
    "Mouse 1": "Primary mouse action",
    "Mouse 2": "Secondary mouse action",
    "Wheel Up": "Scroll up",
    "Wheel Down": "Scroll down",
  };
  return descriptions[value] || `${type} assignment`;
}

function updateRootAccent(profile) {
  document.documentElement.style.setProperty("--accent", profile.accent);
  document.documentElement.style.setProperty("--accent-rgb", profile.accentRgb);
}

function persistState() {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    LEGACY_STORAGE_KEYS.forEach((key) => localStorage.removeItem(key));
  } catch (error) {
    console.error("Tartarus IO could not save the configuration.", error);
    showToast("Configuration not saved", "Browser storage is unavailable for this page.");
  }

  if (usesCompanion && !isHydratingFromCompanion) {
    window.clearTimeout(companionSyncTimer);
    companionSyncTimer = window.setTimeout(() => syncConfiguration(false), 250);
  }
}

async function requestCompanion(path, options = {}) {
  const response = await fetch(path, {
    cache: "no-store",
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {}),
    },
    ...options,
  });
  const body = await response.json();
  if (!response.ok) {
    const error = new Error(body.message || `Companion request failed with ${response.status}.`);
    error.status = response.status;
    error.data = body;
    throw error;
  }
  return body;
}

async function syncConfiguration(notifyOnError) {
  if (!usesCompanion || !companionStatus?.companion) {
    return false;
  }

  window.clearTimeout(companionSyncTimer);
  try {
    await requestCompanion("/api/config", {
      method: "PUT",
      body: JSON.stringify(state),
    });
    return true;
  } catch (error) {
    console.error("Tartarus IO could not sync the configuration to the companion.", error);
    if (notifyOnError) {
      showToast("Companion sync failed", error.message);
    }
    return false;
  }
}

function reconcileCompanionProfile(status, notify) {
  const activeProfileId = status?.profileSwitch?.activeProfileId;
  if (
    !activeProfileId ||
    activeProfileId === state.activeProfileId ||
    !state.profiles.some((profile) => profile.id === activeProfileId)
  ) {
    return false;
  }

  state.activeProfileId = activeProfileId;
  renderProfiles();
  isHydratingFromCompanion = true;
  persistState();
  isHydratingFromCompanion = false;

  if (notify) {
    showToast("Profile switched", status.profileSwitch.message);
  }
  return true;
}

async function initializeCompanion() {
  if (!usesCompanion) {
    renderCompanionStatus(null);
    return;
  }

  try {
    companionStatus = await requestCompanion("/api/status");
    const companionConfiguration = await requestCompanion("/api/config");
    if (Array.isArray(companionConfiguration.profiles) && companionConfiguration.profiles.length) {
      isHydratingFromCompanion = true;
      state = normalizeState(companionConfiguration);
      resetDraft();
      renderAll();
      persistState();
      isHydratingFromCompanion = false;
    } else {
      await syncConfiguration(true);
    }
    companionStatus = await requestCompanion("/api/status");
    reconcileCompanionProfile(companionStatus, false);
    renderCompanionStatus(companionStatus);
    window.clearInterval(companionPollTimer);
    companionPollTimer = window.setInterval(pollCompanionStatus, 1200);
  } catch (error) {
    console.error("Tartarus IO Companion is not reachable.", error);
    companionStatus = null;
    renderCompanionStatus(null);
  }
}

async function pollCompanionStatus() {
  try {
    companionStatus = await requestCompanion("/api/status");
    reconcileCompanionProfile(companionStatus, true);
    renderCompanionStatus(companionStatus);
  } catch (error) {
    window.clearInterval(companionPollTimer);
    companionPollTimer = null;
    companionStatus = null;
    renderCompanionStatus(null);
  }
}

function renderCompanionStatus(status) {
  const device = status?.device;
  const lighting = status?.lighting;
  const driver = status?.driver;
  const remapping = status?.remapping;
  const connected = Boolean(device?.connected);
  const active = Boolean(remapping?.enabled);

  elements.deviceName.textContent = device?.name || "Razer Tartarus Chroma";
  elements.deviceStatusDot.classList.toggle("is-connected", connected);
  elements.deviceStatusDot.classList.toggle("is-disconnected", usesCompanion && !connected);

  if (!usesCompanion || !status?.companion) {
    elements.deviceStatusLabel.textContent = "Browser preview only";
    elements.connectionNoteTitle.textContent = "Windows companion not running";
    elements.connectionNoteBody.textContent = "Run Start-TartarusIO.cmd to detect the keypad and manage software profiles.";
  } else if (!connected) {
    elements.deviceStatusLabel.textContent = "Device not detected";
    elements.connectionNoteTitle.textContent = "Companion connected";
    elements.connectionNoteBody.textContent = "Reconnect the Tartarus Chroma to expose its input interfaces.";
  } else if (active) {
    elements.deviceStatusLabel.textContent = "Connected · remapping active";
    elements.connectionNoteTitle.textContent = "System-wide remapping active";
    elements.connectionNoteBody.textContent = `${getRuntimeProfile().name} is being applied by the Windows companion.`;
  } else if (driver?.ready) {
    elements.deviceStatusLabel.textContent = "Connected · ready";
    elements.connectionNoteTitle.textContent = "Software profile ready";
    elements.connectionNoteBody.textContent = "This Chroma has no onboard memory; Tartarus IO keeps and applies its profiles.";
  } else {
    elements.deviceStatusLabel.textContent = "Connected · setup required";
    elements.connectionNoteTitle.textContent = "Keypad detected";
    elements.connectionNoteBody.textContent = "Profiles are editable now. Complete HidHide setup before enabling remapping.";
  }

  elements.deviceDialogName.textContent = device?.name || "Razer Tartarus Chroma";
  elements.deviceDialogIdentity.textContent = device
    ? `USB ${device.vendorId}:${device.productId} · ${device.interfaceCount} HID interfaces`
    : "USB 1532:0208";
  elements.deviceDialogState.textContent = connected ? (active ? "Active" : "Connected") : "Disconnected";
  elements.deviceDialogState.classList.toggle("is-connected", connected);
  elements.deviceDialogState.classList.toggle("is-disconnected", usesCompanion && !connected);
  elements.deviceDialogIntro.textContent = connected
    ? "The companion can read this keypad. Profiles live in Tartarus IO because the device has no onboard profile memory."
    : usesCompanion
      ? "The Windows companion is running, but the Tartarus Chroma is not currently visible."
      : "Start the Windows companion to connect the interface to the attached keypad.";

  elements.lightingHardwareState.classList.toggle("is-active", Boolean(lighting?.applied));
  elements.lightingHardwareState.classList.toggle(
    "is-error",
    Boolean(status?.companion && connected && lighting && !lighting.applied),
  );
  if (!usesCompanion || !status?.companion) {
    elements.lightingHardwareTitle.textContent = "Browser preview only";
    elements.lightingHardwareMessage.textContent =
      "Start the Windows companion to apply lighting to the physical keypad.";
  } else if (!connected) {
    elements.lightingHardwareTitle.textContent = "Keypad not connected";
    elements.lightingHardwareMessage.textContent = "Reconnect the Tartarus Chroma to apply lighting.";
  } else if (lighting?.applied) {
    elements.lightingHardwareTitle.textContent = "Device lighting active";
    elements.lightingHardwareMessage.textContent =
      `${lighting.message} HidHide is not required for lighting.`;
  } else {
    elements.lightingHardwareTitle.textContent = "Device lighting unavailable";
    elements.lightingHardwareMessage.textContent =
      lighting?.message || "Restart the companion to load hardware lighting support.";
  }

  const installed = Boolean(driver?.installed);
  const ready = Boolean(driver?.ready);
  elements.driverStateTitle.textContent = ready
    ? "HidHide configured"
    : installed
      ? "HidHide needs configuration"
      : "HidHide not installed";
  elements.driverStateDescription.textContent =
    driver?.message || "Physical input must be hidden before remapping starts.";
  elements.driverCallout.classList.toggle("is-ready", ready);
  elements.driverCalloutTitle.textContent = ready ? "Input isolation ready" : "Input isolation required";
  elements.driverCalloutBody.textContent = ready
    ? "The companion can see the keypad while original input stays hidden from other applications."
    : driver?.message || "HidHide prevents original and remapped actions from firing together.";

  elements.driverSetupButton.textContent = installed ? (ready ? "Review HidHide setup" : "Configure HidHide") : "Install HidHide";
  elements.driverSetupButton.disabled = !usesCompanion;
  elements.remappingToggleButton.textContent = active ? "Disable remapping" : "Enable remapping";
  elements.remappingToggleButton.disabled = !active && !Boolean(remapping?.canEnable);
  elements.applyButton.querySelector("span").textContent = active ? "Remapping active" : "Apply profile";

  if (status?.lastInput?.sequence && status.lastInput.sequence !== lastInputSequence) {
    lastInputSequence = status.lastInput.sequence;
    if (elements.testModeButton.classList.contains("is-active")) {
      showPhysicalInput(status.lastInput);
    }
  }
}

function showPhysicalInput(input) {
  const control = controls.find((item) => item.id === input.controlId);
  const button = document.querySelector(`[data-control-id="${CSS.escape(input.controlId)}"]`);
  if (input.pressed) {
    button?.classList.add("is-testing");
    window.setTimeout(() => button?.classList.remove("is-testing"), 260);
    const binding = getActiveProfile().mappings[input.controlId];
    elements.deviceHint.textContent = `${control?.name || input.controlId} → ${binding ? formatBinding(binding) : "Unmapped"}`;
  }
}

function setDirty(isDirty = true) {
  state.dirty = isDirty;
  elements.saveState.classList.toggle("is-dirty", isDirty);
  elements.saveStateLabel.textContent = isDirty ? "Changes pending" : "Profile ready";
  persistState();
}

function showToast(title, message) {
  elements.toastTitle.textContent = title;
  elements.toastMessage.textContent = message;
  elements.toast.classList.add("is-visible");
  window.clearTimeout(toastTimer);
  toastTimer = window.setTimeout(() => elements.toast.classList.remove("is-visible"), 3200);
}

function resetDraft() {
  const profile = getActiveProfile();
  const selected = getSelectedControl();
  draft = structuredClone(profile.mappings[selected.id]);
  state.bindingType = draft.type;
}

function renderProfiles() {
  const query = elements.profileSearch.value.trim().toLowerCase();
  const filteredProfiles = state.profiles.filter((profile) => {
    return `${profile.name} ${profile.category} ${profile.app} ${profile.appDisplayName} ${profile.appPath}`
      .toLowerCase()
      .includes(query);
  });

  elements.profileCount.textContent = String(state.profiles.length);
  elements.profileList.innerHTML = filteredProfiles.length
    ? filteredProfiles
        .map(
          (profile) => {
            const applicationLabel = getProfileApplicationLabel(profile);
            const profileRole =
              profile.id === state.defaultProfileId
                ? `Fallback · ${applicationLabel || "No linked application"}`
                : applicationLabel || "Manual activation";
            const profileDetail =
              profile.id === state.activeProfileId ? `Active · ${profileRole}` : profileRole;
            return `
            <button
              class="profile-item ${profile.id === state.selectedProfileId ? "is-active" : ""}"
              type="button"
              data-profile-id="${escapeHtml(profile.id)}"
              style="--profile-accent:${profile.accent};--profile-accent-rgb:${profile.accentRgb}"
            >
              <span class="profile-glyph">${escapeHtml(profile.glyph || getGlyph(profile.name))}</span>
              <span class="profile-copy">
                <strong>${escapeHtml(profile.name)}</strong>
                <small>${escapeHtml(profileDetail)}</small>
              </span>
              <svg class="profile-item-chevron" aria-hidden="true"><use href="#icon-chevron"></use></svg>
            </button>
          `;
          },
        )
        .join("")
    : `<div class="empty-profiles">No profiles match “${escapeHtml(elements.profileSearch.value)}”.</div>`;
}

function renderProfileHeader() {
  const profile = getActiveProfile();
  updateRootAccent(profile);
  elements.profileTitle.textContent = profile.name;
  elements.profileDescription.textContent = profile.description;
  elements.profileCategory.textContent = profile.category;
  elements.profileSwitchLabel.textContent =
    profile.id === state.defaultProfileId
      ? "Fallback profile"
      : profile.autoSwitch && profile.app
        ? "Auto-switch enabled"
        : "Manual activation";
  elements.linkedApplication.textContent = getProfileApplicationLabel(profile) || "Not linked";
  elements.profileAccentDot.style.background = profile.accent;
  elements.profileAccentDot.style.boxShadow = `0 0 10px ${profile.accent}`;
}

function renderKeypad() {
  const profile = getActiveProfile();
  const mainControls = controls.slice(0, 15);
  const dpadControls = controls.filter((control) => control.id.startsWith("dpad-"));

  elements.keyGrid.innerHTML = mainControls
    .map((control) => {
      const binding = profile.mappings[control.id];
      return `
        <button
          class="keycap ${control.id === state.selectedControlId ? "is-selected" : ""}"
          type="button"
          data-control-id="${control.id}"
          aria-label="${escapeHtml(control.name)}, assigned to ${escapeHtml(formatBinding(binding))}"
          aria-pressed="${control.id === state.selectedControlId}"
          style="--key-accent:${profile.accent}"
        >
          <span class="key-number">${control.number}</span>
          <span class="key-action">${escapeHtml(formatBinding(binding, true))}</span>
          <span class="key-hint">${escapeHtml(binding.description)}</span>
        </button>
      `;
    })
    .join("");

  elements.dpad.innerHTML = dpadControls
    .map(
      (control) => `
        <button
          class="dpad-direction ${control.id === state.selectedControlId ? "is-selected" : ""}"
          type="button"
          data-control-id="${control.id}"
          aria-label="${escapeHtml(control.name)}, assigned to ${escapeHtml(formatBinding(profile.mappings[control.id]))}"
          aria-pressed="${control.id === state.selectedControlId}"
          style="--angle:${control.angle}deg"
        >
          <svg aria-hidden="true"><use href="#icon-chevron"></use></svg>
        </button>
      `,
    )
    .join("");

  ["thumb", "paddle"].forEach((id) => {
    const button = document.querySelector(`[data-control-id="${id}"]`);
    button?.classList.toggle("is-selected", id === state.selectedControlId);
    button?.setAttribute("aria-pressed", String(id === state.selectedControlId));
  });

  const mappings = Object.values(profile.mappings);
  elements.mappedCount.textContent = String(mappings.filter((binding) => binding.value).length);
  elements.macroMappedCount.textContent = String(mappings.filter((binding) => binding.type === "Macro").length);
}

function renderInspector() {
  const control = getSelectedControl();
  const profile = getActiveProfile();
  const currentBinding = profile.mappings[control.id];

  if (!draft) {
    resetDraft();
  }

  elements.selectedControlBadge.textContent = control.number;
  elements.selectedControlTitle.textContent = control.name;
  elements.currentBindingValue.textContent = formatBinding(currentBinding);
  elements.currentBindingDescription.textContent = currentBinding.description;
  elements.assignTarget.textContent = control.name;
  elements.bindingValue.value = draft.value;
  elements.bindingPreview.textContent = draft.value.slice(0, 8) || "—";
  elements.triggerMode.value = draft.trigger || "Press";

  [...elements.bindingTypes.querySelectorAll(".binding-type")].forEach((button) => {
    const isActive = button.dataset.bindingType === state.bindingType;
    button.classList.toggle("is-active", isActive);
  });

  [...elements.modifierRow.querySelectorAll("input")].forEach((input) => {
    input.checked = draft.modifiers?.includes(input.value) || false;
    input.disabled = state.bindingType !== "Keyboard";
  });

  const quickBindings = quickBindingsByType[state.bindingType] || [];
  elements.quickBindings.innerHTML = quickBindings
    .map((binding) => `<button class="quick-binding" type="button" data-quick-binding="${escapeHtml(binding)}">${escapeHtml(binding)}</button>`)
    .join("");

  const placeholders = {
    Keyboard: "Press or enter a key",
    Mouse: "Choose a mouse action",
    Macro: "Choose a saved macro",
    Profile: "Choose a profile",
  };
  elements.bindingValue.placeholder = placeholders[state.bindingType];
  const isRisky = state.bindingType === "Macro" || draft.trigger === "Repeat";
  elements.rulesNote.classList.toggle("is-warning", isRisky);
  elements.rulesNote.querySelector("p").textContent = isRisky
    ? "Automation may violate the rules of competitive or online games."
    : "One input per press is the safest default for online games.";
}

function renderTabs() {
  elements.tabs.forEach((tab) => {
    const isActive = tab.dataset.tab === state.activeTab;
    tab.classList.toggle("is-active", isActive);
    tab.setAttribute("aria-selected", String(isActive));
  });
  elements.panels.forEach((panel) => {
    const isActive = panel.dataset.panel === state.activeTab;
    panel.classList.toggle("is-active", isActive);
    panel.hidden = !isActive;
  });
}

function renderLightingPreview() {
  const lighting = getActiveProfile().lighting;
  elements.lightingEnabled.checked = lighting.enabled;
  elements.saveLightingWithProfile.checked = lighting.saveWithProfile;
  elements.brightnessRange.value = String(lighting.brightness);
  elements.brightnessRange.style.setProperty("--range-value", `${lighting.brightness}%`);
  elements.brightnessValue.textContent = `${lighting.brightness}%`;
  elements.lightingEffectLabel.textContent = lighting.effect;
  elements.lightingColorLabel.textContent = `${lighting.colorName} · ${lighting.brightness}%`;
  elements.lightingStage.style.setProperty("--lighting-color", lighting.color);
  elements.lightingStage.style.setProperty("--lighting-opacity", String(lighting.brightness / 100));
  elements.lightingStage.dataset.effect = lighting.effect;
  elements.lightingStage.classList.toggle("is-disabled", !lighting.enabled);

  elements.lightingKeyGrid.innerHTML = Array.from({ length: 15 }, () => '<span class="mini-key"></span>').join("");
  [...elements.effectGrid.querySelectorAll("[data-effect]")].forEach((button) => {
    button.classList.toggle("is-active", button.dataset.effect === lighting.effect);
  });
  [...elements.colorPalette.querySelectorAll("[data-color]")].forEach((button) => {
    button.classList.toggle("is-active", button.dataset.color.toLowerCase() === lighting.color.toLowerCase());
  });
  elements.customColor.value = lighting.color;
  const saveLabel = elements.saveLightingWithProfile.closest(".setting-row").querySelector("small");
  saveLabel.textContent = `Recall this lighting when ${getActiveProfile().name} activates.`;
}

function macroDuration(macro) {
  return macro.steps.reduce((total, step) => total + Number(step.delay || 0), 0);
}

function renderMacros() {
  const macro = getSelectedMacro();
  elements.macroCount.textContent = String(state.macros.length);
  elements.macroList.innerHTML = state.macros
    .map(
      (item) => `
        <button class="macro-item ${item.id === state.selectedMacroId ? "is-active" : ""}" type="button" data-macro-id="${escapeHtml(item.id)}">
          <span class="macro-item-icon"><svg><use href="#icon-command"></use></svg></span>
          <span>
            <strong>${escapeHtml(item.name)}</strong>
            <small>${item.steps.length} actions</small>
          </span>
          <span class="macro-item-duration">${macroDuration(item)} ms</span>
        </button>
      `,
    )
    .join("");

  elements.macroTitle.textContent = macro.name;
  elements.macroDescription.textContent = macro.description;
  elements.macroDuration.textContent = `${macroDuration(macro)} ms`;
  elements.sequenceList.innerHTML = macro.steps
    .map(
      (step, index) => `
        <div class="sequence-step" data-step-index="${index}">
          <span class="step-number">${String(index + 1).padStart(2, "0")}</span>
          <label class="step-action">
            <svg aria-hidden="true"><use href="#icon-keyboard"></use></svg>
            <select aria-label="Action ${index + 1}">
              ${macroActionOptions
                .map((option) => `<option value="${escapeHtml(option)}" ${option === step.action ? "selected" : ""}>${escapeHtml(option)}</option>`)
                .join("")}
            </select>
          </label>
          <label class="step-delay">
            <input type="number" min="0" max="5000" value="${Number(step.delay)}" aria-label="Delay after action ${index + 1}" />
            <span>ms</span>
          </label>
          <button class="remove-step-button" type="button" data-remove-step="${index}" aria-label="Remove action ${index + 1}">
            <svg><use href="#icon-trash"></use></svg>
          </button>
        </div>
      `,
    )
    .join("");
  elements.deleteMacroButton.disabled = state.macros.length <= 1;
}

function renderAll() {
  renderProfiles();
  renderProfileHeader();
  renderTabs();
  renderKeypad();
  renderInspector();
  renderLightingPreview();
  renderMacros();
  elements.saveState.classList.toggle("is-dirty", state.dirty);
  elements.saveStateLabel.textContent = state.dirty ? "Changes pending" : "Profile ready";
}

function selectControl(id) {
  if (!controls.some((control) => control.id === id)) {
    return;
  }

  if (elements.testModeButton.classList.contains("is-active")) {
    const button = document.querySelector(`[data-control-id="${id}"]`);
    button?.classList.add("is-testing");
    window.setTimeout(() => button?.classList.remove("is-testing"), 220);
    const control = controls.find((item) => item.id === id);
    const binding = getActiveProfile().mappings[id];
    elements.deviceHint.textContent = `${control.name} → ${formatBinding(binding)}`;
    return;
  }

  state.selectedControlId = id;
  resetDraft();
  renderKeypad();
  renderInspector();
  const control = getSelectedControl();
  elements.deviceHint.textContent = `${control.name} selected. Choose a new assignment on the right.`;
  persistState();
}

function selectProfile(profileId) {
  if (profileId === state.selectedProfileId) {
    return;
  }
  state.selectedProfileId = profileId;
  state.selectedControlId = "08";
  resetDraft();
  renderAll();
  persistState();
  showToast("Profile opened", `${getActiveProfile().name} is ready to edit.`);
}

function setBindingType(type) {
  state.bindingType = type;
  draft.type = type;
  draft.modifiers = type === "Keyboard" ? draft.modifiers || [] : [];
  const quickValue = quickBindingsByType[type]?.[0] || "";
  if (draft.value === "" || !quickBindingsByType[type]?.includes(draft.value)) {
    draft.value = quickValue;
  }
  draft.description = describeBinding(type, draft.value);
  renderInspector();
}

function updateDraftFromFields() {
  draft.value = elements.bindingValue.value.trim();
  draft.trigger = elements.triggerMode.value;
  draft.modifiers = [...elements.modifierRow.querySelectorAll("input:checked")].map((input) => input.value);
  draft.description = describeBinding(draft.type, draft.value);
}

function assignDraft() {
  updateDraftFromFields();
  if (!draft.value) {
    elements.bindingValue.focus();
    showToast("Assignment needed", "Enter a key, action, macro, or profile first.");
    return;
  }
  const profile = getActiveProfile();
  const control = getSelectedControl();
  profile.mappings[control.id] = structuredClone(draft);
  setDirty();
  renderKeypad();
  renderInspector();
  showToast("Control updated", `${control.name} is now assigned to ${formatBinding(draft)}.`);
}

function setActiveTab(tabName) {
  state.activeTab = tabName;
  renderTabs();
  persistState();
}

async function applyProfile() {
  setDirty(false);
  if (!usesCompanion || !companionStatus?.companion) {
    state.activeProfileId = state.selectedProfileId;
    renderProfiles();
    persistState();
    showToast("Profile saved in browser", "Start the Windows companion to apply it to the attached keypad.");
    return;
  }

  const synced = await syncConfiguration(true);
  if (!synced) {
    return;
  }

  let activationResult;
  try {
    activationResult = await requestCompanion("/api/profiles/activate", {
      method: "POST",
      body: JSON.stringify({ profileId: state.selectedProfileId }),
    });
    companionStatus = await requestCompanion("/api/status");
    reconcileCompanionProfile(companionStatus, false);
    renderCompanionStatus(companionStatus);
  } catch (error) {
    console.error("Tartarus IO could not activate the selected profile.", error);
    showToast("Profile not activated", error.message);
    return;
  }

  if (companionStatus.remapping?.enabled) {
    showToast("Active profile updated", activationResult.message);
    return;
  }

  if (!companionStatus.remapping?.canEnable) {
    showToast(
      "Profile active · setup required",
      companionStatus.remapping?.message || "Complete device setup to enable remapping.",
    );
    if (!elements.deviceDialog.open) {
      elements.deviceDialog.showModal();
    }
    return;
  }

  try {
    await requestCompanion("/api/remapping", {
      method: "POST",
      body: JSON.stringify({ enabled: true }),
    });
    companionStatus = await requestCompanion("/api/status");
    renderCompanionStatus(companionStatus);
    showToast("Remapping enabled", `${getActiveProfile().name} is now active on the Tartarus Chroma.`);
  } catch (error) {
    showToast("Profile saved · setup required", error.message);
    if (!elements.deviceDialog.open) {
      elements.deviceDialog.showModal();
    }
  }
}

function resetActiveProfile() {
  const profile = getActiveProfile();
  const defaultProfile = createDefaultProfiles().find((item) => item.id === profile.id);
  if (!defaultProfile) {
    showToast("Custom profile retained", "Only bundled profiles have a factory layout.");
    return;
  }
  const index = state.profiles.findIndex((item) => item.id === profile.id);
  state.profiles[index] = defaultProfile;
  resetDraft();
  setDirty();
  renderAll();
  showToast("Profile reset", `${profile.name} has been restored to its starting layout.`);
}

function exportConfiguration() {
  const blob = new Blob([JSON.stringify({ version: 3, exportedAt: new Date().toISOString(), ...state }, null, 2)], {
    type: "application/json",
  });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "tartarus-io-config.json";
  link.click();
  URL.revokeObjectURL(url);
  showToast("Configuration exported", "A portable JSON backup was downloaded.");
}

async function handleDriverSetup() {
  if (!usesCompanion || !companionStatus?.companion) {
    showToast("Companion not running", "Start Tartarus IO with Start-TartarusIO.cmd first.");
    return;
  }

  if (!companionStatus.driver?.installed) {
    window.open(companionStatus.driver?.downloadUrl || "https://github.com/nefarius/HidHide/releases/latest", "_blank", "noopener");
    showToast("Install HidHide, then reboot", "Return here after installation to configure the keypad safely.");
    return;
  }

  elements.driverSetupButton.disabled = true;
  elements.driverSetupButton.textContent = "Waiting for approval…";
  try {
    const result = await requestCompanion("/api/hidhide/configure", {
      method: "POST",
      body: "{}",
    });
    showToast("HidHide configured", result.message);
    companionStatus = await requestCompanion("/api/status");
    renderCompanionStatus(companionStatus);
  } catch (error) {
    showToast("HidHide setup incomplete", error.message);
  } finally {
    elements.driverSetupButton.disabled = false;
    renderCompanionStatus(companionStatus);
  }
}

async function toggleRemapping() {
  if (!companionStatus?.companion) {
    return;
  }

  const enable = !companionStatus.remapping?.enabled;
  try {
    if (enable) {
      await syncConfiguration(true);
    }
    const result = await requestCompanion("/api/remapping", {
      method: "POST",
      body: JSON.stringify({ enabled: enable }),
    });
    companionStatus = await requestCompanion("/api/status");
    renderCompanionStatus(companionStatus);
    showToast(enable ? "Remapping enabled" : "Remapping disabled", result.message);
  } catch (error) {
    showToast("Remapping unavailable", error.message);
  }
}

function openProfileDialog(editing = false) {
  const profile = getActiveProfile();
  editingProfileId = editing ? profile.id : null;
  profileSourceId = profile.id;
  profileApplicationDraft = editing ? getProfileApplication(profile) : null;
  elements.dialogKicker.textContent = editing ? "Profile settings" : "New configuration";
  elements.dialogTitle.textContent = editing ? "Edit profile" : "Create profile";
  elements.dialogDescription.textContent = editing
    ? "Link this profile to an application or make it the fallback."
    : "Start from the active layout, then tune it for another game or workflow.";
  elements.dialogSubmitButton.textContent = editing ? "Save changes" : "Create profile";
  elements.profileNameInput.value = editing ? profile.name : "";
  elements.profileAutoSwitchInput.checked = editing ? profile.autoSwitch : false;
  elements.profileFallbackInput.checked = editing && profile.id === state.defaultProfileId;
  elements.profileFallbackInput.disabled = editing && profile.id === state.defaultProfileId;
  elements.profileFallbackHint.textContent = elements.profileFallbackInput.disabled
    ? "This is the current fallback. Choose this option on another profile to replace it."
    : "Activate when no linked application is running.";
  renderApplicationSelector(usesCompanion && !applicationCatalog);
  elements.profileDialog.showModal();
  loadApplicationCatalog();
  window.setTimeout(() => elements.profileNameInput.focus(), 50);
}

function closeProfileDialog() {
  elements.profileDialog.close();
  elements.profileForm.reset();
  editingProfileId = null;
  profileSourceId = null;
  profileApplicationDraft = null;
}

function submitProfileForm(event) {
  event.preventDefault();
  const name = elements.profileNameInput.value.trim();
  const application = profileApplicationDraft?.selectable ? profileApplicationDraft : null;
  const app = application?.executableName || "";
  const appPath = application?.path || "";
  const appDisplayName = application?.name || app;
  const autoSwitch = Boolean(app && elements.profileAutoSwitchInput.checked);
  const fallback = elements.profileFallbackInput.checked;
  if (!name) {
    elements.profileNameInput.focus();
    return;
  }
  if (profileApplicationDraft && !profileApplicationDraft.selectable) {
    elements.profileAppSelect.focus();
    showToast("Executable required", "Use … to select the game's executable before saving this link.");
    return;
  }

  if (editingProfileId) {
    const profile = state.profiles.find((item) => item.id === editingProfileId);
    if (!profile) {
      closeProfileDialog();
      showToast("Profile not found", "The profile changed before these settings could be saved.");
      return;
    }
    profile.name = name;
    profile.glyph = getGlyph(name);
    profile.app = app;
    profile.appPath = appPath;
    profile.appDisplayName = appDisplayName;
    profile.autoSwitch = autoSwitch;
    if (fallback) {
      state.defaultProfileId = profile.id;
    }
    setDirty();
    closeProfileDialog();
    renderAll();
    showToast("Profile updated", `${name} settings were saved.`);
    return;
  }

  const source = state.profiles.find((profile) => profile.id === profileSourceId) || getActiveProfile();
  const baseId = slugify(name) || "custom-profile";
  let id = baseId;
  let suffix = 2;
  while (state.profiles.some((profile) => profile.id === id)) {
    id = `${baseId}-${suffix}`;
    suffix += 1;
  }

  const newProfile = structuredClone(source);
  newProfile.id = id;
  newProfile.name = name;
  newProfile.glyph = getGlyph(name);
  newProfile.category = "Custom profile";
  newProfile.description = `A custom layout based on ${source.name}.`;
  newProfile.app = app;
  newProfile.appPath = appPath;
  newProfile.appDisplayName = appDisplayName;
  newProfile.autoSwitch = autoSwitch;
  state.profiles.push(newProfile);
  state.selectedProfileId = id;
  if (fallback) {
    state.defaultProfileId = id;
  }
  resetDraft();
  setDirty();
  closeProfileDialog();
  renderAll();
  showToast("Profile created", `${name} is ready to customize.`);
}

function updateLighting(patch) {
  Object.assign(getActiveProfile().lighting, patch);
  setDirty();
  renderLightingPreview();
}

function createMacro() {
  const index = state.macros.length + 1;
  const macro = {
    id: `new-macro-${Date.now()}`,
    name: `New macro ${index}`,
    description: "A new local preview sequence.",
    steps: [{ action: "Key press: E", delay: 80 }],
  };
  state.macros.push(macro);
  state.selectedMacroId = macro.id;
  setDirty();
  renderMacros();
  showToast("Macro created", `${macro.name} was added to the library.`);
}

function deleteSelectedMacro() {
  if (state.macros.length <= 1) {
    showToast("Macro retained", "At least one macro must remain in the preview library.");
    return;
  }
  const macro = getSelectedMacro();
  state.macros = state.macros.filter((item) => item.id !== macro.id);
  state.selectedMacroId = state.macros[0].id;
  setDirty();
  renderMacros();
  showToast("Macro deleted", `${macro.name} was removed.`);
}

function syncMacroStepsFromDom() {
  const macro = getSelectedMacro();
  macro.steps = [...elements.sequenceList.querySelectorAll(".sequence-step")].map((row) => ({
    action: row.querySelector("select").value,
    delay: Math.max(0, Number(row.querySelector("input").value) || 0),
  }));
}

function addMacroStep() {
  syncMacroStepsFromDom();
  getSelectedMacro().steps.push({ action: "Key press: E", delay: 80 });
  setDirty();
  renderMacros();
}

function removeMacroStep(index) {
  syncMacroStepsFromDom();
  const macro = getSelectedMacro();
  if (macro.steps.length <= 1) {
    showToast("Action retained", "A macro needs at least one action.");
    return;
  }
  macro.steps.splice(index, 1);
  setDirty();
  renderMacros();
}

function saveMacro() {
  syncMacroStepsFromDom();
  setDirty();
  renderMacros();
  showToast("Macro saved", `${getSelectedMacro().name} has ${getSelectedMacro().steps.length} actions.`);
}

async function previewMacro() {
  if (playingMacro) {
    playingMacro = false;
    elements.playMacroButton.innerHTML = '<svg><use href="#icon-play"></use></svg> Preview';
    return;
  }

  playingMacro = true;
  elements.playMacroButton.innerHTML = '<svg><use href="#icon-stop"></use></svg> Stop';
  const rows = [...elements.sequenceList.querySelectorAll(".sequence-step")];
  const steps = getSelectedMacro().steps;

  for (let index = 0; index < rows.length && playingMacro; index += 1) {
    rows.forEach((row) => row.classList.remove("is-playing"));
    rows[index].classList.add("is-playing");
    await new Promise((resolve) => window.setTimeout(resolve, Math.max(180, Number(steps[index].delay))));
  }

  rows.forEach((row) => row.classList.remove("is-playing"));
  playingMacro = false;
  elements.playMacroButton.innerHTML = '<svg><use href="#icon-play"></use></svg> Preview';
}

function startRecording() {
  recording = !recording;
  elements.recordButton.classList.toggle("is-recording", recording);
  elements.recordButton.lastChild.textContent = recording ? " Press a key…" : " Record input";
  if (recording) {
    elements.bindingValue.focus();
  }
}

function handleRecordedKey(event) {
  if (!recording) {
    return;
  }
  event.preventDefault();
  const ignored = ["Control", "Shift", "Alt", "Meta"];
  if (ignored.includes(event.key)) {
    return;
  }
  const modifiers = [];
  if (event.ctrlKey) modifiers.push("Ctrl");
  if (event.shiftKey) modifiers.push("Shift");
  if (event.altKey) modifiers.push("Alt");
  if (event.metaKey) modifiers.push("Meta");
  draft.value = event.key.length === 1 ? event.key.toUpperCase() : event.key;
  draft.modifiers = modifiers;
  draft.type = "Keyboard";
  state.bindingType = "Keyboard";
  recording = false;
  elements.recordButton.classList.remove("is-recording");
  elements.recordButton.lastChild.textContent = " Record input";
  renderInspector();
}

elements.profileList.addEventListener("click", (event) => {
  const button = event.target.closest("[data-profile-id]");
  if (button) selectProfile(button.dataset.profileId);
});

elements.profileSearch.addEventListener("input", renderProfiles);
elements.profileSearch.addEventListener("keydown", (event) => {
  if (event.key === "Escape") {
    elements.profileSearch.value = "";
    renderProfiles();
    elements.profileSearch.blur();
  }
});

elements.newProfileButton.addEventListener("click", () => openProfileDialog(false));
elements.editProfileButton.addEventListener("click", () => openProfileDialog(true));
elements.dialogCloseButton.addEventListener("click", closeProfileDialog);
elements.dialogCancelButton.addEventListener("click", closeProfileDialog);
elements.profileForm.addEventListener("submit", submitProfileForm);
elements.profileAppSelect.addEventListener("change", handleApplicationSelection);
elements.browseApplicationButton.addEventListener("click", browseForApplication);

elements.tabs.forEach((tab) => tab.addEventListener("click", () => setActiveTab(tab.dataset.tab)));

elements.deviceStage.addEventListener("click", (event) => {
  const control = event.target.closest("[data-control-id]");
  if (control) selectControl(control.dataset.controlId);
});

elements.testModeButton.addEventListener("click", () => {
  const active = elements.testModeButton.classList.toggle("is-active");
  elements.deviceHint.textContent = active
    ? "Test mode is active. Press any control to preview it."
    : "Select a control to change its assignment.";
});

elements.bindingTypes.addEventListener("click", (event) => {
  const button = event.target.closest("[data-binding-type]");
  if (button) setBindingType(button.dataset.bindingType);
});

elements.quickBindings.addEventListener("click", (event) => {
  const button = event.target.closest("[data-quick-binding]");
  if (!button) return;
  draft.value = button.dataset.quickBinding;
  draft.description = describeBinding(draft.type, draft.value);
  renderInspector();
});

elements.bindingValue.addEventListener("input", () => {
  draft.value = elements.bindingValue.value;
  elements.bindingPreview.textContent = draft.value.slice(0, 8) || "—";
});

elements.triggerMode.addEventListener("change", () => {
  draft.trigger = elements.triggerMode.value;
  renderInspector();
});

elements.modifierRow.addEventListener("change", () => {
  draft.modifiers = [...elements.modifierRow.querySelectorAll("input:checked")].map((input) => input.value);
});

elements.recordButton.addEventListener("click", startRecording);
elements.bindingValue.addEventListener("keydown", handleRecordedKey);
elements.assignButton.addEventListener("click", assignDraft);

elements.applyButton.addEventListener("click", applyProfile);
elements.resetButton.addEventListener("click", resetActiveProfile);
elements.exportButton.addEventListener("click", exportConfiguration);
elements.deviceStatusButton.addEventListener("click", () => {
  if (!elements.deviceDialog.open) {
    elements.deviceDialog.showModal();
  }
});
elements.deviceDialogCloseButton.addEventListener("click", () => elements.deviceDialog.close());
elements.driverSetupButton.addEventListener("click", handleDriverSetup);
elements.remappingToggleButton.addEventListener("click", toggleRemapping);

elements.lightingEnabled.addEventListener("change", () => updateLighting({ enabled: elements.lightingEnabled.checked }));
elements.saveLightingWithProfile.addEventListener("change", () =>
  updateLighting({ saveWithProfile: elements.saveLightingWithProfile.checked }),
);
elements.effectGrid.addEventListener("click", (event) => {
  const button = event.target.closest("[data-effect]");
  if (button) updateLighting({ effect: button.dataset.effect });
});
elements.colorPalette.addEventListener("click", (event) => {
  const button = event.target.closest("[data-color]");
  if (button) {
    updateLighting({ color: button.dataset.color, colorName: button.dataset.name });
  }
});
elements.customColor.addEventListener("input", () => {
  updateLighting({ color: elements.customColor.value, colorName: "Custom color" });
});
elements.brightnessRange.addEventListener("input", () => {
  updateLighting({ brightness: Number(elements.brightnessRange.value) });
});

elements.macroList.addEventListener("click", (event) => {
  const item = event.target.closest("[data-macro-id]");
  if (!item) return;
  state.selectedMacroId = item.dataset.macroId;
  renderMacros();
  persistState();
});
elements.newMacroButton.addEventListener("click", createMacro);
elements.deleteMacroButton.addEventListener("click", deleteSelectedMacro);
elements.addStepButton.addEventListener("click", addMacroStep);
elements.saveMacroButton.addEventListener("click", saveMacro);
elements.playMacroButton.addEventListener("click", previewMacro);
elements.sequenceList.addEventListener("click", (event) => {
  const button = event.target.closest("[data-remove-step]");
  if (button) removeMacroStep(Number(button.dataset.removeStep));
});

document.addEventListener("keydown", (event) => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
    event.preventDefault();
    applyProfile();
  }
  if (event.key === "/" && !["INPUT", "SELECT", "TEXTAREA"].includes(document.activeElement.tagName)) {
    event.preventDefault();
    elements.profileSearch.focus();
  }
});

resetDraft();
renderAll();
initializeCompanion();
