using UnityEngine;
using Verse;

namespace Heyra
{
    /// <summary>
    /// Comp that holds the awakened projectile reference for soul weapons.
    /// The Verb_SoulShoot checks this comp to find the enhanced projectile
    /// when the wielder has the Enableformchange hediff.
    /// Optionally also an awakened LOOK: in an awakened Heyra's hands the weapon is drawn from
    /// a looping set of frames instead (Patch_SoulWeaponAwakenedDraw), e.g. the soul bow's spirit fire.
    /// </summary>
    public class CompProperties_SoulWeapon : CompProperties
    {
        public ThingDef awakenedProjectile;

        /// <summary>Frame textures are &lt;awakenedTexPath&gt;0 .. &lt;awakenedTexPath&gt;(awakenedFrames-1).</summary>
        public string awakenedTexPath;
        public int awakenedFrames;
        public float awakenedFps = 10f;

        public CompProperties_SoulWeapon()
        {
            compClass = typeof(CompSoulWeapon);
        }
    }

    public class CompSoulWeapon : ThingComp
    {
        private Material[] awakenedMats;

        public CompProperties_SoulWeapon Props =>
            (CompProperties_SoulWeapon)props;

        public bool HasAwakenedLook => Props.awakenedFrames > 0 && !Props.awakenedTexPath.NullOrEmpty();

        public static bool IsAwakened(Pawn pawn) =>
            pawn?.health?.hediffSet?.HasHediff(HeyraHediffDefOf.Heyra_Hediff_Enableformchange) == true;

        /// <summary>The current awakened frame. Real-time, so the fire keeps moving while paused (like flame hair).</summary>
        public Material AwakenedFrame()
        {
            if (!HasAwakenedLook) return null;
            if (awakenedMats == null)
            {
                awakenedMats = new Material[Props.awakenedFrames];
                for (int i = 0; i < awakenedMats.Length; i++)
                    awakenedMats[i] = MaterialPool.MatFrom(Props.awakenedTexPath + i, ShaderDatabase.Transparent);
            }
            float phase = (parent.thingIDNumber % 100) * 0.037f;
            return awakenedMats[Mathf.FloorToInt((Time.time + phase) * Props.awakenedFps) % awakenedMats.Length];
        }
    }
}
