using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// Every world in the code, found by type, so a new World class needs no list anywhere: project setup adds them
    /// all to the scene in menu order, and the editor previews and checks use the same list.
    /// </summary>
    public static class WorldCatalog
    {
        const string TemplatePath = "Assets/_Project/Scripts/Editor/WorldTemplate.cs.txt";

        /// <summary>A new world script from the template, in the folder you right-clicked (use Scripts/Runtime/Worlds).</summary>
        [MenuItem("Assets/Create/Music In Your Hands/World", false, 80)]
        static void CreateWorldScript() => ProjectWindowUtil.CreateScriptAssetFromTemplateFile(TemplatePath, "NewWorld.cs");

        /// <summary>
        /// The game's World classes, in no particular order: those in the game's own assembly, apart from any marked
        /// [HiddenWorld] (the editor checks' test world).
        /// </summary>
        public static IEnumerable<Type> Types() =>
            TypeCache.GetTypesDerivedFrom<World>().Where(t => !t.IsAbstract && !t.IsGenericType && t.Assembly == typeof(World).Assembly
                && !t.IsDefined(typeof(HiddenWorldAttribute), false));

        /// <summary>Menu order: MenuOrder, then the class name.</summary>
        public static int Compare(World a, World b) =>
            a.MenuOrder != b.MenuOrder ? a.MenuOrder.CompareTo(b.MenuOrder) : string.CompareOrdinal(a.GetType().Name, b.GetType().Name);

        /// <summary>One inactive instance of every world under <paramref name="parent"/>, in menu order, for previews.</summary>
        public static World[] MakePreviews(Transform parent)
        {
            var worlds = new List<World>();
            foreach (var type in Types())
            {
                var go = new GameObject(type.Name);
                go.transform.SetParent(parent, false);
                worlds.Add((World)go.AddComponent(type));
                go.SetActive(false);
            }
            worlds.Sort(Compare);
            return worlds.ToArray();
        }

        /// <summary>The Material fields Unity saves for a world, its own and its base classes', nearest first.</summary>
        public static IEnumerable<FieldInfo> MaterialFields(Type type)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                foreach (var field in t.GetFields(Flags))
                    if (field.FieldType == typeof(Material) && (field.IsPublic || field.IsDefined(typeof(SerializeField), false))
                        && !field.IsDefined(typeof(NonSerializedAttribute), false))
                        yield return field;
        }

        /// <summary>
        /// The name of the material a world's Material field gets: Materials/{Name}.mat with shader
        /// MusicInYourHands/{Name}. It comes from a [WorldMaterial] attribute on the field, or else from the field's own
        /// name (glowMaterial: Glow, lineMaterial: Line).
        /// </summary>
        public static string MaterialName(Type type, string field)
        {
            var info = MaterialFields(type).FirstOrDefault(f => f.Name == field);
            var named = info?.GetCustomAttribute<WorldMaterialAttribute>();
            if (named != null) return named.Name;
            var stem = field.EndsWith("Material", StringComparison.Ordinal) ? field.Substring(0, field.Length - "Material".Length) : field;
            return stem.Length == 0 ? field : char.ToUpperInvariant(stem[0]) + stem.Substring(1);
        }
    }
}
