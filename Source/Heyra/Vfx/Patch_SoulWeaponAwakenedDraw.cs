using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Heyra
{
    /// <summary>
    /// An awakened Heyra's soul weapon is drawn from its awakened frames (CompSoulWeapon) instead of its
    /// normal graphic. Same placement maths as vanilla PawnRenderUtility.DrawEquipmentAiming (1.6),
    /// so it sits in her hands exactly like the everyday sprite; only the material changes.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class Patch_SoulWeaponAwakenedDraw
    {
        public static bool Prefix(Thing eq, Vector3 drawLoc, float aimAngle)
        {
            CompSoulWeapon comp = eq.TryGetComp<CompSoulWeapon>();
            if (comp == null || !comp.HasAwakenedLook) return true;
            Pawn holder = (eq.ParentHolder as Pawn_EquipmentTracker)?.pawn;
            if (!CompSoulWeapon.IsAwakened(holder)) return true;
            Material mat = comp.AwakenedFrame();
            if (mat == null) return true;

            float angle = aimAngle - 90f;
            Mesh mesh;
            if (aimAngle > 200f && aimAngle < 340f)
            {
                mesh = MeshPool.plane10Flip;
                angle -= 180f;
                angle -= eq.def.equippedAngleOffset;
            }
            else
            {
                mesh = MeshPool.plane10;
                angle += eq.def.equippedAngleOffset;
            }
            angle %= 360f;
            CompEquippable equippable = eq.TryGetComp<CompEquippable>();
            if (equippable != null)
            {
                EquipmentUtility.Recoil(eq.def, EquipmentUtility.GetRecoilVerb(equippable.AllVerbs),
                    out Vector3 drawOffset, out float angleOffset, aimAngle);
                drawLoc += drawOffset;
                angle += angleOffset;
            }
            Vector2 size = eq.Graphic.drawSize;
            Matrix4x4 matrix = Matrix4x4.TRS(drawLoc, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(size.x, 0f, size.y));
            Graphics.DrawMesh(mesh, matrix, mat, 0);
            return false;
        }
    }
}
