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
        private static readonly MethodInfo InventoryChanged = AccessTools.Method(typeof(Inventory), "Changed");
        private static readonly MethodInfo InventoryAddAt =
            AccessTools.Method(typeof(Inventory), "AddItem", new[] { typeof(ItemDrop.ItemData), typeof(Vector2i) });

        private static readonly FieldInfo GuiCurrentContainer = Field(typeof(InventoryGui), "m_currentContainer");
        private static readonly FieldInfo GuiPlayer = Field(typeof(InventoryGui), "m_player");
        private static readonly FieldInfo GuiContainer = Field(typeof(InventoryGui), "m_container");

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
                return method.Invoke(target, args);
            }
            catch (Exception e)
            {
                WarnOnce(method.Name, e);
                return null;
            }
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

        public static void Changed(Inventory inventory) => Call(InventoryChanged, inventory);

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
