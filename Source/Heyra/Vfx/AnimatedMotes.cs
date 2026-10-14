using RimWorld;
using UnityEngine;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════
    //  Animated motes — the parts of MoHAR's MoharGfx that Bando's
    //  slash and spark motes relied on (Graphic_AnimatedMote with the
    //  moteLifespan engine, CustomTransformation_Mote's periodicRandRot,
    //  misc.flipped). Settings live on the mote ThingDef:
    //    <modExtensions><li Class="Heyra.MoteAnimationExtension">…
    // ═══════════════════════════════════════════════════════════
    public class MoteAnimationExtension : DefModExtension
    {
        /// <summary>Frames advance with the mote's own age, so every mote plays its animation from frame 0.</summary>
        public int ticksPerFrame = 3;

        /// <summary>Draw mirrored left-to-right (Bando's AFlipped / ZFlipped slashes).</summary>
        public bool flipped;

        /// <summary>Every this many ticks the mote turns by a random angle (MoHAR periodicRandRot). 0 = off.</summary>
        public IntRange randomRotationPeriod = IntRange.Zero;
        public FloatRange randomRotationAngle = new FloatRange(0f, 359f);
    }

    /// <summary>
    /// Flipbook for motes, frame-locked to the mote's age like MoHAR's Graphic_AnimatedMote. Unlike
    /// Graphic_MoteFlicker (real-time loop, made for flame hair) it draws the way vanilla Graphic_Mote does:
    /// fade (Alpha), instance colour, exactRotation and Scale all apply.
    /// </summary>
    public class Graphic_AnimatedMote : Graphic_Collection
    {
        private static readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        public override Material MatSingle =>
            subGraphics.NullOrEmpty() ? BaseContent.BadMat : subGraphics[0].MatSingle;

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            if (!(thing is Mote mote) || subGraphics.NullOrEmpty()) return;
            float alpha = mote.Alpha;
            if (alpha <= 0f) return;

            MoteAnimationExtension anim = thingDef.GetModExtension<MoteAnimationExtension>();
            int ticksPerFrame = Mathf.Max(1, anim?.ticksPerFrame ?? 3);
            int age = Find.TickManager.TicksGame - mote.spawnTick;
            Material mat = subGraphics[(age / ticksPerFrame) % subGraphics.Length].MatSingle;

            Color color = Color * mote.instanceColor;
            color.a *= alpha;
            Vector3 scale = mote.ExactScale;
            scale.x *= drawSize.x;
            scale.z *= drawSize.y;
            Matrix4x4 matrix = Matrix4x4.TRS(mote.DrawPos, Quaternion.AngleAxis(mote.exactRotation, Vector3.up), scale);
            Mesh mesh = anim != null && anim.flipped ? MeshPool.plane10Flip : MeshPool.plane10;

            if (color.IndistinguishableFrom(mat.color))
            {
                Graphics.DrawMesh(mesh, matrix, mat, 0);
                return;
            }
            propertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            Graphics.DrawMesh(mesh, matrix, mat, 0, null, 0, propertyBlock);
        }

        public override string ToString() =>
            "AnimatedMote(subGraphic[0]=" + subGraphics?[0] + ", count=" + (subGraphics?.Length ?? 0) + ")";
    }

    /// <summary>
    /// A thrown mote that stays where it's put (no velocity) and, if its def asks for it, jitters to a
    /// random angle every few ticks: the electric flicker of Bando's sparks.
    /// </summary>
    public class Mote_Animated : MoteThrown
    {
        private int rotationPeriod;

        protected override void Tick()
        {
            base.Tick();
            if (Destroyed) return;
            MoteAnimationExtension anim = def.GetModExtension<MoteAnimationExtension>();
            if (anim == null || anim.randomRotationPeriod.max <= 0) return;
            if (rotationPeriod <= 0) rotationPeriod = Mathf.Max(1, anim.randomRotationPeriod.RandomInRange);
            if (!this.IsHashIntervalTick(rotationPeriod)) return;
            exactRotation += (Rand.Bool ? 1f : -1f) * anim.randomRotationAngle.RandomInRange;
            rotationPeriod = Mathf.Max(1, anim.randomRotationPeriod.RandomInRange);
        }
    }

    public static class HeyraVfx
    {
        /// <summary>Spawns a mote at an exact position with a scale, the way MoHAR's TryAnyMoteSpawn did.</summary>
        public static Mote SpawnMote(ThingDef moteDef, Vector3 loc, Map map, float scale, float rotation = 0f)
        {
            if (moteDef == null || map == null || !loc.ShouldSpawnMotesAt(map)) return null;
            if (!(ThingMaker.MakeThing(moteDef) is Mote mote)) return null;
            mote.Scale = scale;
            mote.exactRotation = rotation;
            mote.exactPosition = loc;
            GenSpawn.Spawn(mote, loc.ToIntVec3(), map);
            return mote;
        }
    }
}
