// Hand-written stand-ins for the parts of assembly_valheim TarkoValheim uses.
// Members the mod is not certain are public in the real game are declared
// private here, so any direct use fails this compile check. The game enforces
// access at runtime even though the real build sees a publicized copy; those
// members must go through TarkoValheim.Util.GameAccess (reflection).
// Sources for each member: Jotunn 2.30.2's IL references, and current
// EquipmentAndQuickSlots / EpicLoot (RandyKnapp) and aedenthorn's mods.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public interface IDestructible
{
    void Damage(HitData hit);
}

public class Character : MonoBehaviour, IDestructible
{
    private Transform m_eye;
    public Skills GetSkills() { throw null; }
    public bool IsDead() { throw null; }
    private bool TakeInput() { throw null; }
    public virtual void Message(MessageHud.MessageType type, string msg, int amount = 0, Sprite icon = null, bool b = false) { }
    public float GetSkillFactor(Skills.SkillType skill) { throw null; }
    public virtual void RaiseSkill(Skills.SkillType skill, float value = 1f) { }
    public virtual bool IsBlocking() { throw null; }
    public void Damage(HitData hit) { }
    protected virtual void OnDeath() { }
}

public class VisEquipment : MonoBehaviour
{
    private Transform m_leftHand;
}

public class Humanoid : Character
{
    private VisEquipment m_visEquipment;
    public Inventory GetInventory() { throw null; }
    public ItemDrop.ItemData GetCurrentWeapon() { throw null; }
    public bool EquipItem(ItemDrop.ItemData item, bool triggerEquipEffects = true) { throw null; }
    public void UnequipItem(ItemDrop.ItemData item, bool triggerEquipEffects = true) { }
    public void UseItem(Inventory inventory, ItemDrop.ItemData item, bool fromInventoryGui) { }
    public bool DropItem(Inventory inventory, ItemDrop.ItemData item, int amount) { throw null; }
    public bool StartAttack(Character target, bool secondaryAttack) { throw null; }
    public void DropInvalidItems() { }
}

public class Player : Humanoid
{
    public static Player m_localPlayer;
    private Dictionary<string, string> m_customData;
    public bool InPlaceMode() { throw null; }
    private float GetMaxCarryWeight() { throw null; }
    protected override void OnDeath() { }
    private void CreateTombStone() { }
    private void Awake() { }
    private void Update() { }
    public void OnSpawned() { }
    public void SetInventorySize(int rows) { }
}

public class MessageHud : MonoBehaviour
{
    public enum MessageType { TopLeft = 1, Center = 2 }
}

public class Skills : MonoBehaviour
{
    public enum SkillType { None, Swords, Knives, Clubs, Polearms, Spears, Blocking, Axes, Bows, Crossbows, All = 999 }
    public void ResetSkill(SkillType skillType) { }
}

public class Inventory
{
    private int m_width;
    private int m_height;
    public List<ItemDrop.ItemData> GetAllItems() { throw null; }
    public bool AddItem(ItemDrop.ItemData item) { throw null; }
    private bool AddItem(ItemDrop.ItemData item, Vector2i pos) { throw null; }
    public bool RemoveItem(ItemDrop.ItemData item) { throw null; }
    public bool RemoveItem(ItemDrop.ItemData item, int amount) { throw null; }
    public bool CanAddItem(ItemDrop.ItemData item, int stack = -1) { throw null; }
    public bool CanAddItem(GameObject prefab, int stack = -1) { throw null; }
    public ItemDrop.ItemData GetItemAt(int x, int y) { throw null; }
    public int GetWidth() { throw null; }
    public int GetHeight() { throw null; }
    public float GetTotalWeight() { throw null; }
    public bool MoveItemToThis(Inventory fromInventory, ItemDrop.ItemData item, int amount, int x, int y) { throw null; }
    public void MoveInventoryToGrave(Inventory original) { }
    private void Changed() { }
}

public struct Vector2i
{
    public int x;
    public int y;
    public Vector2i(int x, int y) { this.x = x; this.y = y; }
}

public class ItemDrop : MonoBehaviour
{
    public ItemData m_itemData;

    public static ItemDrop DropItem(ItemData item, int amount, Vector3 position, Quaternion rotation) { throw null; }

    public class ItemData
    {
        public enum ItemType
        {
            None, Material, Consumable, OneHandedWeapon, Bow, Shield, Helmet, Chest, Ammo, Customization,
            Legs, Hands, Trophy, TwoHandedWeapon, Torch, Misc, Shoulder, Utility, Tool, Attach_Atgeir,
            Fish, TwoHandedWeaponLeft, AmmoNonEquipable, Trinket
        }

        public SharedData m_shared;
        public GameObject m_dropPrefab;
        public bool m_equipped;
        public Vector2i m_gridPos;
        public int m_stack;
        public int m_quality;
        public float m_durability;
        public Dictionary<string, string> m_customData;

        public ItemData Clone() { throw null; }
        public bool IsWeapon() { throw null; }
        public Sprite GetIcon() { throw null; }
        public float GetMaxDurability() { throw null; }
        public string GetTooltip(int stackOverride = -1) { throw null; }

        public class SharedData
        {
            public string m_name;
            public string m_description;
            public string m_ammoType;
            public ItemType m_itemType;
            public HitData.DamageTypes m_damages;
            public float m_maxDurability;
            public bool m_useDurability;
            public float m_weight;
            public int m_maxStackSize;
            public int m_maxQuality;
            public bool m_teleportable;
            public Attack m_attack;
            public EffectList m_triggerEffect;
        }
    }
}

public class HitData
{
    public DamageTypes m_damage;
    public Vector3 m_point;
    public Vector3 m_dir;
    public float m_pushForce;
    public Skills.SkillType m_skill;
    public void SetAttacker(Character attacker) { }

    public struct DamageTypes
    {
        public float m_damage, m_blunt, m_slash, m_pierce, m_chop, m_pickaxe;
        public float m_fire, m_frost, m_lightning, m_poison, m_spirit;
    }
}

public class Attack
{
    public bool m_requiresReload;
}

public class EffectList
{
    public GameObject[] Create(Vector3 basePos, Quaternion baseRot, Transform baseParent = null, float scale = 1f, int variant = -1) { throw null; }
}

public class Projectile : MonoBehaviour
{
    private static GameObject FindHitObject(Collider collider) { throw null; }
}

public class GameCamera : MonoBehaviour
{
    public static GameCamera instance { get { throw null; } }
    private void UpdateCamera(float dt) { }
}

public class Hud : MonoBehaviour
{
    public static Hud instance { get { throw null; } }
    private Image m_crosshair;
}

public class InventoryGui : MonoBehaviour
{
    public static InventoryGui instance { get { throw null; } }
    public static bool IsVisible() { throw null; }
    private RectTransform m_player;
    private RectTransform m_container;
    private Container m_currentContainer;
}

public class Container : MonoBehaviour
{
    private string m_name;
    private int m_width;
    private int m_height;
    public Inventory GetInventory() { throw null; }
}

public class ObjectDB : MonoBehaviour
{
    public static ObjectDB instance { get { throw null; } }
    public GameObject GetItemPrefab(string name) { throw null; }
}

public static class ZInput
{
    public static bool GetButton(string name) { throw null; }
    public static bool GetButtonDown(string name) { throw null; }
    public static bool GetKey(KeyCode key, bool logWarning = true) { throw null; }
    public static bool GetKeyDown(KeyCode key, bool logWarning = true) { throw null; }
}

public class CraftingStation : MonoBehaviour
{
    private string m_name;
}

public class ZNetView : MonoBehaviour
{
    public bool IsValid() { throw null; }
}

public class CharacterDrop : MonoBehaviour
{
    public List<Drop> m_drops;

    public class Drop
    {
        public GameObject m_prefab;
        public int m_amountMin;
        public int m_amountMax;
        public float m_chance;
        public bool m_onePerPlayer;
        public bool m_levelMultiplier;
    }
}

public class Localization
{
    public static Localization instance { get { throw null; } }
    public string Localize(string text) { throw null; }
}
