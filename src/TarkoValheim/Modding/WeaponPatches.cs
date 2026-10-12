using HarmonyLib;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Modding
{
    /// <summary>
    /// Applies installed parts to Valheim's own numbers: damage (which also
    /// drives the tooltip), max durability, block power, parry force and
    /// attack stamina. Each hook is optional; a missing method only turns
    /// that one stat off.
    /// </summary>
    internal static class WeaponPatches
    {
        private static System.Reflection.FieldInfo _attackWeapon;

        public static void Patch(Harmony harmony)
        {
            var item = typeof(ItemDrop.ItemData);
            Hook(harmony, AccessTools.Method(item, "GetDamage", new[] { typeof(int), typeof(float) }), nameof(DamagePostfix), "damage");
            Hook(harmony, AccessTools.Method(item, "GetMaxDurability", new[] { typeof(int) }), nameof(DurabilityPostfix), "durability");
            Hook(harmony, AccessTools.Method(item, "GetBaseBlockPower", new[] { typeof(int) }), nameof(BlockPostfix), "block");
            Hook(harmony, AccessTools.Method(item, "GetDeflectionForce", new[] { typeof(int) }), nameof(ParryPostfix), "parry");
            Hook(harmony, AccessTools.Method(typeof(Attack), "GetAttackStamina"), nameof(StaminaPostfix), "stamina");
            _attackWeapon = AccessTools.Field(typeof(Attack), "m_weapon");
        }

        private static void Hook(Harmony harmony, System.Reflection.MethodInfo target, string postfix, string what)
        {
            if (target == null)
            {
                Plugin.Log.LogWarning($"Weapon parts: {what} hook not found; parts won't change {what}.");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(WeaponPatches), postfix));
        }

        private static void DamagePostfix(ItemDrop.ItemData __instance, ref HitData.DamageTypes __result)
        {
            try
            {
                var t = WeaponMods.Totals(__instance);
                if (!t.Active)
                {
                    return;
                }
                float m = Mathf.Max(0.2f, 1f + t.Damage);
                float physical = __result.m_blunt + __result.m_slash + __result.m_pierce;
                __result.m_damage *= m;
                __result.m_blunt *= m;
                __result.m_slash *= m;
                __result.m_pierce *= m;
                __result.m_chop *= m;
                __result.m_pickaxe *= m;
                __result.m_fire *= m;
                __result.m_frost *= m;
                __result.m_lightning *= m;
                __result.m_poison *= m;
                __result.m_spirit *= m;
                // Relative to the standard build, so this can take away a
                // silver weapon's spirit damage when its silver head is swapped.
                __result.m_spirit = Mathf.Max(0f, __result.m_spirit + physical * m * t.Spirit);
                __result.m_fire = Mathf.Max(0f, __result.m_fire + physical * m * t.Fire);
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("WeaponPatches.Damage", e);
            }
        }

        private static void DurabilityPostfix(ItemDrop.ItemData __instance, ref float __result)
        {
            try
            {
                var t = WeaponMods.Totals(__instance);
                if (t.Active)
                {
                    __result *= Mathf.Max(0.2f, 1f + t.Durability);
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("WeaponPatches.Durability", e);
            }
        }

        private static void BlockPostfix(ItemDrop.ItemData __instance, ref float __result)
        {
            try
            {
                var t = WeaponMods.Totals(__instance);
                if (t.Active)
                {
                    __result *= Mathf.Max(0.2f, 1f + t.Block);
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("WeaponPatches.Block", e);
            }
        }

        private static void ParryPostfix(ItemDrop.ItemData __instance, ref float __result)
        {
            try
            {
                var t = WeaponMods.Totals(__instance);
                if (t.Active)
                {
                    __result *= Mathf.Max(0.2f, 1f + t.Parry);
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("WeaponPatches.Parry", e);
            }
        }

        private static void StaminaPostfix(Attack __instance, ref float __result)
        {
            try
            {
                var weapon = _attackWeapon != null ? _attackWeapon.GetValue(__instance) as ItemDrop.ItemData : null;
                var t = WeaponMods.Totals(weapon);
                if (t.Active)
                {
                    __result *= Mathf.Max(0.2f, 1f + t.Stamina);
                }
            }
            catch (System.Exception e)
            {
                GameAccess.WarnOnce("WeaponPatches.Stamina", e);
            }
        }
    }
}
