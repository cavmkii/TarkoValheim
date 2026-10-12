using System;
using UnityEngine;

namespace BepInEx
{
    public abstract class BaseUnityPlugin : MonoBehaviour
    {
        protected BepInEx.Logging.ManualLogSource Logger { get { throw null; } }
        public BepInEx.Configuration.ConfigFile Config { get { throw null; } }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class BepInPlugin : Attribute
    {
        public BepInPlugin(string GUID, string Name, string Version) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class BepInDependency : Attribute
    {
        public BepInDependency(string DependencyGUID) { }
    }
}

namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public void LogInfo(object data) { }
        public void LogWarning(object data) { }
        public void LogError(object data) { }
    }
}

namespace BepInEx.Configuration
{
    public class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, ConfigDescription configDescription = null) { throw null; }
    }

    public abstract class ConfigEntryBase { }

    public sealed class ConfigEntry<T> : ConfigEntryBase
    {
        public T Value { get; set; }
    }

    public class ConfigDescription
    {
        public ConfigDescription(string description, AcceptableValueBase acceptableValues = null, params object[] tags) { }
    }

    public abstract class AcceptableValueBase { }

    public class AcceptableValueRange<T> : AcceptableValueBase where T : IComparable
    {
        public AcceptableValueRange(T minValue, T maxValue) { }
    }

    public class AcceptableValueList<T> : AcceptableValueBase where T : IEquatable<T>
    {
        public AcceptableValueList(params T[] acceptableValues) { }
    }
}
