using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    public Skills GetSkills() { throw null; }                 // seen in Jotunn
    public bool IsDead() { throw null; }                      // seen in Jotunn
    public virtual void Message(MessageHud.MessageType type, string msg, int amount = 0, Sprite icon = null, bool b = false) { } // seen in Jotunn (5 params)
    protected virtual void OnDeath() { }
}

public class Humanoid : Character
{
    public Inventory GetInventory() { throw null; }           // seen in Jotunn
    public void UnequipItem(ItemDrop.ItemData item, bool triggerEquipEffects = true) { }
}

public class Player : Humanoid
{
    public static Player m_localPlayer;                       // seen in Jotunn
    protected override void OnDeath() { }
    private void CreateTombStone() { }
}

public class MessageHud : MonoBehaviour
{
    public enum MessageType { TopLeft = 1, Center = 2 }
}

public class Skills : MonoBehaviour
{
    public enum SkillType { None, Swords, Knives, Clubs, Polearms, Spears, Blocking, Axes, Bows, Crossbows, All = 999 }
    public void ResetSkill(SkillType skillType) { }          // seen in Jotunn
}

public class Inventory
{
    // Remaining members here and below: used by ValheimPlus and aedenthorn/ValheimMods.
    public List<ItemDrop.ItemData> GetAllItems() { throw null; }
    public bool AddItem(ItemDrop.ItemData item) { throw null; }
    public bool RemoveItem(ItemDrop.ItemData item) { throw null; }
    private int m_width;
    private int m_height;
}

public struct Vector2i
{
    public int x;
    public int y;
}

public class ItemDrop : MonoBehaviour
{
    public ItemData m_itemData;                               // seen in Jotunn

    public class ItemData
    {
        public SharedData m_shared;                           // seen in Jotunn
        public bool m_equipped;
        public Vector2i m_gridPos;

        public class SharedData
        {
            public string m_name;
            public string m_description;
            public string m_ammoType;
            public HitData.DamageTypes m_damages;
            public float m_maxDurability;
            public float m_weight;
            public int m_maxStackSize;
            public int m_maxQuality;
            public Attack m_attack;
        }
    }
}

public class HitData
{
    public struct DamageTypes
    {
        public float m_damage, m_blunt, m_slash, m_pierce, m_chop, m_pickaxe;
        public float m_fire, m_frost, m_lightning, m_poison, m_spirit;
    }
}

public class Attack { }

public class CraftingStation : MonoBehaviour
{
    public string m_name;
}

public class ZNetView : MonoBehaviour
{
    public bool IsValid() { throw null; }
}

public class CharacterDrop : MonoBehaviour
{
    public List<Drop> m_drops;                                // seen in Jotunn

    public class Drop                                         // all fields seen in Jotunn
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
