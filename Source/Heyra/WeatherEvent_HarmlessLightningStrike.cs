using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Heyra
{
    /// <summary>
    /// Cosmetic lightning bolt — all flash, no damage.
    /// Triggered when Ancestral Recall activates.
    /// </summary>
    [StaticConstructorOnStartup]
    public class WeatherEvent_HarmlessLightningStrike : WeatherEvent_LightningFlash
    {
        private IntVec3 strikeLoc = IntVec3.Invalid;
        private Mesh boltMesh;

        private static readonly Material LightningMat = MatLoader.LoadMat("Weather/LightningBolt");

        public WeatherEvent_HarmlessLightningStrike(Map map)
            : base(map)
        {
        }

        public WeatherEvent_HarmlessLightningStrike(Map map, IntVec3 forcedStrikeLoc)
            : base(map)
        {
            strikeLoc = forcedStrikeLoc;
        }

        public override void FireEvent()
        {
            base.FireEvent();

            if (!strikeLoc.IsValid)
            {
                strikeLoc = CellFinderLoose.RandomCellWith(
                    sq => sq.Standable(map) && !map.roofGrid.Roofed(sq), map, 1000);
            }

            boltMesh = LightningBoltMeshPool.RandomBoltMesh;

            if (!strikeLoc.Fogged(map))
            {
                Vector3 pos = strikeLoc.ToVector3Shifted();
                for (int i = 0; i < 4; i++)
                {
                    FleckMaker.ThrowSmoke(pos, map, 1.5f);
                    FleckMaker.ThrowMicroSparks(pos, map);
                    FleckMaker.ThrowLightningGlow(pos, map, 1.5f);
                }
            }

            SoundDefOf.Thunder_OnMap.PlayOneShot(
                SoundInfo.InMap(new TargetInfo(strikeLoc, map)));
        }

        public override void WeatherEventDraw()
        {
            Graphics.DrawMesh(boltMesh,
                strikeLoc.ToVector3ShiftedWithAltitude(AltitudeLayer.Weather),
                Quaternion.identity,
                FadedMaterialPool.FadedVersionOf(LightningMat, LightningBrightness),
                0);
        }
    }
}
