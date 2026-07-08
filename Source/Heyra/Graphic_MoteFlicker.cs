using UnityEngine;
using Verse;

namespace Heyra
{
    /// <summary>
    /// Drop-in replacement for Graphic_Flicker designed for motes instead of fire.
    ///
    /// Graphic_Flicker hardcodes its scale from Fire.fireSize and always uses
    /// MeshPool.plane10, ignoring both drawSize and mote.exactScale. This makes
    /// it impossible to control mote size through XML.
    ///
    /// Graphic_MoteFlicker provides smooth sequential frame cycling and respects
    /// graphicData drawSize for scaling. Animation runs on real-time so flames
    /// stay alive even when the game is paused. Use in XML:
    ///   <graphicClass>Heyra.Graphic_MoteFlicker</graphicClass>
    ///   <drawSize>3.5</drawSize>
    /// </summary>
    public class Graphic_MoteFlicker : Graphic_Collection
    {
        /// <summary>
        /// Animation speed in frames per second (real-time).
        /// With 7 frames (A-G), 12 fps = full cycle every ~0.58s — a natural flame pace.
        /// </summary>
        private const float FramesPerSecond = 12f;

        /// <summary>
        /// Returns the current animation frame's material.
        /// Sequential cycling based on real-time, with a per-call random offset so
        /// multiple motes reading MatSingle in the same frame aren't all identical.
        /// </summary>
        public override Material MatSingle
        {
            get
            {
                if (subGraphics == null || subGraphics.Length == 0)
                    return BaseContent.BadMat;
                int frameIdx = Mathf.FloorToInt(Time.time * FramesPerSecond) % subGraphics.Length;
                return subGraphics[frameIdx].MatSingle;
            }
        }

        /// <summary>
        /// Draws the graphic with smooth sequential frame cycling and proper drawSize scaling.
        ///
        /// Improvements over Graphic_Flicker:
        ///  - Sequential frames (A→B→C→…→G→A) instead of random hash jumps
        ///  - Real-time clock so flames animate during pause and aren't affected by game speed
        ///  - Per-mote phase offset so overlapping motes look organic, not synchronized
        ///  - drawSize from XML controls the render scale (Graphic_Flicker ignores it)
        /// </summary>
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            if (subGraphics == null || subGraphics.Length == 0)
                return;

            // Per-mote phase offset — each mote starts at a slightly different point
            // in the animation cycle so overlapping motes look like natural flickering
            float phaseOffset = (thing != null) ? (thing.thingIDNumber % 100) * 0.037f : 0f;

            // Sequential frame cycling on real-time clock
            int frameIdx = Mathf.FloorToInt((Time.time + phaseOffset) * FramesPerSecond) % subGraphics.Length;

            Graphic graphic = subGraphics[frameIdx];

            // Apply graphicData.drawOffset — Graphic_Flicker does this explicitly
            // and mote draw paths don't add it automatically
            Vector3 pos = loc;
            if (data != null)
                pos += data.drawOffset;

            // Use drawSize from graphicData — fully XML-configurable
            // NOTE: Graphic_MoteFlicker ignores the effecter sub-effecter 'scale' field.
            // To resize these motes, change <drawSize> on the mote ThingDef directly.
            Vector3 s = new Vector3(drawSize.x, 1f, drawSize.y);

            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(pos, Quaternion.identity, s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, graphic.MatSingle, 0);
        }

        public override string ToString()
        {
            return string.Concat("MoteFlicker(subGraphic[0]=", subGraphics?[0]?.ToString(),
                ", count=", subGraphics?.Length ?? 0, ", drawSize=", drawSize, ")");
        }
    }
}
