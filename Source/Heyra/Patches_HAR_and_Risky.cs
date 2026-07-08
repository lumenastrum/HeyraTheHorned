using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;


namespace Heyra
{
    /// <summary>
    /// HAR (Humanoid Alien Races) interop patches.
    /// Applied manually with assembly existence checks.
    /// </summary>
    public static class Patches_HAR
    {
        private const string LogPrefix = "[Heyra] ";

        // ═══════════════════════════════════════════════════════════════
        //  HIDE BODY ADDONS — Suppresses HAR addons in monsterform.
        // ═══════════════════════════════════════════════════════════════

        public static bool TryPatchCanDrawAddon(Harmony harmony)
        {
            try
            {
                // [1.6 FIX] Try multiple type names — HAR renamed BodyAddon in newer versions
                Type bodyAddonType = AccessTools.TypeByName("AlienRace.BodyAddon")
                                  ?? AccessTools.TypeByName("AlienRace.AbstractBodyAddon")
                                  ?? AccessTools.TypeByName("AlienRace.AlienPartGenerator+BodyAddon");

                // Fallback: scan the AlienRace assembly for any type containing "BodyAddon"
                if (bodyAddonType == null)
                {
                    var harAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "AlienRace");
                    if (harAssembly != null)
                    {
                        bodyAddonType = harAssembly.GetTypes()
                            .FirstOrDefault(t => t.Name.Contains("BodyAddon")
                                              && !t.IsInterface
                                              && t.GetMethod("CanDrawAddon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null);
                        if (bodyAddonType != null)
                            Log.Message(LogPrefix + $"Found HAR BodyAddon via assembly scan: {bodyAddonType.FullName}");
                    }
                }

                if (bodyAddonType == null)
                {
                    Log.Warning(LogPrefix + "AlienRace BodyAddon type not found — addon hiding during monsterform won't apply. " +
                                "This is cosmetic only; horns/tail will remain visible in beast form.");
                    return false;
                }

                var method = AccessTools.Method(bodyAddonType, "CanDrawAddon", new[] { typeof(Pawn) });
                if (method == null)
                {
                    method = bodyAddonType
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == "CanDrawAddon");
                }

                if (method == null)
                {
                    Log.Warning(LogPrefix + $"CanDrawAddon method not found on {bodyAddonType.FullName}.");
                    return false;
                }

                harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(Patches_HAR), nameof(CanDrawAddon_Prefix)));
                Log.Message(LogPrefix + $"Patched HAR {bodyAddonType.Name}.CanDrawAddon for monsterform addon hiding.");
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + $"Failed to patch CanDrawAddon: {e.Message}");
                return false;
            }
        }

        public static bool CanDrawAddon_Prefix(Pawn pawn, ref bool __result)
        {
            if (pawn != null && pawn.Spawned && pawn.InMonsterform())
            {
                __result = false;
                return false;
            }
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        //  BIOHORN SUPPRESSION — Hides the default HAR horn addon when
        //  a biohorn hediff is present on the Heyra_Horn body part.
        //
        //  When a miracle seed is applied, the biohorn HediffDef has its
        //  own renderNodeProperties (via AncotLibrary) that renders the
        //  new horn texture. Without this patch, both the HAR default
        //  horn addon AND the renderNodeProperties horn would draw,
        //  causing double-rendering.
        //
        //  Runs as a POSTFIX after the existing monsterform PREFIX.
        //  If monsterform is active, the prefix already returned false
        //  and this postfix never fires (correct — we don't want
        //  biohorn rendering during monsterform either).
        // ═══════════════════════════════════════════════════════════════

        // Cached reflection for BodyAddon.path field
        private static FieldInfo _fi_addonPath;
        private static bool _addonPathResolved;

        // All biohorn HediffDef defNames
        private static readonly HashSet<string> BioHornDefNames = new HashSet<string>
        {
            "Heyra_Biohorn0A", "Heyra_Biohorn1A", "Heyra_Biohorn2A",
            "Heyra_Biohorn3A", "Heyra_Biohorn4A", "Heyra_Biohorn5A",
            "Heyra_Biohorn0D", "Heyra_Biohorn1D", "Heyra_Biohorn2D",
            "Heyra_Biohorn3D"
        };

        // Horn addon default paths (Heyra and Rong)
        private static readonly HashSet<string> HornAddonPaths = new HashSet<string>
        {
            "Heyra/Horns/Horn",
            "Heyra/Horns/HornRong"
        };

        public static bool TryPatchCanDrawAddon_BioHorn(Harmony harmony)
        {
            try
            {
                Type bodyAddonType = AccessTools.TypeByName("AlienRace.AlienPartGenerator+BodyAddon");
                if (bodyAddonType == null)
                {
                    var harAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "AlienRace");
                    if (harAssembly != null)
                    {
                        bodyAddonType = harAssembly.GetTypes()
                            .FirstOrDefault(t => t.Name.Contains("BodyAddon")
                                              && !t.IsInterface
                                              && t.GetMethod("CanDrawAddon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null);
                    }
                }

                if (bodyAddonType == null)
                {
                    Log.Warning(LogPrefix + "BodyAddon type not found for biohorn patch.");
                    return false;
                }

                var method = AccessTools.Method(bodyAddonType, "CanDrawAddon", new[] { typeof(Pawn) });
                if (method == null)
                {
                    method = bodyAddonType
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == "CanDrawAddon");
                }

                if (method == null)
                {
                    Log.Warning(LogPrefix + "CanDrawAddon method not found for biohorn patch.");
                    return false;
                }

                harmony.Patch(method,
                    postfix: new HarmonyMethod(typeof(Patches_HAR), nameof(CanDrawAddon_BioHorn_Postfix)));
                Log.Message(LogPrefix + "Patched CanDrawAddon postfix for biohorn horn suppression.");
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + $"Failed to patch biohorn suppression: {e.Message}");
                return false;
            }
        }

        public static void CanDrawAddon_BioHorn_Postfix(object __instance, Pawn pawn, ref bool __result)
        {
            if (!__result) return; // Already hidden (monsterform or condition failure)

            // Resolve the path field once
            if (!_addonPathResolved)
            {
                _addonPathResolved = true;
                _fi_addonPath = AccessTools.Field(__instance.GetType(), "path");
                if (_fi_addonPath == null)
                {
                    // Try parent types (path is on ExtendedGraphicTop)
                    Type t = __instance.GetType();
                    while (t != null && _fi_addonPath == null)
                    {
                        _fi_addonPath = t.GetField("path", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        t = t.BaseType;
                    }
                }
            }

            if (_fi_addonPath == null) return;

            // Only suppress horn addons
            string path = (string)_fi_addonPath.GetValue(__instance);
            if (!HornAddonPaths.Contains(path)) return;

            // Check if pawn has any biohorn hediff
            List<Hediff> hediffs = pawn.health?.hediffSet?.hediffs;
            if (hediffs == null) return;

            for (int i = 0; i < hediffs.Count; i++)
            {
                if (BioHornDefNames.Contains(hediffs[i].def.defName))
                {
                    __result = false;
                    return;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  CUSTOM DRAW SIZE — Overrides HAR AlienComp draw sizes
        //  during monsterform so the beast form renders at the correct
        //  scale. Inspired by Pawnmorph's PawnScaling approach.
        //
        //  Hooks PawnRenderTree.SetDirty as a postfix AFTER HAR, so
        //  HAR resets AlienComp to race defaults first, then we
        //  override with beast form values. When monsterform ends,
        //  HAR's defaults are naturally restored (we do nothing).
        // ═══════════════════════════════════════════════════════════════

        // Cached reflection for HAR AlienComp fields
        private static Type _alienCompType;
        private static FieldInfo _fi_customDrawSize;
        private static FieldInfo _fi_customPortraitDrawSize;
        private static bool _alienCompResolved;

        private static bool ResolveAlienCompFields()
        {
            if (_alienCompResolved) return _alienCompType != null;
            _alienCompResolved = true;

            _alienCompType = AccessTools.TypeByName("AlienRace.AlienComp");
            if (_alienCompType == null) return false;

            _fi_customDrawSize = AccessTools.Field(_alienCompType, "customDrawSize");
            _fi_customPortraitDrawSize = AccessTools.Field(_alienCompType, "customPortraitDrawSize");
            return _fi_customDrawSize != null;
        }

        private static object GetAlienComp(Pawn pawn)
        {
            if (_alienCompType == null || pawn?.AllComps == null) return null;
            foreach (var comp in pawn.AllComps)
            {
                if (_alienCompType.IsInstanceOfType(comp))
                    return comp;
            }
            return null;
        }

        public static bool TryPatchDrawSize(Harmony harmony)
        {
            try
            {
                if (!ResolveAlienCompFields())
                {
                    Log.Message(LogPrefix + "HAR AlienComp not found — beast form draw size scaling unavailable (cosmetic only).");
                    return false;
                }

                var setDirty = AccessTools.Method(typeof(PawnRenderTree), "SetDirty");
                if (setDirty == null)
                {
                    Log.Warning(LogPrefix + "PawnRenderTree.SetDirty not found — draw size scaling unavailable.");
                    return false;
                }

                var postfix = new HarmonyMethod(typeof(Patches_HAR), nameof(DrawSize_SetDirtyPostfix));
                postfix.after = new[] { "erdelf.HumanoidAlienRaces" };

                harmony.Patch(setDirty, postfix: postfix);
                Log.Message(LogPrefix + "Patched PawnRenderTree.SetDirty for monsterform draw size (via HAR AlienComp).");
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + $"Failed to patch draw size: {e.Message}");
                return false;
            }
        }

        public static void DrawSize_SetDirtyPostfix(Pawn ___pawn)
        {
            if (___pawn == null || !___pawn.Spawned || !___pawn.InMonsterform()) return;

            var changeComp = ___pawn.GetChangeFormComp();
            if (changeComp == null) return;

            var alienComp = GetAlienComp(___pawn);
            if (alienComp == null) return;

            Vector2 beastSize = changeComp.DrawSize;

            _fi_customDrawSize.SetValue(alienComp, beastSize);
            _fi_customPortraitDrawSize?.SetValue(alienComp, beastSize);
        }
    }

    /// <summary>
    /// Other patches that target methods with potentially fragile signatures.
    /// Applied manually in HarmonyInitial.
    /// </summary>
    public static class Patches_Risky
    {
        private const string LogPrefix = "[Heyra] ";

        // ═══════════════════════════════════════════════════════════════
        //  STAGGER IMMUNITY — Prevents stagger during monsterform.
        //
        //  StaggerHandler may have moved namespaces in 1.6.
        //  We find it dynamically.
        // ═══════════════════════════════════════════════════════════════

        public static bool TryPatchStagger(Harmony harmony)
        {
            try
            {
                // Try multiple namespace locations
                Type staggerType = AccessTools.TypeByName("RimWorld.StaggerHandler")
                                ?? AccessTools.TypeByName("Verse.StaggerHandler");

                if (staggerType == null)
                {
                    Log.Warning(LogPrefix + "StaggerHandler type not found in 1.6 — stagger immunity won't apply.");
                    return false;
                }

                var method = AccessTools.Method(staggerType, "StaggerFor");
                if (method == null)
                {
                    // Fallback: find any method with "Stagger" in the name
                    method = staggerType
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name.Contains("Stagger"));
                }

                if (method == null)
                {
                    Log.Warning(LogPrefix + "StaggerFor method not found — stagger immunity won't apply.");
                    return false;
                }

                harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(Patches_Risky), nameof(Stagger_Prefix)));
                Log.Message(LogPrefix + $"Patched stagger immunity on {staggerType.Name}.{method.Name}.");
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + $"Failed to patch stagger: {e.Message}");
                return false;
            }
        }

        public static bool Stagger_Prefix(object __instance)
        {
            // Use reflection since the type may differ between versions
            try
            {
                Pawn pawn = Traverse.Create(__instance).Field("parent").GetValue<Pawn>();
                if (pawn != null && pawn.InMonsterform())
                {
                    var comp = pawn.GetChangeFormComp();
                    if (comp != null && comp.Props.immuneToStagger)
                        return false;
                }
            }
            catch { }
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        //  DISABLED WORK TYPES — Adds work restrictions during monsterform.
        //
        //  Targets a compiler-generated method inside Pawn.GetDisabledWorkTypes.
        //  Fragile but non-critical — XML disabledWorkTags still applies.
        // ═══════════════════════════════════════════════════════════════

        public static bool TryPatchDisabledWorkTypes(Harmony harmony)
        {
            try
            {
                var targetMethod = AccessTools.GetDeclaredMethods(typeof(Pawn))
                    .FirstOrDefault(mi =>
                        mi.HasAttribute<CompilerGeneratedAttribute>() &&
                        mi.Name.Contains("GetDisabledWorkTypes"));

                if (targetMethod == null)
                {
                    Log.Message(LogPrefix + "GetDisabledWorkTypes compiler-generated method not found — " +
                                "work type disabling still handled by hediff XML tags.");
                    return false;
                }

                harmony.Patch(targetMethod,
                    prefix: new HarmonyMethod(typeof(Patches_Risky), nameof(DisabledWorkTypes_Prefix)));
                Log.Message(LogPrefix + "Patched GetDisabledWorkTypes for monsterform work restrictions.");
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + $"Failed to patch GetDisabledWorkTypes: {e.Message}");
                return false;
            }
        }

        public static void DisabledWorkTypes_Prefix(Pawn __instance, List<WorkTypeDef> list)
        {
            if (!__instance.InMonsterform()) return;

            // Computed per-pawn per-call, from THIS pawn's current disabled tags.
            // A static cache here once froze the FIRST transformer's personal
            // backstory/trait/gene disables and applied them to every pawn who
            // transformed afterward. Cheap loop, and vanilla caches the result
            // until Notify_DisabledWorkTypesChanged anyway.
            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefs)
            {
                if ((workType.workTags & __instance.CombinedDisabledWorkTags) != 0
                    && !list.Contains(workType))
                {
                    list.Add(workType);
                }
            }
        }
    }
}
