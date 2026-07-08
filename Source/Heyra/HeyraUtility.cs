using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
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
        /// Moves apparel and weapons from inventory back to worn/equipped slots.
        /// Gear is stashed to inventory on transform; this reverses it. Called from
        /// HediffComp_ChangeForm.CompPostPostRemoved so EVERY removal path restores
        /// gear — manual toggle-off, natural timer expiry, and downed-revert alike.
        /// </summary>
        public static void RestoreGearFromInventory(this Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.inventory?.innerContainer == null)
                return;

            // Snapshot items first — can't modify collection while iterating
            List<Apparel> toWear = new List<Apparel>();
            List<ThingWithComps> toEquip = new List<ThingWithComps>();

            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                if (thing is Apparel apparel)
                    toWear.Add(apparel);
                else if (thing is ThingWithComps twc && twc.def.IsWeapon)
                    toEquip.Add(twc);
            }

            // Re-wear all apparel (conflicts go back to inventory, not dropped)
            foreach (Apparel apparel in toWear)
            {
                if (pawn.apparel != null && pawn.inventory.innerContainer.Contains(apparel))
                {
                    pawn.inventory.innerContainer.Remove(apparel);
                    pawn.apparel.Wear(apparel, dropReplacedApparel: false);
                }
            }

            // Re-equip weapons (primary slot only — extras stay in inventory)
            foreach (ThingWithComps weapon in toEquip)
            {
                if (pawn.equipment != null && pawn.inventory.innerContainer.Contains(weapon))
                {
                    if (pawn.equipment.Primary == null)
                    {
                        pawn.inventory.innerContainer.Remove(weapon);
                        pawn.equipment.AddEquipment(weapon);
                    }
                }
            }
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
