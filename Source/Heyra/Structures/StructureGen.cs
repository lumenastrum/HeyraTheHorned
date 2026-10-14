using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════════
    //  STRUCTURE GENERATOR — builds a Heyra.StructureLayoutDef onto a
    //  map. Mirrors what KCSG's LayoutUtils/SymbolUtils did for our
    //  layouts (decompiled VEF 1.6, recorded against 7 baseline lab sites
    //  on 2026-10-13), minus the KCSG features we never used (settlements,
    //  random rotation, pawn symbols, crates, mortars).
    //
    //  Order: clean -> floors -> things -> power -> roofs.
    //    clean   under every roofed cell: hostile-to-site pawns, filth,
    //            non-rock buildings and chunks go (natural rock stays)
    //    floors  terrain tokens (new: KCSG had stopped laying them)
    //    things  walls/furniture/filth/items, faction = site faction;
    //            Security things (turrets, sandbags) skip cells that were
    //            natural rock, so nothing gets buried in a mountain
    //    power   nets rebuilt, generators/users switched on
    //    roofs   per roofGrid, clearing plants and rock under them
    // ═══════════════════════════════════════════════════════════════

    public static class StructureGen
    {
        public static void Generate(StructureLayoutDef layout, Map map, CellRect rect, Faction faction)
        {
            IntVec2 size = layout.Size;
            IntVec3 origin = new IntVec3(rect.minX + (rect.Width - size.x) / 2, 0, rect.minZ + (rect.Height - size.z) / 2);

            // What was natural rock before we touched anything (KCSG's GetAllMineableIn).
            var rock = new Dictionary<IntVec3, Mineable>();
            ForEachCell(size, origin, map, (c, row, col) => rock[c] = c.GetFirstMineable(map));

            Clean(layout, map, size, origin);

            // Floors first, so things stand on them.
            foreach (SymbolDef[,] layer in layout.Layers)
                ForEachCell(size, origin, map, (c, row, col) =>
                {
                    SymbolDef sym = layer[row, col];
                    if (sym?.terrainDef == null) return;
                    DespawnRock(rock, c);
                    map.terrainGrid.SetTerrain(c, sym.terrainDef);
                });

            var spawned = new List<Thing>();
            foreach (SymbolDef[,] layer in layout.Layers)
                ForEachCell(size, origin, map, (c, row, col) =>
                {
                    SymbolDef sym = layer[row, col];
                    if (sym?.thingDef == null) return;
                    Thing t = SpawnSymbol(sym, map, c, faction, rock);
                    if (t != null) spawned.Add(t);
                });

            PowerUp(map, spawned);

            if (layout.HasRoofGrid)
                ForEachCell(size, origin, map, (c, row, col) =>
                {
                    RoofDef roof = null;
                    switch (layout.RoofAt(row, col))
                    {
                        case "1": if (c.GetRoof(map) == null) roof = RoofDefOf.RoofConstructed; break;
                        case "2": if (c.GetRoof(map) == null || c.GetRoof(map) == RoofDefOf.RoofConstructed) roof = RoofDefOf.RoofRockThin; break;
                        case "3": roof = RoofDefOf.RoofRockThick; break;
                    }
                    if (roof == null) return;
                    map.roofGrid.SetRoof(c, roof);
                    Plant plant = c.GetPlant(map);
                    if (plant != null && plant.def.plant.interferesWithRoof) plant.DeSpawn();
                    DespawnRock(rock, c);
                });
        }

        private delegate void CellAction(IntVec3 cell, int row, int col);

        private static void ForEachCell(IntVec2 size, IntVec3 origin, Map map, CellAction act)
        {
            for (int row = 0; row < size.z; row++)
                for (int col = 0; col < size.x; col++)
                {
                    IntVec3 c = origin + new IntVec3(col, 0, row);
                    if (c.InBounds(map)) act(c, row, col);
                }
        }

        // The entry stays: SpawnSymbol asks "was this cell rock before we started?", not "is it now".
        private static void DespawnRock(Dictionary<IntVec3, Mineable> rock, IntVec3 c)
        {
            if (rock.TryGetValue(c, out Mineable m) && m != null && m.Spawned) m.DeSpawn();
        }

        // KCSG's CleanRect without fullClean, limited to roofed cells when there's a roof grid.
        private static void Clean(StructureLayoutDef layout, Map map, IntVec2 size, IntVec3 origin)
        {
            Faction site = map.ParentFaction;
            ForEachCell(size, origin, map, (c, row, col) =>
            {
                if (layout.HasRoofGrid && layout.RoofAt(row, col) == ".") return;
                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (!t.Spawned) continue;
                    bool go = t is Pawn
                        ? t.Faction != site && t.Faction != Faction.OfPlayer
                        : t.def.category == ThingCategory.Filth
                          || (t.def.category == ThingCategory.Building && !t.def.building.isNaturalRock)
                          // Stone chunks sit in StoneChunks, a CHILD of Chunks, so KCSG's direct
                          // Contains(Chunks) never matched them and rubble stayed inside rooms.
                          || t.def.IsWithinCategory(ThingCategoryDefOf.Chunks);
                    if (go) t.DeSpawn();
                }
                TerrainDef under = map.terrainGrid.UnderTerrainAt(c);
                if (under != null) map.terrainGrid.SetTerrain(c, under);
            });
        }

        private static Thing SpawnSymbol(SymbolDef sym, Map map, IntVec3 c, Faction faction, Dictionary<IntVec3, Mineable> rock)
        {
            ThingDef def = sym.thingDef;
            switch (def.category)
            {
                case ThingCategory.Item: return SpawnItem(sym, map, c);
                case ThingCategory.Pawn: return null;
                case ThingCategory.Plant:
                    TerrainDef ground = c.GetTerrain(map);
                    if (ground.BuildableByPlayer || ground.fertility < def.plant.fertilityMin) return null;
                    Plant plant = (Plant)ThingMaker.MakeThing(def);
                    plant.Growth = 0.5f;
                    return GenSpawn.Spawn(plant, c, map, WipeMode.VanishOrMoveAside);
            }

            if (def.designationCategory?.defName == "Security" && rock.TryGetValue(c, out Mineable m) && m != null)
                return null;

            ThingDef stuff = null;
            if (def.MadeFromStuff)
                stuff = def.defName.ToLower().Contains("wall") ? sym.stuffDef : sym.stuffDef ?? def.defaultStuff ?? ThingDefOf.WoodLog;
            Thing thing = ThingMaker.MakeThing(def, stuff);

            if (ModsConfig.IdeologyActive && faction?.ideos?.PrimaryIdeo != null)
                thing.StyleDef = faction.ideos.PrimaryIdeo.GetStyleFor(thing.def);

            CompRefuelable fuel = thing.TryGetComp<CompRefuelable>();
            if (fuel != null)
            {
                if (fuel.Fuel > 0f) fuel.ConsumeFuel(fuel.Fuel);
                fuel.Refuel(fuel.Props.fuelCapacity);
            }
            thing.TryGetComp<CompPowerBattery>()?.SetStoredEnergyPct(1f);

            if (thing is Building_CryptosleepCasket casket && Rand.Value <= sym.chanceToContainPawn)
            {
                Pawn sleeper = CasketPawn(sym, map);
                if (sleeper != null && !casket.TryAcceptThing(sleeper))
                    sleeper.Destroy();
            }

            Thing placed = GenSpawn.Spawn(thing, c, map, sym.rotation, WipeMode.VanishOrMoveAside);
            if (placed == null) return null;
            if (sym.spawnPartOfFaction && faction != null && placed.def.CanHaveFaction) placed.SetFaction(faction);
            if (sym.glowColor.HasValue && placed.TryGetComp<CompGlower>() is CompGlower glow) glow.GlowColor = sym.glowColor.Value;
            return placed;
        }

        private static Thing SpawnItem(SymbolDef sym, Map map, IntVec3 c)
        {
            ThingDef def = sym.thingDef;
            ThingDef stuff = sym.stuffDef ?? (def.MadeFromStuff ? GenStuff.RandomStuffFor(def) : null);
            Thing item = ThingMaker.MakeThing(def, stuff);
            item.stackCount = sym.maxStackSize != -1
                ? Rand.RangeInclusive(1, sym.maxStackSize)
                : Mathf.Clamp(Rand.RangeInclusive(1, def.stackLimit), 1, 75);
            item.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityBaseGen(), ArtGenerationContext.Outsider);
            GenPlace.TryPlaceThing(item, c, map, ThingPlaceMode.Direct);
            item.SetForbidden(true, warnOnFail: false);
            return item;
        }

        private static Pawn CasketPawn(SymbolDef sym, Map map)
        {
            Faction faction = sym.spawnPartOfFaction ? map.ParentFaction : null;
            PawnKindDef kind = sym.containPawnKindAnyOf.Count > 0
                ? sym.containPawnKindAnyOf.RandomElement()
                : faction?.RandomPawnKind() ?? PawnKindDefOf.Villager;
            return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true));
        }

        // KCSG's ReconnectAllPowerBuildings, scoped to what we spawned.
        private static void PowerUp(Map map, List<Thing> spawned)
        {
            map.powerNetManager.UpdatePowerNetsAndConnections_First();
            foreach (Thing t in spawned)
                t.TryGetComp<CompPowerPlant>()?.UpdateDesiredPowerOutput();
            foreach (Thing t in spawned)
            {
                CompPowerTrader trader = t.TryGetComp<CompPowerTrader>();
                if (trader == null || trader.PowerOn || trader.PowerNet == null) continue;
                CompFlickable flick = t.TryGetComp<CompFlickable>();
                if (flick != null && !flick.SwitchIsOn) flick.SwitchIsOn = true;
                CompSchedule schedule = t.TryGetComp<CompSchedule>();
                if (schedule != null) schedule.Allowed = true;
                trader.PowerOn = true;
                schedule?.RecalculateAllowed();
            }
            map.powerNetManager.UpdatePowerNetsAndConnections_First();
        }
    }
}
