using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Heyra
{
    /// <summary>
    /// RENDER PATCHES — Written for 1.6's PawnRenderTree system.
    ///
    /// In 1.4, PawnRenderer had a PawnGraphicSet field called "graphics" with
    /// nakedGraphic and headGraphic that could be swapped directly.
    ///
    /// In 1.6, PawnGraphicSet is GONE. PawnRenderer now has a PawnRenderTree
    /// called "renderTree" which manages rendering via PawnRenderNode objects
    /// keyed by PawnRenderNodeTagDef. Graphics are resolved per-node.
    ///
    /// We dirty the render tree when monsterform state changes, and patch
    /// PawnRenderNode_Body.GraphicFor to return the beast graphic during
    /// monsterform.
    /// </summary>
    public static class Patches_Render
    {
        private const string LogPrefix = "[Heyra] ";

        // Per-pawn tracking: thingID of each pawn currently in changed graphic state
        private static readonly HashSet<int> changedPawns = new HashSet<int>();

        // Cached reflection for PawnRenderer.pawn (private field in 1.6)
        private static readonly FieldInfo fi_pawnRenderer_pawn = AccessTools.Field(typeof(PawnRenderer), "pawn");

        private static Pawn GetPawn(PawnRenderer renderer)
        {
            if (fi_pawnRenderer_pawn == null) return null;
            return fi_pawnRenderer_pawn.GetValue(renderer) as Pawn;
        }

        // ═══════════════════════════════════════════════════════════════
        //  GRAPHIC SWAP — Dirty the render tree on monsterform state change.
        //  This forces PawnRenderNode.GraphicFor to re-resolve per node.
        //  The actual body graphic override is in BodyGraphicFor_Postfix.
        // ═══════════════════════════════════════════════════════════════

        public static bool TryPatchGraphicSwap(Harmony harmony)
        {
            string[] candidates = {
                "RenderPawnAt",
                "ParallelGetPreRenderResults",
                "RenderPawnInternal"
            };

            foreach (string methodName in candidates)
            {
                var methods = typeof(PawnRenderer)
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == methodName)
                    .ToArray();

                foreach (var method in methods)
                {
                    try
                    {
                        harmony.Patch(method,
                            prefix: new HarmonyMethod(typeof(Patches_Render), nameof(GraphicSwap_Prefix)));
                        Log.Message(LogPrefix + $"Patched render tree dirty on {method.DeclaringType.Name}.{method.Name}");
                        return true;
                    }
                    catch (Exception e)
                    {
                        Log.Warning(LogPrefix + $"Failed to patch {methodName}: {e.Message}");
                    }
                }
            }

            Log.Error(LogPrefix + "Could not patch graphic swap — visual transformation will not work!");
            return false;
        }

        public static void GraphicSwap_Prefix(PawnRenderer __instance)
        {
            Pawn pawn = GetPawn(__instance);
            if (pawn == null || pawn.Dead) return;

            int id = pawn.thingIDNumber;

            if (pawn.InMonsterform())
            {
                if (!changedPawns.Contains(id))
                {
                    // Entering monsterform — dirty the render tree so it re-resolves
                    pawn.DirtyPawnRenderTree();
                    changedPawns.Add(id);
                    ClearCachedOffset(id);
                }
            }
            else if (changedPawns.Contains(id))
            {
                // Exiting monsterform — dirty tree to restore original graphics
                pawn.DirtyPawnRenderTree();
                changedPawns.Remove(id);
                ClearCachedOffset(id);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  BODY GRAPHIC — Patches PawnRenderNode_Body.GraphicFor to
        //  return the beast graphic when the pawn has monsterform active.
        // ═══════════════════════════════════════════════════════════════

        public static bool TryPatchBodyGraphic(Harmony harmony)
        {
            try
            {
                // PawnRenderNode_Body lives in Verse namespace within Assembly-CSharp
                var nodeBodyType = typeof(PawnRenderNode).Assembly.GetType("Verse.PawnRenderNode_Body");
                if (nodeBodyType == null)
                {
                    Log.Warning(LogPrefix + "PawnRenderNode_Body type not found — body graphic swap unavailable.");
                    return false;
                }

                var graphicFor = AccessTools.Method(nodeBodyType, "GraphicFor", new[] { typeof(Pawn) });
                if (graphicFor == null)
                {
                    // Fallback: search by name
                    graphicFor = nodeBodyType
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == "GraphicFor");
                }

                if (graphicFor == null)
                {
                    Log.Warning(LogPrefix + "PawnRenderNode_Body.GraphicFor method not found.");
                    return false;
                }

                harmony.Patch(graphicFor,
                    postfix: new HarmonyMethod(typeof(Patches_Render), nameof(BodyGraphicFor_Postfix)));
                Log.Message(LogPrefix + "Patched PawnRenderNode_Body.GraphicFor for beast form graphic.");
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + $"Failed to patch body graphic: {e.Message}");
                return false;
            }
        }

        public static void BodyGraphicFor_Postfix(Pawn pawn, ref Graphic __result)
        {
            if (pawn == null || !pawn.InMonsterform()) return;

            var comp = pawn.GetChangeFormComp();
            if (comp?.Props?.graphic == null) return;

            // GraphicData.Graphic auto-resolves from GraphicDatabase on first access
            var beastGraphic = comp.Props.graphic.Graphic;
            if (beastGraphic != null)
                __result = beastGraphic;
        }

        // ═══════════════════════════════════════════════════════════════
        //  HEAD SUPPRESSION — Return null from PawnRenderNode_Head.GraphicFor
        //  during monsterform so head + all child nodes (eyes, hair, etc.)
        //  are suppressed. Also kills the Heyra_Stump red X.
        // ═══════════════════════════════════════════════════════════════

        public static bool TryPatchHeadSuppression(Harmony harmony)
        {
            try
            {
                var nodeHeadType = typeof(PawnRenderNode).Assembly.GetType("Verse.PawnRenderNode_Head");
                if (nodeHeadType == null)
                {
                    Log.Warning(LogPrefix + "PawnRenderNode_Head type not found — head suppression unavailable.");
                    return false;
                }

                var graphicFor = AccessTools.Method(nodeHeadType, "GraphicFor", new[] { typeof(Pawn) });
                if (graphicFor == null)
                {
                    graphicFor = nodeHeadType
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == "GraphicFor");
                }

                if (graphicFor == null)
                {
                    Log.Warning(LogPrefix + "PawnRenderNode_Head.GraphicFor method not found.");
                    return false;
                }

                harmony.Patch(graphicFor,
                    postfix: new HarmonyMethod(typeof(Patches_Render), nameof(HeadGraphicFor_Postfix)));
                Log.Message(LogPrefix + "Patched PawnRenderNode_Head.GraphicFor for monsterform head suppression.");
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + $"Failed to patch head suppression: {e.Message}");
                return false;
            }
        }

        public static void HeadGraphicFor_Postfix(Pawn pawn, ref Graphic __result)
        {
            if (pawn == null || !pawn.InMonsterform()) return;
            __result = null;
        }

        // ═══════════════════════════════════════════════════════════════
        //  MESH SCALING — Overrides MeshSetFor on the body render node
        //  so the beast form mesh is rendered at the correct size.
        //
        //  In 1.6, graphic drawSize does NOT control the mesh. The mesh
        //  is determined by PawnRenderNode.MeshSetFor(Pawn), called once
        //  in the node constructor and cached. We postfix it to return
        //  a scaled mesh when in monsterform.
        // ═══════════════════════════════════════════════════════════════

        private static Type _bodyNodeType;
        private static Type _headNodeType;

        public static bool TryPatchMeshSetFor(Harmony harmony)
        {
            try
            {
                var meshSetFor = AccessTools.Method(typeof(PawnRenderNode), "MeshSetFor", new[] { typeof(Pawn) });
                if (meshSetFor == null)
                {
                    Log.Warning(LogPrefix + "PawnRenderNode.MeshSetFor not found — beast form mesh scaling won't apply.");
                    return false;
                }

                _bodyNodeType = typeof(PawnRenderNode).Assembly.GetType("Verse.PawnRenderNode_Body");
                _headNodeType = typeof(PawnRenderNode).Assembly.GetType("Verse.PawnRenderNode_Head");

                harmony.Patch(meshSetFor,
                    postfix: new HarmonyMethod(typeof(Patches_Render), nameof(MeshSetFor_Postfix)));
                Log.Message(LogPrefix + "Patched PawnRenderNode.MeshSetFor for beast form mesh scaling.");
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + $"Failed to patch MeshSetFor: {e.Message}");
                return false;
            }
        }

        public static void MeshSetFor_Postfix(PawnRenderNode __instance, Pawn pawn, ref GraphicMeshSet __result)
        {
            if (_bodyNodeType == null || !_bodyNodeType.IsInstanceOfType(__instance)) return;
            if (pawn == null || !pawn.InMonsterform()) return;

            var comp = pawn.GetChangeFormComp();
            if (comp == null) return;

            Vector2 size = comp.DrawSize;
            if (size.x <= 0 || size.y <= 0) return;

            __result = MeshPool.GetMeshSetForSize(size.x, size.y);
        }

        // ═══════════════════════════════════════════════════════════════
        //  BODY OFFSET — Postfixes PawnRenderNode.GetTransform to shift
        //  the body render node upward during monsterform.
        //
        //  Previous attempts:
        //  1) Prefix on RenderPawnAt  — 1.6's PawnRenderTree ignores
        //     the drawLoc param, so the offset was never applied.
        //  2) Postfix on Thing.DrawPos — Thing.DrawPos is virtual and
        //     Pawn overrides it (for tweener), so Harmony on the base
        //     getter never intercepts Pawn calls.
        //
        //  GetTransform (ILSpy-confirmed non-virtual) is the single
        //  entry point that produces the offset/pivot/rotation/scale
        //  tuple consumed by PawnRenderTree.TryGetMatrix.  Postfixing
        //  it lets us add floatOffset directly into the render pipeline.
        //
        //  NOTE: This only moves the body *graphic* — not DrawPos, so
        //  MoteAttached objects (flamehair) need their own drawOffset
        //  in XML to reach the elevated neck position.
        // ═══════════════════════════════════════════════════════════════

        // Cache the floatOffset per-pawn to avoid repeated comp lookups every frame
        private static readonly Dictionary<int, Vector2> _cachedOffsets = new Dictionary<int, Vector2>();

        public static bool TryPatchBodyOffset(Harmony harmony)
        {
            try
            {
                var getTransform = AccessTools.Method(typeof(PawnRenderNode), "GetTransform");
                if (getTransform == null)
                {
                    Log.Warning(LogPrefix + "PawnRenderNode.GetTransform not found — body offset won't apply.");
                    return false;
                }

                harmony.Patch(getTransform,
                    postfix: new HarmonyMethod(typeof(Patches_Render), nameof(GetTransform_Postfix)));
                Log.Message(LogPrefix + "Patched PawnRenderNode.GetTransform for monsterform body offset.");
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + $"Failed to patch GetTransform: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Postfix on PawnRenderNode.GetTransform.
        /// The 'offset' out-param becomes ref in Harmony postfixes.
        /// We add floatOffset to body nodes only, keeping head (suppressed)
        /// and other nodes at their default positions.
        /// </summary>
        public static void GetTransform_Postfix(PawnRenderNode __instance, PawnDrawParms parms, ref Vector3 offset)
        {
            if (parms.pawn == null) return;

            // ── Head depth fix: north-facing head renders behind apparel ──
            // HAR body addons have per-direction layerOffset, but vanilla's
            // head node has no directional depth adjustment. When north-facing
            // (we see the pawn's back), the head+hair should render in front
            // of body/apparel. Push head Y forward so it draws on top.
            // Hair/eyes/beard are children of the head node so they cascade.
            if (_headNodeType != null && _headNodeType.IsInstanceOfType(__instance)
                && parms.facing == Rot4.North
                && parms.pawn.IsHeyraRace())
            {
                offset.y += 0.3f;
                return;
            }

            // ── Monsterform body offset ──
            if (_bodyNodeType == null || !_bodyNodeType.IsInstanceOfType(__instance)) return;

            // Fast exit for non-monsterform pawns
            if (!changedPawns.Contains(parms.pawn.thingIDNumber)) return;

            // Use cached offset to avoid comp lookup every frame
            if (!_cachedOffsets.TryGetValue(parms.pawn.thingIDNumber, out Vector2 floatOff))
            {
                var comp = parms.pawn.GetChangeFormComp();
                if (comp == null) return;
                floatOff = comp.Props.floatOffset;
                _cachedOffsets[parms.pawn.thingIDNumber] = floatOff;
            }

            // floatOffset is (x, y) in comp → (x, z) in world coordinates
            offset.x += floatOff.x;
            offset.z += floatOff.y;
        }

        /// <summary>
        /// Called from GraphicSwap_Prefix when monsterform state changes.
        /// Clears cached offset so it's re-read from the comp on next access.
        /// </summary>
        public static void ClearCachedOffset(int thingID)
        {
            _cachedOffsets.Remove(thingID);
        }

        // ═══════════════════════════════════════════════════════════════
        //  FORCE SHOW BODY — Probably not needed in 1.6's render tree.
        // ═══════════════════════════════════════════════════════════════

        public static bool TryPatchForceShowBody(Harmony harmony)
        {
            var method = AccessTools.Method(typeof(PawnRenderer), "GetBodyPos");
            if (method == null)
            {
                Log.Message(LogPrefix + "GetBodyPos not found in 1.6 — skipping (likely not needed with render tree).");
                return false;
            }

            var parameters = method.GetParameters();
            if (!parameters.Any(p => p.Name == "showBody" && p.ParameterType == typeof(bool).MakeByRefType()))
            {
                Log.Message(LogPrefix + "GetBodyPos lacks showBody param — skipping.");
                return false;
            }

            try
            {
                harmony.Patch(method,
                    postfix: new HarmonyMethod(typeof(Patches_Render), nameof(ForceShowBody_Postfix)));
                Log.Message(LogPrefix + "Patched force-show-body on GetBodyPos.");
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + $"Failed to patch GetBodyPos: {e.Message}");
                return false;
            }
        }

        public static void ForceShowBody_Postfix(PawnRenderer __instance, ref bool showBody)
        {
            Pawn pawn = GetPawn(__instance);
            if (pawn != null && pawn.InMonsterform())
                showBody = true;
        }

        // ═══════════════════════════════════════════════════════════════
        //  SUPPRESS EYE GRAPHIC — In 1.6 with render nodes, eyes are
        //  their own node. If we suppress the head node, eyes go too.
        // ═══════════════════════════════════════════════════════════════

        public static bool TryPatchSuppressEyes(Harmony harmony)
        {
            try
            {
                var targetMethod = typeof(PawnRenderer)
                    .GetNestedTypes(AccessTools.all)
                    .SelectMany(nestedType => AccessTools.GetDeclaredMethods(nestedType))
                    .FirstOrDefault(mi => mi.Name.Contains("DrawExtraEyeGraphic"));

                if (targetMethod == null)
                {
                    Log.Message(LogPrefix + "DrawExtraEyeGraphic not found (expected in 1.6) — " +
                                "eye suppression handled by render tree node visibility.");
                    return false;
                }

                harmony.Patch(targetMethod,
                    prefix: new HarmonyMethod(typeof(Patches_Render), nameof(SuppressEyes_Prefix)));
                Log.Message(LogPrefix + "Patched DrawExtraEyeGraphic eye suppression.");
                return true;
            }
            catch (Exception e)
            {
                Log.Message(LogPrefix + $"DrawExtraEyeGraphic patch skipped: {e.Message}");
                return false;
            }
        }

        public static bool SuppressEyes_Prefix(PawnRenderer __instance)
        {
            try
            {
                var outerRenderer = Traverse.Create(__instance).Field("<>4__this").GetValue<PawnRenderer>();
                Pawn pawn = GetPawn(outerRenderer ?? __instance);
                if (pawn != null && pawn.IsHeyra())
                    return false;
            }
            catch { }
            return true;
        }
    }
}
