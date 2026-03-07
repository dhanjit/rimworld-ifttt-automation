using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using Verse;

namespace RimWorldIFTTT
{
    /// <summary>
    /// A single discoverable pawn property entry — either a float/numeric or bool.
    /// Discovered at runtime via reflection; no code changes needed to pick up
    /// properties from mods or new RimWorld versions.
    /// </summary>
    public class PawnPropEntry
    {
        public string Key;          // Unique key stored in save: "root.Pawn_PsychicEntropyTracker.CurrentPsyfocus"
        public string DisplayName;  // "Psychic Entropy: Current Psyfocus"
        public string GroupName;    // "Psychic Entropy"
        public bool   IsBoolean;    // true = bool property, false = numeric (float/int/double)

        public Func<Pawn, float?> GetFloat;   // null when IsBoolean
        public Func<Pawn, bool?>  GetBool;    // null when !IsBoolean
    }

    /// <summary>
    /// Discovers all public scalar (float/bool/int) properties on standard pawn
    /// sub-trackers and on any ThingComp subclasses from loaded mod assemblies.
    /// Runs once lazily; scanning is O(assembly × type × property).
    ///
    /// Results are cached for the lifetime of the session. No reflection occurs
    /// at trigger evaluation time — only at dropdown-open time and once at startup.
    /// </summary>
    public static class PawnPropertyScanner
    {
        private static List<PawnPropEntry>                _all;
        private static Dictionary<string, PawnPropEntry>  _byKey;

        public static IReadOnlyList<PawnPropEntry> All
        {
            get { EnsureScanned(); return _all; }
        }

        public static PawnPropEntry Find(string key)
        {
            EnsureScanned();
            if (key.NullOrEmpty()) return null;
            return _byKey.TryGetValue(key, out var e) ? e : null;
        }

        // ── Predefined roots ──────────────────────────────────────────────────
        // These are the standard RimWorld pawn sub-trackers. Each is scanned for
        // public scalar properties. DLC-specific trackers return null if the DLC
        // isn't installed; getters handle null gracefully.

        private static readonly (string group, Func<Pawn, object> get, Type type)[] Roots =
        {
            ("Psychic Entropy", p => p.psychicEntropy,  typeof(Pawn_PsychicEntropyTracker)),
            ("Age",             p => p.ageTracker,       typeof(Pawn_AgeTracker)),
            ("Health",          p => p.health,           typeof(Pawn_HealthTracker)),
            ("Story",           p => p.story,            typeof(Pawn_StoryTracker)),
            ("Needs",           p => p.needs,            typeof(Pawn_NeedsTracker)),
            ("Royalty",         p => p.royalty,          typeof(Pawn_RoyaltyTracker)),
            ("Genes",           p => p.genes,            typeof(Pawn_GeneTracker)),
            ("Relations",       p => p.relations,        typeof(Pawn_RelationsTracker)),
            ("Skills",          p => p.skills,           typeof(Pawn_SkillTracker)),
            ("Equipment",       p => p.equipment,        typeof(Pawn_EquipmentTracker)),
        };

        // Properties to skip across all types — noisy, positional, or non-queryable.
        private static readonly HashSet<string> Blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HashCode", "ThingID", "Label", "LabelCap", "LabelShort", "LabelNoCount",
            "LabelNoParenthesis", "LabelMouseover", "DescriptionFlavor", "DescriptionDetailed",
            "Map", "MapHeld", "Position", "PositionHeld", "Rotation", "DrawPos",
            "Spawned", "SpawnedOrAnyParentSpawned", "Destroyed", "Discarded",
            "def", "Def", "parent", "HeldPosOffset",
        };

        // Assemblies that are part of the base game — we do NOT scan their comp types
        // (they would produce noise from building/item comps). We DO scan the Roots above,
        // which are carefully chosen vanilla tracker types.
        private static readonly HashSet<string> BaseAssemblyPrefixes = new HashSet<string>
        {
            "Assembly-CSharp", "Assembly-CSharp-firstpass",
            "UnityEngine", "Unity.", "mscorlib", "System", "Mono.",
            "Newtonsoft", "NUnit", "0Harmony",
        };

        // ── Scanning ──────────────────────────────────────────────────────────

        private static void EnsureScanned()
        {
            if (_all != null) return;
            _all   = new List<PawnPropEntry>();
            _byKey = new Dictionary<string, PawnPropEntry>(StringComparer.OrdinalIgnoreCase);
            Scan();
            Log.Message($"[IFTTT] PawnPropertyScanner: discovered {_all.Count} pawn properties " +
                        $"({_all.Count(e => e.IsBoolean)} bool, {_all.Count(e => !e.IsBoolean)} numeric)");
        }

        private static void Scan()
        {
            // 1. Predefined tracker roots (vanilla + DLC)
            foreach (var (group, getRoot, type) in Roots)
                ScanType(group, $"root.{type.Name}", getRoot, type);

            // 2. ThingComp subclasses from mod assemblies — picks up VPE, other mods automatically
            ScanModComps();

            // Sort once after scanning: alphabetical within group, groups alphabetical
            _all = _all.OrderBy(e => e.GroupName).ThenBy(e => e.DisplayName).ToList();
        }

        private static void ScanType(string groupName, string keyPrefix,
                                      Func<Pawn, object> getRoot, Type type)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;
            PropertyInfo[] props;
            try { props = type.GetProperties(Flags); }
            catch { return; }

            foreach (PropertyInfo prop in props)
            {
                try
                {
                    if (!prop.CanRead)                                         continue;
                    if (prop.GetIndexParameters().Length > 0)                  continue;
                    if (Blacklist.Contains(prop.Name))                        continue;
                    if (prop.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length > 0) continue;

                    bool isNum  = IsNumericType(prop.PropertyType);
                    bool isBool = prop.PropertyType == typeof(bool);
                    if (!isNum && !isBool)                                     continue;

                    string key = $"{keyPrefix}.{prop.Name}";
                    if (_byKey.ContainsKey(key))                              continue;

                    string displayName = $"{groupName}: {FormatName(prop.Name)}";

                    var entry = new PawnPropEntry
                    {
                        Key         = key,
                        DisplayName = displayName,
                        GroupName   = groupName,
                        IsBoolean   = isBool,
                    };

                    // Capture for lambda
                    PropertyInfo capturedProp = prop;

                    if (isNum)
                    {
                        entry.GetFloat = p =>
                        {
                            try
                            {
                                object root = getRoot(p);
                                if (root == null) return null;
                                object val = capturedProp.GetValue(root);
                                return val == null ? (float?)null : Convert.ToSingle(val);
                            }
                            catch { return null; }
                        };
                    }
                    else
                    {
                        entry.GetBool = p =>
                        {
                            try
                            {
                                object root = getRoot(p);
                                if (root == null) return null;
                                return (bool?)capturedProp.GetValue(root);
                            }
                            catch { return null; }
                        };
                    }

                    _all.Add(entry);
                    _byKey[key] = entry;
                }
                catch { /* skip any problematic property */ }
            }
        }

        private static void ScanModComps()
        {
            Type compBase = typeof(ThingComp);

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Skip base game and system assemblies
                string asmName = asm.GetName().Name ?? "";
                if (BaseAssemblyPrefixes.Any(prefix => asmName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    continue;
                // Skip our own assembly (we have no pawn comps)
                if (asmName == "RimWorldIFTTT") continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                catch { continue; }

                foreach (Type t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || !compBase.IsAssignableFrom(t) || t == compBase)
                            continue;

                        Type   capturedType = t;
                        string groupName    = $"[{ShortAssemblyName(asm)}] {FormatTypeName(t)}";
                        string keyPrefix    = $"comp.{t.FullName}";

                        Func<Pawn, object> getComp =
                            p => p.AllComps?.FirstOrDefault(c => c.GetType() == capturedType);

                        ScanType(groupName, keyPrefix, getComp, t);
                    }
                    catch { /* skip */ }
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool IsNumericType(Type t) =>
            t == typeof(float) || t == typeof(double) ||
            t == typeof(int)   || t == typeof(long)   || t == typeof(short);

        /// Converts "CurrentPsyfocus" → "Current Psyfocus" via camelCase split.
        public static string FormatName(string name)
        {
            if (name.NullOrEmpty()) return name;
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c))
                {
                    bool prevLower = !char.IsUpper(name[i - 1]);
                    bool nextLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                    if (prevLower || (nextLower && char.IsUpper(name[i - 1])))
                        sb.Append(' ');
                }
                sb.Append(i == 0 ? char.ToUpper(c) : c);
            }
            return sb.ToString();
        }

        /// "CompPsycaster" → "Psycaster"
        private static string FormatTypeName(Type t)
        {
            string name = t.Name;
            foreach (string prefix in new[] { "Comp", "Pawn_", "Pawn" })
            {
                if (name.StartsWith(prefix) && name.Length > prefix.Length)
                {
                    name = name.Substring(prefix.Length);
                    break;
                }
            }
            return FormatName(name);
        }

        /// "VanillaExpandedFramework" → "VEF"
        private static string ShortAssemblyName(Assembly asm)
        {
            string name = asm.GetName().Name ?? "Mod";
            // Abbreviate long names
            if (name.Length > 20)
            {
                var words = name.Split('.', '_', '-');
                if (words.Length > 1)
                    return string.Join("", words.Select(w => w.Length > 0 ? w[0].ToString().ToUpper() : ""));
            }
            return name;
        }
    }
}
