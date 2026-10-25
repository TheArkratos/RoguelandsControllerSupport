using System;
using UnityEngine;

namespace ControllerSupport
{
    // Reuses the native crosshair's guide-plane mesh, material and measured
    // thickness. Rendering never changes the white cursor or weapon aim.
    internal static class ControllerCombatGuide
    {
        private static ActionCursor cursor;
        private static MeshRenderer[] bars;
        private static bool[] previousEnabled;
        private static bool hidden;
        private static GameObject guide;
        private static MeshRenderer guideRenderer;
        private static Mesh coneMesh;

        internal static void Observe(ActionCursor candidate)
        {
            if (!ControllerInputOwnership.UsingController) Restore();
            if (!candidate || candidate.gameObject.name != "actionCursor") return;
            if (cursor != candidate)
            {
                Reset();
                cursor = candidate;
            }
        }

        // Runs after the native cursor/camera updates so movement does not leave
        // the guide attached to a previous frame's position or viewport edge.
        internal static void UpdateVisual()
        {
            try
            {
                Vector3 unused;
                Camera camera = Camera.main;
                GameObject player = MenuScript.player;
                if (!ControllerInputOwnership.UsingController || !GameScript.combatMode ||
                    GameScript.dead || GameScript.pausing || GameScript.buildMode ||
                    GameScript.inventoryOpen || !camera || !player ||
                    !cursor || !cursor.gameObject.activeInHierarchy ||
                    !ControllerAim.TryWorldPoint(out unused))
                { Restore(); return; }
                NetworkView owner = player.GetComponent<NetworkView>();
                if (!owner || !owner.isMine || !FindBars()) { Restore(); return; }
                MeshFilter source = bars[0].GetComponent<MeshFilter>();
                Mesh mesh = source ? source.sharedMesh : null;
                if (!mesh || !bars[0].sharedMaterial ||
                    (!hidden && !bars[0].enabled && !bars[1].enabled))
                { Restore(); return; }

                Vector3 nativeSize = bars[0].bounds.size;
                float width = Math.Min(Math.Abs(nativeSize.x), Math.Abs(nativeSize.y));
                Vector3 meshSize = mesh.bounds.size;
                if (!Finite(width) || width <= 0f || meshSize.x <= 0f || meshSize.y <= 0f)
                { Restore(); return; }
                float z = bars[0].transform.position.z;
                bool staff = GameScript.equippedIDs != null && GameScript.equippedIDs.Length > 0 &&
                    GameScript.equippedIDs[0] >= 550 && GameScript.equippedIDs[0] < 600;
                Vector3 center = staff ? ControllerAimAssist.PlayerCenter() : player.transform.position;
                Vector3 start = new Vector3(center.x, center.y, z);
                Vector3 aim = new Vector3(cursor.transform.position.x, cursor.transform.position.y, z);
                Vector3 screenStart = camera.WorldToScreenPoint(start);
                Vector3 screenAim = camera.WorldToScreenPoint(aim);
                Vector2 screenEnd;
                if (screenStart.z <= 0f || screenAim.z <= 0f ||
                    !TryScreenEndpoint(screenStart, screenAim, camera.pixelRect, out screenEnd))
                { Restore(); return; }
                Vector3 end = camera.ScreenToWorldPoint(new Vector3(screenEnd.x, screenEnd.y, screenStart.z));
                // Native guide planes and camera face the XY gameplay plane.
                // Decline unsupported projections rather than draw off-plane art.
                if (!Finite(end.x) || !Finite(end.y) || !Finite(end.z) || Math.Abs(end.z - z) > .01f)
                { Restore(); return; }
                Vector3 axis = end - start;
                float length = (float)Math.Sqrt(axis.x * axis.x + axis.y * axis.y);
                if (!Finite(length) || length <= 0f) { Restore(); return; }
                if (!guide)
                {
                    guide = new GameObject("Controller Combat Aim Guide");
                    guide.layer = bars[0].gameObject.layer;
                    guide.AddComponent<MeshFilter>().sharedMesh = mesh;
                    guideRenderer = guide.AddComponent<MeshRenderer>();
                    guideRenderer.sharedMaterial = bars[0].sharedMaterial;
                    guideRenderer.sortingLayerID = bars[0].sortingLayerID;
                    guideRenderer.sortingOrder = bars[0].sortingOrder;
                }
                if (!guideRenderer) { Restore(); return; }
                MeshFilter guideFilter = guide.GetComponent<MeshFilter>();
                if (staff)
                {
                    if (!UpdateCone(camera, start, aim, screenStart.z, mesh)) { Restore(); return; }
                    guideFilter.sharedMesh = coneMesh;
                    guide.transform.position = start;
                    guide.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                    guide.transform.localScale = new Vector3(1f, 1f, 1f);
                }
                else
                {
                    guideFilter.sharedMesh = mesh;
                    guide.transform.position = (start + end) * .5f;
                    // Native guide planes include a facing flip (180 degrees
                    // around X). Preserve it: a Z-only rotation exposes the
                    // back face to the native CullBack transparent shader.
                    Vector3 nativeAxis = bars[0].transform.TransformDirection(new Vector3(1f, 0f, 0f));
                    float angle = (float)((Math.Atan2(axis.y, axis.x) -
                        Math.Atan2(nativeAxis.y, nativeAxis.x)) * 180d / Math.PI);
                    guide.transform.rotation = Quaternion.Euler(0f, 0f, angle) * bars[0].transform.rotation;
                    guide.transform.localScale = new Vector3(length / meshSize.x, width / meshSize.y, 1f);
                }
                guideRenderer.enabled = true;
                if (!hidden)
                {
                    previousEnabled = new[] { bars[0].enabled, bars[1].enabled };
                    hidden = true;
                }
                bars[0].enabled = bars[1].enabled = false;
            }
            catch (Exception)
            {
                // Presentation failures must leave native guides available and
                // discard partial allocations so the next native observation can retry.
                Reset();
            }
        }

        private static bool UpdateCone(Camera camera, Vector3 start, Vector3 aim, float depth, Mesh nativeMesh)
        {
            Rect view = camera.pixelRect;
            var corners = new[] {
                camera.ScreenToWorldPoint(new Vector3(view.xMin, view.yMin, depth)),
                camera.ScreenToWorldPoint(new Vector3(view.xMax, view.yMin, depth)),
                camera.ScreenToWorldPoint(new Vector3(view.xMax, view.yMax, depth)),
                camera.ScreenToWorldPoint(new Vector3(view.xMin, view.yMax, depth))
            };
            var viewport = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 point = corners[i];
                if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z) || Math.Abs(point.z - start.z) > .01f)
                    return false;
                viewport[i] = new Vector2(point.x, point.y);
            }
            // Camera shake rolls this rectangle in world XY. Keep its actual
            // footprint so valid shake frames neither restore mouse guides nor
            // fill outside the screen by clipping against an axis-aligned box.
            Vector2[] polygon = AimAssistGeometry.VisibleConePolygon(start, aim - start, viewport);
            if (polygon.Length < 3) return false;
            double area = 0d;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                area += (double)a.x * b.y - (double)a.y * b.x;
            }
            if (Math.Abs(area) < 1e-8) return false;
            // Match the native plane's front face. Do not add reversed duplicate
            // triangles: its shader may be two-sided and would double the opacity.
            Vector3[] nativeVertices = nativeMesh.vertices;
            int[] nativeTriangles = nativeMesh.triangles;
            double nativeArea = 0d;
            if (nativeVertices == null || nativeTriangles == null) return false;
            for (int i = 0; i + 2 < nativeTriangles.Length && nativeArea == 0d; i += 3)
            {
                // Cone vertices already lie in world XY under an identity
                // transform, so match the native plane's WORLD winding. Its
                // local winding is reversed by the native facing flip.
                Vector3 a = bars[0].transform.TransformPoint(nativeVertices[nativeTriangles[i]]),
                    b = bars[0].transform.TransformPoint(nativeVertices[nativeTriangles[i + 1]]),
                    c = bars[0].transform.TransformPoint(nativeVertices[nativeTriangles[i + 2]]);
                nativeArea = (double)(b.x - a.x) * (c.y - a.y) - (double)(b.y - a.y) * (c.x - a.x);
            }
            if (nativeArea == 0d) return false;
            var vertices = new Vector3[polygon.Length];
            var uv = new Vector2[polygon.Length];
            for (int i = 0; i < polygon.Length; i++)
            {
                vertices[i] = new Vector3(polygon[i].x - start.x, polygon[i].y - start.y, 0f);
                // The verified native shade texture is uniform; sampling its
                // center preserves the very same material alpha and white tint.
                uv[i] = new Vector2(.5f, .5f);
            }
            var triangles = new int[(polygon.Length - 2) * 3];
            bool reverse = Math.Sign(area) != Math.Sign(nativeArea);
            for (int i = 0; i < polygon.Length - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = reverse ? i + 2 : i + 1;
                triangles[i * 3 + 2] = reverse ? i + 1 : i + 2;
            }
            if (!coneMesh) coneMesh = new Mesh();
            coneMesh.Clear();
            coneMesh.vertices = vertices;
            coneMesh.uv = uv;
            coneMesh.triangles = triangles;
            coneMesh.RecalculateBounds();
            coneMesh.RecalculateNormals();
            return true;
        }

        private static bool FindBars()
        {
            if (bars != null && bars.Length == 2 && bars[0] && bars[1]) return true;
            MeshRenderer first = null, second = null;
            foreach (MeshRenderer renderer in cursor.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh || !renderer.sharedMaterial) continue;
                Vector3 size = renderer.bounds.size;
                float small = Math.Min(Math.Abs(size.x), Math.Abs(size.y));
                float large = Math.Max(Math.Abs(size.x), Math.Abs(size.y));
                // The two native planes are 200 by 0.2 world units; the center
                // glyph is square. Names are identical, so do not identify by name.
                if (small <= 0f || large / small < 100f) continue;
                if (!first) first = renderer;
                else if (!second) second = renderer;
                else return false;
            }
            if (!first || !second || first.sharedMaterial != second.sharedMaterial) return false;
            bars = new[] { first, second };
            return true;
        }

        internal static bool TryScreenEndpoint(Vector2 origin, Vector2 aim, Rect viewport, out Vector2 end)
        {
            end = origin;
            if (!Finite(origin.x) || !Finite(origin.y) || !Finite(aim.x) || !Finite(aim.y) ||
                !Finite(viewport.xMin) || !Finite(viewport.xMax) ||
                !Finite(viewport.yMin) || !Finite(viewport.yMax) ||
                viewport.width <= 0f || viewport.height <= 0f ||
                origin.x < viewport.xMin || origin.x > viewport.xMax ||
                origin.y < viewport.yMin || origin.y > viewport.yMax) return false;
            double dx = (double)aim.x - origin.x, dy = (double)aim.y - origin.y;
            if (dx * dx + dy * dy < 1e-12) return false;
            double distance = double.PositiveInfinity;
            if (dx > 0d) distance = Math.Min(distance, (viewport.xMax - origin.x) / dx);
            else if (dx < 0d) distance = Math.Min(distance, (viewport.xMin - origin.x) / dx);
            if (dy > 0d) distance = Math.Min(distance, (viewport.yMax - origin.y) / dy);
            else if (dy < 0d) distance = Math.Min(distance, (viewport.yMin - origin.y) / dy);
            if (double.IsInfinity(distance) || double.IsNaN(distance) || distance <= 0d) return false;
            end = new Vector2((float)(origin.x + dx * distance), (float)(origin.y + dy * distance));
            return Finite(end.x) && Finite(end.y);
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static void Restore()
        {
            if (guideRenderer) guideRenderer.enabled = false;
            if (hidden && bars != null)
                for (int i = 0; i < bars.Length; i++)
                    if (bars[i]) bars[i].enabled = previousEnabled[i];
            hidden = false;
        }
        internal static void Reset()
        {
            Restore();
            if (guide) UnityEngine.Object.Destroy(guide);
            if (coneMesh) UnityEngine.Object.Destroy(coneMesh);
            coneMesh = null;
            guide = null; guideRenderer = null; bars = null; previousEnabled = null; cursor = null;
        }
    }
}
