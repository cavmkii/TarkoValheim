using HarmonyLib;

namespace TarkoValheim.Util
{
    /// <summary>
    /// Field writes for game members whose names have shifted between Valheim
    /// releases. A missing field logs a warning instead of throwing, so one
    /// renamed field degrades a single stat rather than breaking item setup.
    /// </summary>
    internal static class Reflect
    {
        public static bool TrySet(object target, string field, object value)
        {
            if (target == null)
            {
                return false;
            }

            var info = AccessTools.Field(target.GetType(), field);
            if (info == null)
            {
                Plugin.Log.LogWarning($"{target.GetType().Name}.{field} not found; skipping.");
                return false;
            }

            info.SetValue(target, value);
            return true;
        }
    }
}
