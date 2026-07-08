using Verse;

namespace Heyra
{
    /// <summary>
    /// Comp that holds the awakened projectile reference for soul weapons.
    /// The Verb_SoulShoot checks this comp to find the enhanced projectile
    /// when the wielder has the Enableformchange hediff.
    /// </summary>
    public class CompProperties_SoulWeapon : CompProperties
    {
        public ThingDef awakenedProjectile;

        public CompProperties_SoulWeapon()
        {
            compClass = typeof(CompSoulWeapon);
        }
    }

    public class CompSoulWeapon : ThingComp
    {
        public CompProperties_SoulWeapon Props =>
            (CompProperties_SoulWeapon)props;
    }
}
