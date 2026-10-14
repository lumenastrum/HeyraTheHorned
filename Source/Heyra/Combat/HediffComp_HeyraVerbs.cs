using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════════
    //  HEYRA HEDIFF VERBS — Heyra-owned replacement for MVCF's
    //  HediffCompProperties_ExtendedVerbGiver (breath + static field).
    //
    //  Builds on vanilla HediffComp_VerbGiver (it owns + saves the verbs).
    //  Two kinds of verb:
    //    - command verbs (hasStandardCommand): the pawn's ranged attack.
    //      Patch_HeyraVerbs makes Pawn.TryGetAttackVerb return it, so
    //      drafted auto-attack and NPC AI use it; the player gets a button.
    //    - turret verbs (label listed in turretVerbs): fire on their own,
    //      each with its own target / warmup / cooldown, without touching
    //      the pawn's stance (the static field's four range bands).
    // ═══════════════════════════════════════════════════════════════

    public class HediffCompProperties_HeyraVerbs : HediffCompProperties_VerbGiver
    {
        /// <summary>Labels of verbs that fire independently as turrets.</summary>
        public List<string> turretVerbs = new List<string>();
        /// <summary>Icon for the command button of command verbs.</summary>
        public string commandIcon;

        public HediffCompProperties_HeyraVerbs()
        {
            compClass = typeof(HediffComp_HeyraVerbs);
        }
    }

    public class HediffComp_HeyraVerbs : HediffComp_VerbGiver
    {
        // Per-verb turret state, parallel to VerbTracker.AllVerbs. Not every index is a turret.
        private List<int> warmup = new List<int>();
        private List<int> cooldown = new List<int>();
        private List<Thing> target = new List<Thing>();

        public new HediffCompProperties_HeyraVerbs Props => (HediffCompProperties_HeyraVerbs)props;

        public bool IsTurret(Verb v) => Props.turretVerbs != null && Props.turretVerbs.Contains(v.verbProps.label);

        /// <summary>The command (non-turret, ranged) verb the pawn should use as its attack, if any.</summary>
        public Verb CommandVerb
        {
            get
            {
                foreach (Verb v in VerbTracker.AllVerbs)
                    if (!v.verbProps.IsMeleeAttack && !IsTurret(v) && v.verbProps.hasStandardCommand)
                        return v;
                return null;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed) return;

            List<Verb> verbs = VerbTracker.AllVerbs;
            EnsureState(verbs.Count);
            for (int i = 0; i < verbs.Count; i++)
                if (IsTurret(verbs[i]))
                    TickTurret(pawn, verbs[i], i);
        }

        private void EnsureState(int n)
        {
            while (warmup.Count < n) warmup.Add(-1);
            while (cooldown.Count < n) cooldown.Add(0);
            while (target.Count < n) target.Add(null);
        }

        private void TickTurret(Pawn pawn, Verb verb, int i)
        {
            if (cooldown[i] > 0) { cooldown[i]--; return; }

            if (!ValidTarget(pawn, verb, target[i]))
            {
                target[i] = FindTarget(pawn, verb);
                warmup[i] = -1;
                if (target[i] == null) return;
            }

            if (warmup[i] < 0)
            {
                // This tick counts as the first warmup tick (a vanilla warmup starts on the cast tick too):
                // 0.1 s warmup + 1.0 s cooldown = one shot every 67 ticks, same as the MVCF turrets.
                warmup[i] = Mathf.Max(0, verb.verbProps.warmupTime.SecondsToTicks() - 1);
                return;
            }
            if (warmup[i] > 0) { warmup[i]--; return; }

            Fire(pawn, verb, target[i]);
            warmup[i] = -1;
            cooldown[i] = verb.verbProps.defaultCooldownTime.SecondsToTicks();
        }

        private static bool ValidTarget(Pawn pawn, Verb verb, Thing t)
        {
            if (!(t is Pawn p) || !p.Spawned || p.Dead || p.Downed || p.Map != pawn.Map) return false;
            if (!p.HostileTo(pawn)) return false;
            float d = pawn.Position.DistanceTo(p.Position);
            if (d < verb.verbProps.minRange || d > verb.verbProps.range) return false;
            return GenSight.LineOfSight(pawn.Position, p.Position, pawn.Map, skipFirstCell: true);
        }

        private static Thing FindTarget(Pawn pawn, Verb verb)
        {
            Thing best = null;
            float bestDist = float.MaxValue;
            foreach (IAttackTarget at in pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn))
            {
                Thing t = at.Thing;
                if (!ValidTarget(pawn, verb, t)) continue;
                float d = pawn.Position.DistanceToSquared(t.Position);
                if (d < bestDist) { bestDist = d; best = t; }
            }
            return best;
        }

        private static void Fire(Pawn pawn, Verb verb, Thing t)
        {
            ThingDef projDef = verb.verbProps.defaultProjectile;
            if (projDef == null) return;
            var proj = (Projectile)GenSpawn.Spawn(projDef, pawn.Position, pawn.Map);
            proj.Launch(pawn, pawn.DrawPos, t, t, ProjectileHitFlags.IntendedTarget, preventFriendlyFire: false);
            verb.verbProps.soundCast?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            verb.verbProps.soundCastTail?.PlayOneShotOnCamera(pawn.Map);
            Find.BattleLog.Add(new BattleLogEntry_RangedFire(pawn, t, null, projDef, burst: false));
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            IEnumerable<Gizmo> baseGizmos = base.CompGetGizmos();   // vanilla returns null, not empty
            if (baseGizmos != null)
                foreach (Gizmo g in baseGizmos) yield return g;
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.IsColonistPlayerControlled || !pawn.Drafted) yield break;
            Verb v = CommandVerb;
            if (v == null) yield break;
            yield return new Command_VerbTarget
            {
                verb = v,
                defaultLabel = v.verbProps.label.CapitalizeFirst(),
                defaultDesc = parent.def.description,
                icon = HeyraCombatTextures.CommandIcon(Props.commandIcon),
                ownerThing = pawn
            };
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Collections.Look(ref warmup, "heyraTurretWarmup", LookMode.Value);
            Scribe_Collections.Look(ref cooldown, "heyraTurretCooldown", LookMode.Value);
            Scribe_Collections.Look(ref target, "heyraTurretTarget", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                warmup ??= new List<int>();
                cooldown ??= new List<int>();
                target ??= new List<Thing>();
            }
        }
    }

    [StaticConstructorOnStartup]
    internal static class HeyraCombatTextures
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly Texture2D Fallback = ContentFinder<Texture2D>.Get("Icon/Heyra_Icon_Formchange", reportFailure: false);

        public static Texture2D CommandIcon(string path)
        {
            if (path.NullOrEmpty()) return Fallback ?? BaseContent.BadTex;
            if (!Cache.TryGetValue(path, out Texture2D tex))
                Cache[path] = tex = ContentFinder<Texture2D>.Get(path, reportFailure: false) ?? Fallback ?? BaseContent.BadTex;
            return tex;
        }
    }
}
