using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════
    //  TrailMotes — port of MoHAR's HediffComp_TrailLeaver as Bando
    //  used it on Heyra_Hediff_Slash: every `period` ticks, one random
    //  mote from the pool is left in the world at the pawn, with a
    //  random scale.
    // ═══════════════════════════════════════════════════════════
    public class HediffCompProperties_TrailMotes : HediffCompProperties
    {
        public List<ThingDef> motePool;
        public IntRange period = new IntRange(8, 8);
        public Vector3 offset = Vector3.zero;
        public FloatRange randomScale = FloatRange.One;

        public HediffCompProperties_TrailMotes()
        {
            compClass = typeof(HediffComp_TrailMotes);
        }
    }

    public class HediffComp_TrailMotes : HediffComp
    {
        private int period;

        public HediffCompProperties_TrailMotes Props => (HediffCompProperties_TrailMotes)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            Pawn pawn = Pawn;
            if (!pawn.Spawned || Props.motePool.NullOrEmpty()) return;
            if (period <= 0) period = Mathf.Max(1, Props.period.RandomInRange);
            if (!pawn.IsHashIntervalTick(period)) return;

            HeyraVfx.SpawnMote(Props.motePool.RandomElement(), pawn.DrawPos + Props.offset, pawn.Map, Props.randomScale.RandomInRange);
            period = Mathf.Max(1, Props.period.RandomInRange);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  InnerShine — port of MoHAR's HediffComp_InnerShine as Bando
    //  used it on Heyra_Hediff_Showspark: each shine keeps up to
    //  `maxAlive` motes glued to the pawn's head, topping up every
    //  `period` ticks. Standing pawns only, like the original.
    // ═══════════════════════════════════════════════════════════
    public class ShineSpec
    {
        public List<ThingDef> motePool;
        public int maxAlive = 1;
        public IntRange period = new IntRange(24, 24);
        public FloatRange scale = FloatRange.One;
        public bool onHead = true;
    }

    public class HediffCompProperties_InnerShine : HediffCompProperties
    {
        public List<ShineSpec> shines;
        public bool onlyStanding = true;

        public HediffCompProperties_InnerShine()
        {
            compClass = typeof(HediffComp_InnerShine);
        }
    }

    public class HediffComp_InnerShine : HediffComp
    {
        private List<Mote>[] alive;
        private int[] ticksLeft;

        public HediffCompProperties_InnerShine Props => (HediffCompProperties_InnerShine)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            Pawn pawn = Pawn;
            List<ShineSpec> shines = Props.shines;
            if (shines.NullOrEmpty()) return;
            if (alive == null)
            {
                alive = new List<Mote>[shines.Count];
                ticksLeft = new int[shines.Count];
                for (int i = 0; i < shines.Count; i++) alive[i] = new List<Mote>();
            }
            if (!pawn.Spawned)
            {
                ClearMotes();
                return;
            }

            bool allowed = !Props.onlyStanding || pawn.GetPosture() == PawnPosture.Standing;
            Vector3 body = pawn.DrawPos;
            Vector3 head = body + HeadOffset(pawn);
            for (int i = 0; i < shines.Count; i++)
            {
                ShineSpec shine = shines[i];
                List<Mote> motes = alive[i];
                motes.RemoveAll(m => m.DestroyedOrNull());
                if (!allowed)
                {
                    DestroyAll(motes);
                    continue;
                }

                Vector3 at = shine.onHead ? head : body;
                foreach (Mote m in motes) m.exactPosition = at;

                if (ticksLeft[i] > 0)
                {
                    ticksLeft[i]--;
                    continue;
                }
                if (motes.Count < shine.maxAlive && !shine.motePool.NullOrEmpty())
                {
                    Mote m = HeyraVfx.SpawnMote(shine.motePool.RandomElement(), at, pawn.Map, shine.scale.RandomInRange);
                    if (m != null) motes.Add(m);
                }
                ticksLeft[i] = Mathf.Max(0, shine.period.RandomInRange);
            }
        }

        public override void CompPostPostRemoved() => ClearMotes();

        /// <summary>MoHAR's MoteLink head offset: base head offset for the current posture, turned with the body.</summary>
        private static Vector3 HeadOffset(Pawn pawn)
        {
            PawnRenderer renderer = pawn.Drawer.renderer;
            Rot4 facing = pawn.GetPosture() == PawnPosture.Standing ? Rot4.North : renderer.LayingFacing();
            return renderer.BaseHeadOffsetAt(facing).RotatedBy(renderer.BodyAngle(PawnRenderFlags.None));
        }

        private void ClearMotes()
        {
            if (alive == null) return;
            foreach (List<Mote> motes in alive) DestroyAll(motes);
        }

        private static void DestroyAll(List<Mote> motes)
        {
            foreach (Mote m in motes)
                if (!m.DestroyedOrNull()) m.Destroy();
            motes.Clear();
        }
    }
}
