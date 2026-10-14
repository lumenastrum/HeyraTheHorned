using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════════
    //  HEYRA BEAM — Heyra-owned piercing beam projectile, replacing
    //  VEF.Weapons.GaussProjectile for the breath, the static field's
    //  shocks and Celestial Whisper, and giving the Soul Great Bow its
    //  spectral lance.
    //
    //  Lifecycle:
    //    Launch    remember start + end (end = target, or full range if
    //              extendToRange; cut short by the first full wall)
    //    travel    the head moves at projectile.speed (tiles/tick = speed/100);
    //              every pawn the beam passes over is hit once
    //    fade      after the head arrives, play fade frames, then vanish
    //
    //  The start point is stored at launch, so the beam never reads the
    //  launcher again for its geometry: if the launcher dies or despawns
    //  mid-beam it just finishes. (VEF's version threw every tick forever.)
    // ═══════════════════════════════════════════════════════════════

    public class HeyraBeamProps : DefModExtension
    {
        /// <summary>Folder of travel frames (shown while the head moves). Optional.</summary>
        public string travelFrames;
        /// <summary>Folder of fade frames (shown after the head arrives). Optional.</summary>
        public string fadeFrames;
        /// <summary>Optional sprite drawn at the head while it travels (e.g. the soul arrow).</summary>
        public string headTexture;
        public float headSize = 1.5f;
        public ShaderTypeDef shader;
        public Color color = Color.white;
        /// <summary>Beam width in cells (drawing), in texture widths: a thin bolt texture needs a big value.</summary>
        public float width = 1f;
        /// <summary>Width used for the fade frames (0 = width). Lets a thin trail texture and a full-width
        /// burst texture share one beam.</summary>
        public float fadeWidth = 0f;
        /// <summary>Pawns whose center is within hitWidth/2 of the beam line are hit (0 = only cells on the line).
        /// Breath uses a wide value so its forcedMissRadius wobble still catches the target, like VEF's arc.</summary>
        public float hitWidth = 0f;
        /// <summary>Ticks each travel frame is shown.</summary>
        public int frameTicks = 3;
        /// <summary>Ticks each fade frame is shown (0 = frameTicks).</summary>
        public int fadeFrameTicks = 0;
        /// <summary>Go through built walls; stop only at natural rock and ore (Celestial Whisper).</summary>
        public bool passThroughBuiltWalls = false;

        public int FadeFrameTicks => fadeFrameTicks > 0 ? fadeFrameTicks : Mathf.Max(1, frameTicks);
        /// <summary>Cells in front of the launcher where the beam starts.</summary>
        public float startForward = 0.5f;
        /// <summary>Within this many cells of the start only the intended target can be hit
        /// (VEF's minDistanceToAffect: the piercing line starts here, the target is always hit).</summary>
        public float minDistance = 0f;
        /// <summary>Ticks the beam stays on its travel frame after arriving, before the fade frames.</summary>
        public int holdTicks = 0;
        /// <summary>Keep going to full range past the target (else stop at the target cell).</summary>
        public bool extendToRange = false;
        /// <summary>Range used by extendToRange when the shot isn't from a weapon. 0 = target distance.</summary>
        public float range = 0f;
        /// <summary>Damage multiplier per pawn already hit (1 = no falloff).</summary>
        public float pierceFalloff = 1f;
        /// <summary>Stop after hitting this many pawns (0 = unlimited).</summary>
        public int maxTargets = 0;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors()) yield return e;
            if (travelFrames.NullOrEmpty() && fadeFrames.NullOrEmpty())
                yield return "HeyraBeamProps needs travelFrames and/or fadeFrames";
        }
    }

    public class Projectile_HeyraBeam : Projectile
    {
        private Vector3 beamStart;
        private Vector3 beamEnd;
        private float traveled;
        private int fadeTick = -1;
        private int age;
        private int pawnsHit;
        private HashSet<int> hitIds = new HashSet<int>();

        private static readonly Dictionary<string, Material[]> FrameCache = new Dictionary<string, Material[]>();

        private HeyraBeamProps Props => def.GetModExtension<HeyraBeamProps>() ?? FallbackProps;
        private static readonly HeyraBeamProps FallbackProps = new HeyraBeamProps();
        private float Length => (beamEnd - beamStart).MagnitudeHorizontal();
        private Vector3 Dir => (beamEnd - beamStart).Yto0().normalized;
        private Vector3 Head => beamStart + Dir * Mathf.Min(traveled, Length);
        private bool Arrived => traveled >= Length;

        public override Vector3 ExactPosition => Head;
        public override Quaternion ExactRotation => Quaternion.LookRotation(Dir == Vector3.zero ? Vector3.forward : Dir);

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            HeyraBeamProps p = Props;

            Vector3 target = usedTarget.HasThing ? usedTarget.Thing.DrawPos : usedTarget.Cell.ToVector3Shifted();
            Vector3 dir = (target - origin).Yto0();
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();

            beamStart = origin.Yto0() + dir * p.startForward;
            float reach = (target - origin).MagnitudeHorizontal();
            if (p.extendToRange)
                reach = Mathf.Max(reach, WeaponRange(equipment, p));
            beamEnd = ClipToWalls(beamStart, beamStart + dir * Mathf.Max(0.5f, reach - p.startForward));
        }

        private static float WeaponRange(Thing equipment, HeyraBeamProps p)
        {
            float r = equipment?.TryGetComp<CompEquippable>()?.PrimaryVerb?.verbProps?.range ?? 0f;
            return r > 0f ? r : p.range;
        }

        /// <summary>Shorten the beam to the first full-fill wall or natural rock on the line.</summary>
        private Vector3 ClipToWalls(Vector3 from, Vector3 to)
        {
            Map map = launcher?.Map ?? MapHeld;
            if (map == null) return to;
            IntVec3 startCell = from.ToIntVec3();
            foreach (IntVec3 c in GenSight.PointsOnLineOfSight(startCell, to.ToIntVec3()))
            {
                if (c == startCell || !c.InBounds(map)) continue;
                Building b = c.GetEdifice(map);
                if (b == null) continue;
                bool rock = b.def.mineable || (b.def.building?.isNaturalRock ?? false) || (b.def.building?.isResourceRock ?? false);
                if (rock || (!Props.passThroughBuiltWalls && b.def.Fillage == FillCategory.Full))
                    return c.ToVector3Shifted().Yto0();
            }
            return to;
        }

        protected override void TickInterval(int delta)
        {
            // No base call: vanilla would fly to the target and roll an Impact. The beam owns its lifecycle.
            age += delta;
            if (!Arrived)
            {
                float before = traveled;
                traveled = Mathf.Min(Length, traveled + def.projectile.SpeedTilesPerTick * delta);
                DamageAlong(before, traveled);
                if (Destroyed) return;
                if (Arrived) fadeTick = 0;
            }
            else
            {
                fadeTick += delta;
                int fadeLength = Mathf.Max(1, FadeFrames.Length) * Props.FadeFrameTicks;
                if (fadeTick >= Props.holdTicks + fadeLength)
                    Destroy();
            }
        }

        private void DamageAlong(float from, float to)
        {
            Map map = Map;
            if (map == null) return;
            HeyraBeamProps p = Props;
            Vector3 dir = Dir;
            Vector3 a = beamStart + dir * from, b = beamStart + dir * to;
            float reach = p.hitWidth > 0f ? p.hitWidth / 2f + 0.5f : 0f;
            var cells = new HashSet<IntVec3>();
            foreach (IntVec3 c in GenSight.PointsOnLineOfSight(a.ToIntVec3(), b.ToIntVec3()))
            {
                if (reach <= 0f) { cells.Add(c); continue; }
                foreach (IntVec3 n in GenRadial.RadialCellsAround(c, reach, true)) cells.Add(n);
            }

            foreach (IntVec3 c in cells)
            {
                if (!c.InBounds(map)) continue;
                List<Thing> things = map.thingGrid.ThingsListAtFast(c);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    if (!(things[i] is Pawn pawn) || hitIds.Contains(pawn.thingIDNumber)) continue;
                    Vector3 rel = pawn.Position.ToVector3Shifted().Yto0() - beamStart;
                    float along = Vector3.Dot(rel, dir);
                    if (along < 0f || along > to + 0.5f) continue;
                    if (reach > 0f && (rel - dir * along).MagnitudeHorizontal() > reach) continue;
                    bool intended = intendedTarget.Thing == pawn;
                    if (along < p.minDistance && !intended) continue;

                    hitIds.Add(pawn.thingIDNumber);   // judged once per beam (friendly-fire roll included)
                    if (!ShouldHit(pawn)) continue;
                    HitPawn(pawn, p);
                    if (p.maxTargets > 0 && pawnsHit >= p.maxTargets)
                    {
                        beamEnd = beamStart + dir * to;   // stop here; fade from this point
                        traveled = Length;
                        return;
                    }
                }
            }
        }

        private bool ShouldHit(Pawn pawn)
        {
            if (pawn == launcher || pawn.Dead || !pawn.Spawned) return false;
            bool intended = intendedTarget.Thing == pawn;
            if (intended) return true;
            if (pawn.Downed) return false;
            if (launcher?.Faction != null && pawn.Faction != null && !pawn.Faction.HostileTo(launcher.Faction))
            {
                if (preventFriendlyFire) return false;
                return Rand.Chance(Find.Storyteller.difficulty.friendlyFireChanceFactor);
            }
            return true;
        }

        private void HitPawn(Pawn pawn, HeyraBeamProps p)
        {
            float mult = Mathf.Pow(Mathf.Clamp01(p.pierceFalloff), pawnsHit);
            pawnsHit++;
            float angle = ExactRotation.eulerAngles.y;
            ThingDef weaponDef = equipmentDef;

            BattleLogEntry_RangedImpact entry = null;
            if (launcher != null && !launcher.Destroyed)
            {
                entry = new BattleLogEntry_RangedImpact(launcher, pawn, intendedTarget.Thing, weaponDef, def, targetCoverDef);
                Find.BattleLog.Add(entry);
            }

            var dinfo = new DamageInfo(def.projectile.damageDef, def.projectile.GetDamageAmount(equipment) * mult,
                def.projectile.GetArmorPenetration(equipment), angle, launcher, null, weaponDef,
                DamageInfo.SourceCategory.ThingOrUnknown, intendedTarget.Thing);
            DamageWorker.DamageResult result = pawn.TakeDamage(dinfo);
            if (entry != null) result.AssociateWithLog(entry);

            if (def.projectile.extraDamages != null && !pawn.Dead)
            {
                foreach (ExtraDamage extra in def.projectile.extraDamages)
                {
                    if (!Rand.Chance(extra.chance)) continue;
                    pawn.TakeDamage(new DamageInfo(extra.def, extra.amount * mult, extra.AdjustedArmorPenetration(), angle,
                        launcher, null, weaponDef, DamageInfo.SourceCategory.ThingOrUnknown, intendedTarget.Thing));
                }
            }
        }

        // ── drawing ──────────────────────────────────────────────────────────

        private Material[] TravelFrames => Frames(Props.travelFrames);
        private Material[] FadeFrames => Frames(Props.fadeFrames);

        private Material[] Frames(string folder)
        {
            if (folder.NullOrEmpty()) return System.Array.Empty<Material>();
            string key = folder + "|" + Props.color + "|" + Props.shader?.defName;
            if (!FrameCache.TryGetValue(key, out Material[] mats))
            {
                Shader shader = Props.shader?.Shader ?? ShaderDatabase.MoteGlow;
                var list = new List<Material>();
                List<Texture2D> textures = new List<Texture2D>(ContentFinder<Texture2D>.GetAllInFolder(folder));
                textures.SortBy(t => t.name);
                foreach (Texture2D tex in textures)
                    list.Add(MaterialPool.MatFrom(new MaterialRequest(tex, shader, Props.color)));
                if (list.Count == 0)
                {
                    Texture2D single = ContentFinder<Texture2D>.Get(folder, reportFailure: false);
                    if (single != null) list.Add(MaterialPool.MatFrom(new MaterialRequest(single, shader, Props.color)));
                }
                mats = list.ToArray();
                FrameCache[key] = mats;
            }
            return mats;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            HeyraBeamProps p = Props;
            Material[] frames;
            int index;
            float width = p.width;
            if (!Arrived || FadeFrames.Length == 0 || fadeTick < p.holdTicks)
            {
                frames = TravelFrames.Length > 0 ? TravelFrames : FadeFrames;
                index = age / Mathf.Max(1, p.frameTicks);
                if (frames.Length > 0) index %= frames.Length;
            }
            else
            {
                frames = FadeFrames;
                index = (fadeTick - p.holdTicks) / p.FadeFrameTicks;
                if (p.fadeWidth > 0f) width = p.fadeWidth;
            }
            Vector3 head = Head;
            float length = (head - beamStart).MagnitudeHorizontal();
            Quaternion rot = Quaternion.LookRotation(Dir == Vector3.zero ? Vector3.forward : Dir);

            if (frames.Length > 0 && length >= 0.05f)
            {
                Material mat = frames[Mathf.Clamp(index, 0, frames.Length - 1) % frames.Length];
                Vector3 center = (beamStart + head) / 2f;
                center.y = def.Altitude;
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(center, rot, new Vector3(width, 1f, length)), mat, 0);
            }

            if (!Arrived && !p.headTexture.NullOrEmpty())
            {
                Material headMat = HeadMaterial(p);
                if (headMat != null)
                {
                    Vector3 at = head;
                    at.y = def.Altitude + 0.01f;
                    Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(at, rot, new Vector3(p.headSize, 1f, p.headSize)), headMat, 0);
                }
            }
        }

        private static Material HeadMaterial(HeyraBeamProps p)
        {
            string key = "head|" + p.headTexture + "|" + p.shader?.defName;
            if (!FrameCache.TryGetValue(key, out Material[] mats))
            {
                Texture2D tex = ContentFinder<Texture2D>.Get(p.headTexture, reportFailure: true);
                mats = tex != null
                    ? new[] { MaterialPool.MatFrom(new MaterialRequest(tex, p.shader?.Shader ?? ShaderDatabase.MoteGlow, Color.white)) }
                    : System.Array.Empty<Material>();
                FrameCache[key] = mats;
            }
            return mats.Length > 0 ? mats[0] : null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref beamStart, "heyraBeamStart");
            Scribe_Values.Look(ref beamEnd, "heyraBeamEnd");
            Scribe_Values.Look(ref traveled, "heyraBeamTraveled");
            Scribe_Values.Look(ref fadeTick, "heyraBeamFadeTick", -1);
            Scribe_Values.Look(ref age, "heyraBeamAge");
            Scribe_Values.Look(ref pawnsHit, "heyraBeamPawnsHit");
            Scribe_Collections.Look(ref hitIds, "heyraBeamHitIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && hitIds == null)
                hitIds = new HashSet<int>();
        }
    }
}
