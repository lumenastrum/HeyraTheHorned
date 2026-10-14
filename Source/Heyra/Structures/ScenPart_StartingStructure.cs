using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════════
    //  STARTING STRUCTURE — Heyra-owned replacement for
    //  KCSG.ScenPart_AddStartingStructure, for Bando's two restored
    //  scenarios (The Last Stand = Stronghold, Winter is Coming = Village).
    //
    //  After the starting map is generated, builds one layout from
    //  <chooseFrom> for the player: centred on the map (nearMapCenter),
    //  or on a random buildable, unfogged spot away from the edge.
    //  Only on a brand-new colony (KCSG's "tick <= 5" guard), never on
    //  maps settled later. The colonists already stand on the map; walls
    //  spawning on them push them aside, as with KCSG.
    //
    //  Not carried over: KCSG's Prepare Carefully hook and its
    //  "spawnTheStartingPawn" drop-pod mode (Bando used neither).
    // ═══════════════════════════════════════════════════════════════

    public class ScenPart_StartingStructure : ScenPart
    {
        public string structureLabel;
        public List<StructureLayoutDef> chooseFrom = new List<StructureLayoutDef>();
        public bool nearMapCenter;

        public override void PostMapGenerate(Map map)
        {
            if (Find.TickManager.TicksGame > 5 || chooseFrom.NullOrEmpty() || !map.IsPlayerHome) return;

            StructureLayoutDef layout = chooseFrom.RandomElement();
            IntVec2 size = layout.Size;
            IntVec3 center = nearMapCenter ? map.Center : RandomSpot(map, size);
            StructureGen.Generate(layout, map, CellRect.CenteredOn(center, size.x, size.z), Faction.OfPlayer);
            if (map.mapPawns.FreeColonistsSpawned.Count > 0) FloodFillerFog.DebugRefogMap(map);
        }

        private static IntVec3 RandomSpot(Map map, IntVec2 size)
        {
            int edge = Mathf.Max(size.x, size.z);
            bool Fits(IntVec3 c)
            {
                if (c.CloseToEdge(map, edge) || c.Fogged(map) || !c.SupportsStructureType(map, TerrainAffordanceDefOf.Heavy)) return false;
                foreach (IntVec3 cell in CellRect.CenteredOn(c, size.x, size.z).ClipInsideMap(map))
                    if (cell.GetTerrain(map).HasTag("River")
                        || !GenConstruct.CanBuildOnTerrain(ThingDefOf.Wall, cell, map, Rot4.North, null, ThingDefOf.Granite))
                        return false;
                return true;
            }
            return CellFinder.TryFindRandomCellNear(map.Center, map, Mathf.Min(map.Size.x, map.Size.z) / 2 - edge, Fits, out IntVec3 spot, 2000)
                ? spot : map.Center;
        }

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            Rect rect = listing.GetScenPartRect(this, RowHeight * 2f);
            structureLabel = Widgets.TextField(new Rect(rect.x, rect.y, rect.width, RowHeight), structureLabel);
            Widgets.CheckboxLabeled(new Rect(rect.x, rect.y + RowHeight, rect.width, RowHeight),
                "Heyra_StartingStructure_NearCenter".Translate(), ref nearMapCenter);
        }

        public override string Summary(Scenario scen) =>
            ScenSummaryList.SummaryWithList(scen, "MapContain", "Heyra_StartingStructure_MapContains".Translate());

        public override IEnumerable<string> GetSummaryListEntries(string tag)
        {
            if (tag == "MapContain" && !structureLabel.NullOrEmpty()) yield return structureLabel.CapitalizeFirst();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref structureLabel, "structureLabel");
            Scribe_Values.Look(ref nearMapCenter, "nearMapCenter");
            Scribe_Collections.Look(ref chooseFrom, "chooseFrom", LookMode.Def);
        }

        public override int GetHashCode() =>
            base.GetHashCode() ^ (structureLabel?.GetHashCode() ?? 0) ^ chooseFrom.Aggregate(0, (h, d) => h ^ d.shortHash);
    }
}
