using System.Collections.Generic;
using System.Reflection;
using System;
using HarmonyLib;
using UnityEngine.UI;
using UnityEngine;

namespace TarkoValheim.Util
{
    /// <summary>
    /// Every game member that is (or might be) private goes through here.
    /// The build compiles against a publicized copy of the game, but the
    /// runtime still enforces access, so touching a private field directly
    /// throws FieldAccessException. Reflection doesn't. A member that's
    /// missing logs once and returns a fallback instead of throwing.
    /// </summary>
    internal static class GameAccess
    {
        private static readonly FieldInfo InventoryHeight = Field(typeof(Inventory), "m_height");
        private static readonly MethodInfo InventoryChanged = FindChanged();
        private static readonly MethodInfo InventoryAddAt =
            AccessTools.Method(typeof(Inventory), "AddItem", new[] { typeof(ItemDrop.ItemData), typeof(Vector2i) });

        private static readonly FieldInfo GuiCurrentContainer = Field(typeof(InventoryGui), "m_currentContainer");
        private static readonly FieldInfo GuiPlayer = Field(typeof(InventoryGui), "m_player");
        private static readonly FieldInfo GuiContainer = Field(typeof(InventoryGui), "m_container");
        private static readonly FieldInfo GuiCrafting = Field(typeof(InventoryGui), "m_crafting");

        private static readonly FieldInfo HumanoidVisEquipment = Field(typeof(Humanoid), "m_visEquipment");
        private static readonly FieldInfo VisLeftHand = Field(typeof(VisEquipment), "m_leftHand");
        private static readonly FieldInfo CharacterEye = Field(typeof(Character), "m_eye");
        private static readonly MethodInfo CharacterTakeInput = AccessTools.Method(typeof(Character), "TakeInput");

        private static readonly FieldInfo PlayerCustomData = Field(typeof(Player), "m_customData");
        private static readonly MethodInfo PlayerMaxCarry = AccessTools.Method(typeof(Player), "GetMaxCarryWeight");

        private static readonly FieldInfo HudCrosshair = Field(typeof(Hud), "m_crosshair");

        private static readonly FieldInfo ContainerName = Field(typeof(Container), "m_name");
        private static readonly FieldInfo ContainerWidth = Field(typeof(Container), "m_width");
        private static readonly FieldInfo ContainerHeight = Field(typeof(Container), "m_height");
        private static readonly FieldInfo StationName = Field(typeof(CraftingStation), "m_name");

        private static readonly MethodInfo StationGetLevel = Method(typeof(CraftingStation), "GetLevel");
        private static readonly MethodInfo GuiHide = Method(typeof(InventoryGui), "Hide");
        private static readonly MethodInfo GuiCanRepair =
            AccessTools.Method(typeof(InventoryGui), "CanRepair", new[] { typeof(ItemDrop.ItemData) });
        private static readonly MethodInfo PlayerBodyArmor = Method(typeof(Player), "GetBodyArmor");
        private static readonly MethodInfo PlayerFoods = Method(typeof(Player), "GetFoods");
        private static readonly MethodInfo SkillPercent = Method(typeof(Skills.Skill), "GetLevelPercentage");
        private static readonly FieldInfo RecipeOnlyOne = AccessTools.Field(typeof(Recipe), "m_requireOnlyOneIngredient");

        private static readonly MethodInfo FindHitObjectMethod =
            AccessTools.Method(typeof(Projectile), "FindHitObject", new[] { typeof(Collider) });

        private static readonly HashSet<string> Warned = new HashSet<string>();

        private static FieldInfo Field(Type type, string name)
        {
            var field = AccessTools.Field(type, name);
            if (field == null)
            {
                Plugin.Log?.LogWarning($"{type.Name}.{name} not found.");
            }
            return field;
        }

        // By name only, without throwing on overloads: the instance overload
        // with the fewest parameters, searching base classes too.
        private static MethodInfo Method(Type type, string name)
        {
            try
            {
                for (var t = type; t != null; t = t.BaseType)
                {
                    MethodInfo best = null;
                    foreach (var m in AccessTools.GetDeclaredMethods(t))
                    {
                        if (m.Name == name && !m.IsStatic && (best == null || m.GetParameters().Length < best.GetParameters().Length))
                        {
                            best = m;
                        }
                    }
                    if (best != null)
                    {
                        return best;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"{type.Name}.{name}: {e.Message}");
                return null;
            }
            Plugin.Log?.LogWarning($"{type.Name}.{name} not found.");
            return null;
        }

        private static T Get<T>(FieldInfo field, object target, T fallback)
        {
            if (field == null || target == null)
            {
                return fallback;
            }
            try
            {
                return field.GetValue(target) is T value ? value : fallback;
            }
            catch (Exception e)
            {
                WarnOnce(field.Name, e);
                return fallback;
            }
        }

        private static void Set(FieldInfo field, object target, object value)
        {
            if (field == null || target == null)
            {
                return;
            }
            try
            {
                field.SetValue(target, value);
            }
            catch (Exception e)
            {
                WarnOnce(field.Name, e);
            }
        }

        private static object Call(MethodInfo method, object target, params object[] args)
        {
            if (method == null)
            {
                return null;
            }
            try
            {
                return method.Invoke(target, WithDefaults(method, args));
            }
            catch (Exception e)
            {
                WarnOnce(method.Name, e);
                return null;
            }
        }

        // Pads the argument list with the method's declared defaults, so a
        // game update that adds an optional parameter doesn't break the call.
        private static object[] WithDefaults(MethodInfo method, object[] args)
        {
            var parameters = method.GetParameters();
            if (args.Length >= parameters.Length)
            {
                return args;
            }
            var full = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                full[i] = i < args.Length ? args[i]
                    : p.HasDefaultValue ? p.DefaultValue
                    : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
            }
            return full;
        }

        public static void WarnOnce(string key, Exception e)
        {
            if (Warned.Add(key))
            {
                Plugin.Log?.LogError($"[{key}] {e}");
            }
        }

        // ------------------------------------------------------------------

        public static int GetHeight(Inventory inventory) => Get(InventoryHeight, inventory, inventory != null ? inventory.GetHeight() : 0);

        public static void SetHeight(Inventory inventory, int height) => Set(InventoryHeight, inventory, height);

        public static void Changed(Inventory inventory)
        {
            if (InventoryChanged == null)
            {
                return;
            }

            // Fill any parameters with their declared defaults (or the type's
            // default), in case the only overload takes arguments.
            var parameters = InventoryChanged.GetParameters();
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                args[i] = p.HasDefaultValue ? p.DefaultValue
                    : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
            }
            Call(InventoryChanged, inventory, args);
        }

        // Inventory has more than one method called Changed; prefer the one
        // without parameters.
        private static MethodInfo FindChanged()
        {
            MethodInfo best = null;
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(Inventory)))
            {
                if (method.Name == "Changed" && !method.IsStatic
                    && (best == null || method.GetParameters().Length < best.GetParameters().Length))
                {
                    best = method;
                }
            }
            if (best == null)
            {
                Plugin.Log?.LogWarning("Inventory.Changed not found.");
            }
            return best;
        }

        public static bool AddItemAt(Inventory inventory, ItemDrop.ItemData item, Vector2i pos)
        {
            if (InventoryAddAt == null)
            {
                item.m_gridPos = pos;
                return inventory.AddItem(item);
            }
            return Call(InventoryAddAt, inventory, item, pos) is bool ok && ok;
        }

        public static Container CurrentContainer(InventoryGui gui) => Get<Container>(GuiCurrentContainer, gui, null);

        public static RectTransform PlayerPanel(InventoryGui gui) => Get<RectTransform>(GuiPlayer, gui, null);

        public static RectTransform ContainerPanel(InventoryGui gui) => Get<RectTransform>(GuiContainer, gui, null);

        public static RectTransform CraftingPanel(InventoryGui gui) => Get<RectTransform>(GuiCrafting, gui, null);

        public static Transform LeftHand(Humanoid humanoid)
        {
            var vis = Get<VisEquipment>(HumanoidVisEquipment, humanoid, null);
            return Get<Transform>(VisLeftHand, vis, null);
        }

        public static Transform Eye(Character character) => Get<Transform>(CharacterEye, character, null);

        public static bool TakeInput(Character character) => Call(CharacterTakeInput, character) is bool ok ? ok : true;

        /// <summary>The player's saved key/value store, or null if unavailable.</summary>
        public static Dictionary<string, string> CustomData(Player player) =>
            Get<Dictionary<string, string>>(PlayerCustomData, player, null);

        public static float MaxCarryWeight(Player player) => Call(PlayerMaxCarry, player) is float w ? w : 300f;

        public static Image Crosshair(Hud hud) => Get<Image>(HudCrosshair, hud, null);

        public static string ContainerTitle(Container container) => Get(ContainerName, container, "");

        public static void SetContainer(Container container, string name, int width, int height)
        {
            Set(ContainerName, container, name);
            Set(ContainerWidth, container, width);
            Set(ContainerHeight, container, height);
        }

        public static void SetStationName(CraftingStation station, string name) => Set(StationName, station, name);

        public static string GetStationName(CraftingStation station) => Get(StationName, station, station != null ? station.name : "");

        public static int StationLevel(CraftingStation station)
        {
            if (station == null)
            {
                return 0;
            }
            return Call(StationGetLevel, station) is int level ? level : 1;
        }

        /// <summary>Closes Valheim's inventory screen.</summary>
        public static void HideInventory(InventoryGui gui) => Call(GuiHide, gui);

        /// <summary>Valheim's own repair check (station type and level), or
        /// null when the method can't be found.</summary>
        public static bool? CanRepair(InventoryGui gui, ItemDrop.ItemData item)
        {
            if (GuiCanRepair == null || gui == null)
            {
                return null;
            }
            return Call(GuiCanRepair, gui, item) is bool ok ? ok : (bool?)null;
        }

        public static float BodyArmor(Player player) => Call(PlayerBodyArmor, player) is float a ? a : 0f;

        /// <summary>Active foods as (item, seconds left). Empty if unavailable.</summary>
        public static List<KeyValuePair<ItemDrop.ItemData, float>> Foods(Player player)
        {
            var list = new List<KeyValuePair<ItemDrop.ItemData, float>>();
            if (!(Call(PlayerFoods, player) is System.Collections.IEnumerable foods))
            {
                return list;
            }
            foreach (var food in foods)
            {
                if (food == null)
                {
                    continue;
                }
                var t = food.GetType();
                var item = AccessTools.Field(t, "m_item")?.GetValue(food) as ItemDrop.ItemData;
                float time = AccessTools.Field(t, "m_time")?.GetValue(food) is float f ? f : 0f;
                if (item != null)
                {
                    list.Add(new KeyValuePair<ItemDrop.ItemData, float>(item, time));
                }
            }
            return list;
        }

        /// <summary>Progress to the next level, 0..1.</summary>
        public static float SkillProgress(Skills.Skill skill)
        {
            if (!(Call(SkillPercent, skill) is float p))
            {
                return 0f;
            }
            // Some versions return 0..100, others 0..1.
            return Mathf.Clamp01(p > 1f ? p / 100f : p);
        }

        /// <summary>Active status effects as (name token, icon). Empty if unavailable.</summary>
        public static List<KeyValuePair<string, Sprite>> StatusEffects(Character character)
        {
            var list = new List<KeyValuePair<string, Sprite>>();
            try
            {
                var seman = Method(typeof(Character), "GetSEMan")?.Invoke(character, new object[0]);
                var effects = seman != null ? Method(seman.GetType(), "GetStatusEffects")?.Invoke(seman, new object[0]) : null;
                if (effects is System.Collections.IEnumerable all)
                {
                    foreach (var se in all)
                    {
                        if (se == null)
                        {
                            continue;
                        }
                        var t = se.GetType();
                        string name = AccessTools.Field(t, "m_name")?.GetValue(se) as string;
                        var icon = AccessTools.Field(t, "m_icon")?.GetValue(se) as Sprite;
                        if (!string.IsNullOrEmpty(name))
                        {
                            list.Add(new KeyValuePair<string, Sprite>(name, icon));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                WarnOnce("StatusEffects", e);
            }
            return list;
        }

        private static object _worldGen;
        private static MethodInfo _getBiome;

        /// <summary>The biome at a position, or None if it can't be read.</summary>
        public static Heightmap.Biome BiomeAt(Vector3 pos)
        {
            try
            {
                var type = typeof(WorldGenerator);
                var instanceProp = AccessTools.Property(type, "instance");
                _worldGen = instanceProp != null ? instanceProp.GetValue(null, null) : AccessTools.Field(type, "instance")?.GetValue(null);
                if (_worldGen == null)
                {
                    return Heightmap.Biome.None;
                }
                if (_getBiome == null)
                {
                    foreach (var m in AccessTools.GetDeclaredMethods(type))
                    {
                        var ps = m.GetParameters();
                        if (m.Name == "GetBiome" && !m.IsStatic && ps.Length >= 2 && ps[0].ParameterType == typeof(float)
                            && ps[1].ParameterType == typeof(float) && (_getBiome == null || ps.Length < _getBiome.GetParameters().Length))
                        {
                            _getBiome = m;
                        }
                    }
                }
                return Call(_getBiome, _worldGen, pos.x, pos.z) is Heightmap.Biome b ? b : Heightmap.Biome.None;
            }
            catch (Exception e)
            {
                WarnOnce("BiomeAt", e);
                return Heightmap.Biome.None;
            }
        }

        private static readonly FieldInfo RequirementUpgrader = AccessTools.Field(typeof(Piece.Requirement), "m_upgraderResource");
        private static readonly FieldInfo StationUpgrader = AccessTools.Field(typeof(CraftingStation), "m_upgrader");

        /// <summary>
        /// Valheim 1.0's rule for "upgrader resources" (the idols): they only
        /// count at an upgrader station, where nothing else counts. At any
        /// other station, or with none, they're ignored. Before 1.0 the
        /// fields don't exist and every ingredient counts.
        /// </summary>
        public static bool RequirementApplies(Piece.Requirement req, CraftingStation station)
        {
            bool idol = RequirementUpgrader != null && RequirementUpgrader.GetValue(req) is bool b && b;
            if (station == null)
            {
                return !idol;
            }
            bool upgrader = StationUpgrader != null && StationUpgrader.GetValue(station) is bool u && u;
            return upgrader == idol;
        }

        /// <summary>
        /// Adds a saved pin to the local map unless one with this name is
        /// already within a few metres. All by reflection: AddPin's
        /// parameter list has changed between game versions. Returns false
        /// if the map isn't ready yet (try again later).
        /// </summary>
        public static bool AddMapPin(Vector3 pos, string name, string pinType)
        {
            try
            {
                var type = AccessTools.TypeByName("Minimap");
                var map = type != null ? AccessTools.Property(type, "instance")?.GetValue(null, null) : null;
                if (map == null)
                {
                    return false;
                }

                if (AccessTools.Field(type, "m_pins")?.GetValue(map) is System.Collections.IEnumerable pins)
                {
                    foreach (var pin in pins)
                    {
                        var pt = pin.GetType();
                        if (AccessTools.Field(pt, "m_pos")?.GetValue(pin) is Vector3 p
                            && AccessTools.Field(pt, "m_name")?.GetValue(pin) as string == name
                            && (p - pos).sqrMagnitude < 25f)
                        {
                            return true;
                        }
                    }
                }

                MethodInfo add = null;
                foreach (var m in AccessTools.GetDeclaredMethods(type))
                {
                    var ps = m.GetParameters();
                    if (m.Name == "AddPin" && ps.Length >= 4 && ps[0].ParameterType == typeof(Vector3)
                        && (add == null || ps.Length < add.GetParameters().Length))
                    {
                        add = m;
                    }
                }
                var pinTypeType = add?.GetParameters()[1].ParameterType;
                if (add == null || pinTypeType == null || !pinTypeType.IsEnum)
                {
                    WarnOnce("AddMapPin", new MissingMethodException("Minimap.AddPin"));
                    return true;
                }

                object kind = Enum.IsDefined(pinTypeType, pinType) ? Enum.Parse(pinTypeType, pinType) : Enum.GetValues(pinTypeType).GetValue(0);
                // (pos, type, name, save, isChecked, ...defaults)
                Call(add, map, pos, kind, name, true, false);
                return true;
            }
            catch (Exception e)
            {
                WarnOnce("AddMapPin", e);
                return true;
            }
        }

        public static bool RequireOnlyOneIngredient(Recipe recipe) => Get(RecipeOnlyOne, recipe, false);

        public static GameObject FindHitObject(Collider collider)
        {
            if (FindHitObjectMethod != null)
            {
                return Call(FindHitObjectMethod, null, collider) as GameObject;
            }
            return collider.attachedRigidbody != null ? collider.attachedRigidbody.gameObject : collider.gameObject;
        }
    }
}
