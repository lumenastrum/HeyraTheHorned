using Verse;

namespace Heyra
{
    /// <summary>
    /// Custom ranged verb that swaps to an awakened projectile when
    /// the caster pawn has the Enableformchange hediff (granted by the
    /// Seed of Awakening ritual). Falls back to the default projectile
    /// for non-awakened pawns.
    /// </summary>
    public class Verb_SoulShoot : Verb_Shoot
    {
        public override ThingDef Projectile
        {
            get
            {
                if (CasterPawn != null && IsAwakened(CasterPawn))
                {
                    ThingDef awakened = GetAwakenedProjectile();
                    if (awakened != null)
                        return awakened;
                }
                return base.Projectile;
            }
        }

        private bool IsAwakened(Pawn pawn)
        {
            return pawn.health?.hediffSet?.HasHediff(
                HeyraHediffDefOf.Heyra_Hediff_Enableformchange) == true;
        }

        private ThingDef GetAwakenedProjectile()
        {
            return EquipmentSource?.TryGetComp<CompSoulWeapon>()?.Props.awakenedProjectile;
        }
    }
}
