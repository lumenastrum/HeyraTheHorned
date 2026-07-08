using RimWorld;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════
    //  CompProperties_FuelPoweredSpawner
    //  Drop-in replacement for FuPoSpa.CompProperties_FuelPoweredSpawner.
    //  Auto-spawns items when both powered and fueled.
    //  XML field names match the original FuPoSpa mod for compatibility.
    // ═══════════════════════════════════════════════════════════
    public class CompProperties_FuelPoweredSpawner : CompProperties
    {
        public ThingDef thingToSpawn;
        public int spawnCount = 1;
        public int spawnIntervalRange = 60000;
        public bool requiresFuel = true;
        public bool requiresPower = true;
        public bool writeTimeLeftToSpawn = true;
        public int spawnMaxAdjacent = -1;
        public bool spawnForbidden = false;
        public string saveKeysPrefix = "";

        public CompProperties_FuelPoweredSpawner()
        {
            compClass = typeof(CompFuelPoweredSpawner);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  CompFuelPoweredSpawner — auto-spawner gated on fuel + power
    // ═══════════════════════════════════════════════════════════
    public class CompFuelPoweredSpawner : ThingComp
    {
        private int ticksUntilSpawn;
        private CompPowerTrader cachedPower;
        private CompRefuelable cachedFuel;

        public CompProperties_FuelPoweredSpawner Props =>
            (CompProperties_FuelPoweredSpawner)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            cachedPower = parent.TryGetComp<CompPowerTrader>();
            cachedFuel = parent.TryGetComp<CompRefuelable>();
            if (!respawningAfterLoad)
                ResetTimer();
        }

        public override void CompTick()
        {
            if (!CanSpawn())
                return;

            ticksUntilSpawn--;
            if (ticksUntilSpawn <= 0)
            {
                TrySpawn();
                ResetTimer();
            }
        }

        private bool CanSpawn()
        {
            if (Props.requiresPower && (cachedPower == null || !cachedPower.PowerOn))
                return false;

            if (Props.requiresFuel && (cachedFuel == null || !cachedFuel.HasFuel))
                return false;

            return true;
        }

        private void TrySpawn()
        {
            if (Props.thingToSpawn == null)
                return;

            // Respect adjacent item cap
            if (Props.spawnMaxAdjacent >= 0)
            {
                int count = 0;
                foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(parent))
                {
                    if (!cell.InBounds(parent.Map))
                        continue;
                    foreach (Thing t in cell.GetThingList(parent.Map))
                    {
                        if (t.def == Props.thingToSpawn)
                            count += t.stackCount;
                    }
                }
                if (count >= Props.spawnMaxAdjacent)
                    return;
            }

            Thing thing = ThingMaker.MakeThing(Props.thingToSpawn);
            thing.stackCount = Props.spawnCount;
            if (Props.spawnForbidden)
                thing.SetForbidden(true, false);

            GenPlace.TryPlaceThing(thing, parent.Position, parent.Map, ThingPlaceMode.Near);
        }

        private void ResetTimer()
        {
            ticksUntilSpawn = Props.spawnIntervalRange;
        }

        public override void PostExposeData()
        {
            // Include thingToSpawn defName in key to disambiguate
            // multiple spawner comps on the same building
            string prefix = Props.saveKeysPrefix.NullOrEmpty()
                ? Props.thingToSpawn?.defName ?? "unknown"
                : Props.saveKeysPrefix + "_" + (Props.thingToSpawn?.defName ?? "unknown");
            Scribe_Values.Look(ref ticksUntilSpawn, prefix + "_ticks", Props.spawnIntervalRange);
        }

        public override string CompInspectStringExtra()
        {
            if (!Props.writeTimeLeftToSpawn || Props.thingToSpawn == null)
                return null;

            string label = Props.thingToSpawn.LabelCap;

            if (!CanSpawn())
            {
                if (Props.requiresPower && (cachedPower == null || !cachedPower.PowerOn))
                    return label + ": Needs power";

                if (Props.requiresFuel && (cachedFuel == null || !cachedFuel.HasFuel))
                    return label + ": Needs fuel";

                return null;
            }

            return label + " in " + ticksUntilSpawn.ToStringTicksToPeriod();
        }
    }
}
