using BepInEx.Configuration;
using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using Jotunn.Utils;
using TarkoValheim.Content;
using TarkoValheim.Guns;
using TarkoValheim.Raid;
using TarkoValheim.Stash.UI;
using TarkoValheim.Stash;
using UnityEngine;

namespace TarkoValheim
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "cavmkii.tarkovalheim";
        public const string PluginName = "TarkoValheim";
        public const string PluginVersion = "0.3.0";

        internal static Plugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;

        internal static ConfigEntry<DeathRule> RaidDeathRule;
        internal static ConfigEntry<int> SecureSlots;
        internal static ConfigEntry<bool> WipeSkillsOnRaidDeath;
        internal static ConfigEntry<float> HideoutRadius;
        internal static ConfigEntry<bool> EnemyAmmoDrops;

        internal static ConfigEntry<bool> TetrisInventory;
        internal static ConfigEntry<int> CellSize;
        internal static ConfigEntry<int> ContainerHeightFactor;
        internal static ConfigEntry<string> SizeOverrides;

        internal static ConfigEntry<float> GunDamage;
        internal static ConfigEntry<float> RecoilMultiplier;
        internal static ConfigEntry<bool> ReloadFromBackpack;
        internal static ConfigEntry<AmmoDisplayMode> AmmoDisplay;
        internal static ConfigEntry<bool> AdsToggle;
        internal static ConfigEntry<bool> Tracers;
        internal static ConfigEntry<bool> Crosshair;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BindConfig();
            BindKeys();
            GunSkills.Register();

            Translations.Register();
            PrefabManager.OnVanillaPrefabsAvailable += RegisterContent;
            PrefabManager.OnPrefabsRegistered += LootDrops.Apply;

            _harmony = new Harmony(PluginGuid);
            DeathRules.Patch(_harmony);
            InventoryHooks.Patch(_harmony);
            GunPatches.Patch(_harmony);

            gameObject.AddComponent<RaidTracker>();
            gameObject.AddComponent<StashUi>();
            gameObject.AddComponent<GunController>();
            gameObject.AddComponent<Ballistics>();
            gameObject.AddComponent<GunHud>();
        }

        private void RegisterContent()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterContent;
            Hideout.Register();
            Firearms.Register();
            Gear.Register();
            GunParts.Register();
        }

        private void BindKeys()
        {
            AddKey(GunController.ReloadButton, "Reload", KeyCode.R, "$tv_key_reload");
            AddKey(GunController.FireModeButton, "FireMode", KeyCode.B, "$tv_key_firemode");
            AddKey(GunController.MagCheckButton, "CheckMagazine", KeyCode.T, "$tv_key_magcheck");
        }

        private void AddKey(string name, string configKey, KeyCode key, string hint)
        {
            InputManager.Instance.AddButton(PluginGuid, new ButtonConfig
            {
                Name = name,
                Config = Config.Bind("Keys", configKey, key),
                HintToken = hint,
                ActiveInGUI = false
            });
        }

        private void BindConfig()
        {
            var admin = new ConfigurationManagerAttributes { IsAdminOnly = true };

            RaidDeathRule = Config.Bind("Raid", "DeathRule", DeathRule.LoseEverything,
                new ConfigDescription(
                    "What happens to your inventory when you die outside a hideout.\n" +
                    "Vanilla: normal tombstone.\n" +
                    "KeepSecure: normal tombstone, but the secure container stays with you.\n" +
                    "LoseEverything: no tombstone; everything outside the secure container is gone.",
                    null, admin));

            SecureSlots = Config.Bind("Raid", "SecureContainerSlots", 4,
                new ConfigDescription(
                    "Only used with the Tetris inventory turned off: number of slots, counted leftward " +
                    "from the bottom-right corner of the vanilla inventory, that survive death in raid.",
                    new AcceptableValueRange<int>(0, 8), admin));

            WipeSkillsOnRaidDeath = Config.Bind("Raid", "HardcoreSkillWipe", false,
                new ConfigDescription("If true, dying in raid resets every skill to zero.", null, admin));

            HideoutRadius = Config.Bind("Raid", "HideoutRadius", 30f,
                new ConfigDescription(
                    "Distance in metres from a Hideout workbench that counts as being out of raid.",
                    new AcceptableValueRange<float>(5f, 100f), admin));

            EnemyAmmoDrops = Config.Bind("Loot", "EnemyAmmoDrops", true,
                new ConfigDescription(
                    "If true, some enemies drop ammunition. Applied when a world loads; turning it off needs a game restart to take effect.",
                    null, admin));

            TetrisInventory = Config.Bind("Inventory", "TetrisInventory", true,
                "Tarkov-style inventory (multi-cell items, rig, backpack, secure container). " +
                "Turn off to get Valheim's own inventory screen back; your items are kept either way.");

            CellSize = Config.Bind("Inventory", "CellSize", 48,
                new ConfigDescription("Size of one inventory cell in pixels at 1080p.",
                    new AcceptableValueRange<int>(32, 72)));

            ContainerHeightFactor = Config.Bind("Inventory", "ContainerHeightFactor", 2,
                new ConfigDescription(
                    "Chests show this many Tetris rows per Valheim row, since items now take several cells.",
                    new AcceptableValueRange<int>(1, 3), admin));

            SizeOverrides = Config.Bind("Inventory", "SizeOverrides", "",
                new ConfigDescription(
                    "Item sizes by prefab name, overriding the defaults. Example: SwordIron=1x4, Wood=1x1",
                    null, admin));

            GunDamage = Config.Bind("Guns", "DamageMultiplier", 1f,
                new ConfigDescription("Multiplies every bullet's damage.",
                    new AcceptableValueRange<float>(0.1f, 5f), admin));

            RecoilMultiplier = Config.Bind("Guns", "RecoilMultiplier", 1f,
                new ConfigDescription("Multiplies how far each shot kicks the view.",
                    new AcceptableValueRange<float>(0f, 3f), admin));

            ReloadFromBackpack = Config.Bind("Guns", "ReloadFromBackpack", false,
                new ConfigDescription(
                    "If false (Tarkov rules), reloading only uses ammo in pockets, rig and secure container.",
                    null, admin));

            AmmoDisplay = Config.Bind("Guns", "AmmoDisplay", AmmoDisplayMode.Exact,
                "Exact: always show rounds left. Tarkov: show only a rough estimate after checking the magazine (T).");

            AdsToggle = Config.Bind("Guns", "AimToggle", false,
                "If true, right mouse toggles aiming instead of holding it.");

            Tracers = Config.Bind("Guns", "Tracers", true, "Draw bullet tracers.");

            Crosshair = Config.Bind("Guns", "Crosshair", true,
                "Show a crosshair that grows with the cone of fire. Tarkov has none; turn it off for that.");
        }
    }

    internal enum DeathRule
    {
        Vanilla,
        KeepSecure,
        LoseEverything
    }
}
