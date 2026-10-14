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

        /// <summary>Optional additive fire-only frames (same count), drawn above RimWorld's darkness so the
        /// spirit fire shines at night. Strength scales their colour.</summary>
        public string awakenedGlowTexPath;
        public float awakenedGlowStrength = 0.6f;

        public CompProperties_SoulWeapon()
        {
            compClass = typeof(CompSoulWeapon);
        }
    }

    public class CompSoulWeapon : ThingComp
    {
        private Material[] awakenedMats;
        private Material[] glowMats;

        public CompProperties_SoulWeapon Props =>
            (CompProperties_SoulWeapon)props;

        public bool HasAwakenedLook => Props.awakenedFrames > 0 && !Props.awakenedTexPath.NullOrEmpty();

        public static bool IsAwakened(Pawn pawn) =>
            pawn?.health?.hediffSet?.HasHediff(HeyraHediffDefOf.Heyra_Hediff_Enableformchange) == true;

        /// <summary>The current awakened frame (and its glow frame, or null). Real-time, so the fire keeps
        /// moving while paused (like flame hair).</summary>
        public Material AwakenedFrame(out Material glow)
        {
            glow = null;
            if (!HasAwakenedLook) return null;
            if (awakenedMats == null)
            {
                awakenedMats = new Material[Props.awakenedFrames];
                for (int i = 0; i < awakenedMats.Length; i++)
                    awakenedMats[i] = MaterialPool.MatFrom(Props.awakenedTexPath + i, ShaderDatabase.Transparent);
                if (!Props.awakenedGlowTexPath.NullOrEmpty())
                {
                    float s = Props.awakenedGlowStrength;
                    glowMats = new Material[Props.awakenedFrames];
                    for (int i = 0; i < glowMats.Length; i++)
                        glowMats[i] = MaterialPool.MatFrom(Props.awakenedGlowTexPath + i, ShaderDatabase.MoteGlow, new Color(s, s, s, 1f));
                }
            }
            float phase = (parent.thingIDNumber % 100) * 0.037f;
            int f = Mathf.FloorToInt((Time.time + phase) * Props.awakenedFps) % awakenedMats.Length;
            glow = glowMats?[f];
            return awakenedMats[f];
        }
    }
}
