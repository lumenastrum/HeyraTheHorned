using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════════
    //  LAB SITE GENSTEPS — Heyra-owned replacements for
    //  KCSG.GenStep_CustomStructureGen and KCSG.GenStep_EnnemiesPresence
    //  (Heyra_Structures_Lab.xml, the Rong lab quest site).
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Builds one random layout from the list, centred on the map.</summary>
    public class GenStep_HeyraStructure : GenStep
    {
        public List<StructureLayoutDef> structureLayoutDefs = new List<StructureLayoutDef>();

        public override int SeedPart => 482910377;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (structureLayoutDefs.NullOrEmpty()) return;
            StructureLayoutDef layout = structureLayoutDefs.RandomElement();
            IntVec2 size = layout.Size;
            CellRect rect = CellRect.CenteredOn(map.Center, size.x, size.z);
            StructureGen.Generate(layout, map, rect, map.ParentFaction);
            if (map.mapPawns.FreeColonistsSpawned.Count > 0) FloodFillerFog.DebugRefogMap(map);
        }
    }

    /// <summary>
    /// The site's defenders: a combat group of <c>forcedFaction</c> (else the site's faction),
    /// defending the map centre. Same points as KCSG: the site part's threat points if they fall in
    /// <c>defaultPointsRange</c>, otherwise a roll in it; at least 150; times <c>pointMultiplier</c>.
    ///
    /// Two KCSG bugs fixed:
    ///  - KCSG ran before the structure and gave up on the WHOLE group the first time it found no
    ///    standable cell within 2 of the centre (rock or marsh there). 2 of 7 baseline labs had no
    ///    defenders at all. Ours runs after the structure (order 470 in the XML) and widens the search
    ///    per pawn; only a pawn with nowhere at all to stand is dropped.
    ///  - KCSG's no-faction fallback threw its pick away and crashed; ours logs and returns.
    /// </summary>
    public class GenStep_RongGarrison : GenStep
    {
        public FactionDef forcedFaction;
        public float pointMultiplier = 1f;
        public FloatRange defaultPointsRange = new FloatRange(300f, 500f);

        public override int SeedPart => 1290531184;

        public override void Generate(Map map, GenStepParams parms)
        {
            Site site = parms.sitePart?.site;
            Faction faction = forcedFaction != null ? Find.FactionManager.FirstFactionOfDef(forcedFaction) : site?.Faction;
            if (faction == null)
            {
                Log.Warning("[Heyra] Lab site garrison: no " + (forcedFaction?.defName ?? "site") + " faction in this world, no defenders.");
                return;
            }
            if (site != null && site.Faction != faction) site.SetFaction(faction);

            float threat = parms.sitePart?.parms?.threatPoints ?? -1f;
            float points = defaultPointsRange.Includes(threat) ? threat : defaultPointsRange.RandomInRange;
            points = UnityEngine.Mathf.Max(points, 150f) * pointMultiplier;

            IEnumerable<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Combat,
                tile = map.Tile,
                faction = faction,
                points = points
            });

            // Vanilla's spawn-cell search floods out from its root, so a root in rock or a wall finds
            // nothing at all. Root on the nearest standable cell (inside the lab, normally).
            IntVec3 root = map.Center;
            if (!root.Standable(map))
                foreach (IntVec3 c in GenRadial.RadialCellsAround(map.Center, 12f, true))
                    if (c.InBounds(map) && c.Standable(map)) { root = c; break; }

            Lord lord = LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, map.Center, 25000), map);
            foreach (Pawn p in pawns)
            {
                if (!CellFinder.TryFindRandomSpawnCellForPawnNear(root, map, out IntVec3 cell, 2)
                    && !CellFinder.TryFindRandomCellNear(map.Center, map, 40, c => c.Standable(map) && c.GetFirstPawn(map) == null, out cell))
                {
                    p.Discard();
                    continue;
                }
                GenSpawn.Spawn(p, cell, map);
                lord.AddPawn(p);
            }
        }
    }
}
