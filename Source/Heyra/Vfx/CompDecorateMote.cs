using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════
    //  DecorateMote — Bando's "machine at work" overlays (soymilk beans
    //  and milk, biscuits, the loom's light show). The original went
    //  through OLB.CompDecorate, bundled inside MoHAR, which spawned a
    //  fresh 20 s mote every 80 ticks and let them stack. This keeps
    //  exactly one alive per decoration while its conditions hold,
    //  refreshes it before it fades, and clears it when they stop.
    // ═══════════════════════════════════════════════════════════
    public class MoteDecoration
    {
        public ThingDef moteDef;
        public bool ifFueled;
        public bool ifPowered;
        public float scale = 1f;
    }

    public class CompProperties_DecorateMote : CompProperties
    {
        public List<MoteDecoration> motes;

        public CompProperties_DecorateMote()
        {
            compClass = typeof(CompDecorateMote);
        }
    }

    public class CompDecorateMote : ThingComp
    {
        private Mote[] alive;

        public CompProperties_DecorateMote Props => (CompProperties_DecorateMote)props;

        public override void CompTick()
        {
            List<MoteDecoration> motes = Props.motes;
            if (!parent.Spawned || motes.NullOrEmpty()) return;
            if (alive == null) alive = new Mote[motes.Count];

            CompRefuelable fuel = parent.GetComp<CompRefuelable>();
            CompPowerTrader power = parent.GetComp<CompPowerTrader>();
            for (int i = 0; i < motes.Count; i++)
            {
                MoteDecoration deco = motes[i];
                bool working = (!deco.ifFueled || (fuel != null && fuel.HasFuel))
                            && (!deco.ifPowered || (power != null && power.PowerOn));
                Mote current = alive[i];
                if (!working)
                {
                    if (!current.DestroyedOrNull()) current.Destroy();
                    alive[i] = null;
                    continue;
                }
                // The replacement starts as the current one starts fading out: a crossfade, never a gap or a stack.
                MoteProperties mote = current?.def.mote;
                if (current.DestroyedOrNull() || current.AgeSecs >= mote.Lifespan - Mathf.Max(mote.fadeOutTime, 1f / 60f))
                {
                    Mote next = HeyraVfx.SpawnMote(deco.moteDef, parent.TrueCenter(), parent.Map, deco.scale);
                    if (next != null) alive[i] = next;
                }
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (alive == null) return;
            foreach (Mote m in alive)
                if (!m.DestroyedOrNull()) m.Destroy();
            alive = null;
        }
    }
}
