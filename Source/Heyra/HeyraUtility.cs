using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Heyra
{
    public static class HeyraUtility
    {
        // Cached reflection for PawnRenderer.renderTree and PawnRenderTree.SetDirty
        private static readonly FieldInfo fi_renderTree = AccessTools.Field(typeof(PawnRenderer), "renderTree");
        private static readonly MethodInfo mi_setDirty = AccessTools.Method(typeof(PawnRenderTree), "SetDirty");

        public static bool IsHeyra(this Pawn pawn)
        {
            return pawn.def == HeyraRaceDefOf.Alien_Heyra;
        }

        public static bool IsHeyraRace(this Pawn pawn)
        {
            return pawn.def == HeyraRaceDefOf.Alien_Heyra || pawn.def == HeyraRaceDefOf.Alien_Heyra_Rong;
        }

        /// <summary>
        /// Safe check for monsterform hediff. Used throughout patches.
        /// </summary>
        public static bool InMonsterform(this Pawn pawn)
        {
            return pawn?.health?.hediffSet?.HasHediff(HeyraHediffDefOf.Heyra_Hediff_Monsterform) ?? false;
        }

        /// <summary>
        /// Gets the ChangeForm comp from the active monsterform hediff, or null.
        /// </summary>
        public static HediffComp_ChangeForm GetChangeFormComp(this Pawn pawn)
        {
            if (!pawn.InMonsterform()) return null;
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HeyraHediffDefOf.Heyra_Hediff_Monsterform);
            return hediff?.TryGetComp<HediffComp_ChangeForm>();
        }

        /// <summary>
        /// Marks the pawn's PawnRenderTree as dirty so all node graphics re-resolve.
        /// Uses reflection since renderTree/SetDirty may be non-public.
        /// </summary>
        public static void DirtyPawnRenderTree(this Pawn pawn)
        {
            try
            {
                if (pawn?.Drawer?.renderer == null || fi_renderTree == null || mi_setDirty == null)
                    return;

                var renderTree = fi_renderTree.GetValue(pawn.Drawer.renderer);
                if (renderTree != null)
                {
                    mi_setDirty.Invoke(renderTree, null);
                    pawn.Drawer.renderer.EnsureGraphicsInitialized();
                }
            }
            catch (Exception e)
            {
                Log.WarningOnce("[Heyra] Failed to dirty render tree: " + e.Message,
                    "HeyraRenderTreeDirty".GetHashCode());
            }
        }
    }
}
