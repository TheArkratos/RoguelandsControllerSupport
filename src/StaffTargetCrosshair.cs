using System;
using System.Collections.Generic;
using UnityEngine;

namespace ControllerSupport
{
    // A copy of the native cursor glyph, not its position-updating root or guides.
    // Points are published by native staff ticks; rendering never selects targets.
    internal static class StaffTargetCrosshair
    {
        internal const float StaleSeconds = .7f;
        private sealed class Marker
        {
            internal Projectile Owner;
            internal GameObject Player, Glyph;
            internal Material Material;
            internal MeshRenderer Template;
            internal float Until;
        }
        private static readonly Dictionary<int,Marker> markers = new Dictionary<int,Marker>();
        private static MeshRenderer template;
        private static ActionCursor cursor;
        private static bool warned;

        private static bool FindTemplate()
        {
            if(template && cursor && template.sharedMaterial && template.sharedMaterial.mainTexture) return true;
            template=null; cursor=null;
            foreach(ActionCursor candidate in UnityEngine.Object.FindObjectsOfType<ActionCursor>())
            {
                if(candidate.gameObject.name!="actionCursor") continue;
                foreach(MeshRenderer renderer in candidate.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material material=renderer.sharedMaterial;
                    if(!material || !material.mainTexture || material.mainTexture.name!="actionCursor") continue;
                    template=renderer; cursor=candidate; return true;
                }
            }
            return false;
        }
        private static Material RedMaterial(Texture texture)
        {
            foreach(string name in new[]{"Sprites/Default","Particles/Alpha Blended","Legacy Shaders/Transparent/Diffuse"})
            {
                Shader shader=Shader.Find(name);
                if(!shader) continue;
                Material material=new Material(shader);
                if(material.HasProperty("_Color")) material.SetColor("_Color",Color.red);
                // Particle shader doubles vertex tint. Half tint preserves native
                // gray-core brightness and alpha while changing only its color.
                else if(material.HasProperty("_TintColor")) material.SetColor("_TintColor",new Color(.5f,0,0,.5f));
                else { UnityEngine.Object.Destroy(material); continue; }
                material.mainTexture=texture; return material;
            }
            if(!warned) { warned=true; Debug.LogWarning("Controller Support: no tintable cursor shader; staff target crosshair unavailable."); }
            return null;
        }
        internal static void Set(Projectile owner,Vector3 point)
        {
            if(!owner || !FindTemplate()) return;
            int id=owner.GetInstanceID(); Marker marker;
            if(markers.TryGetValue(id,out marker) && (marker.Template!=template || !marker.Glyph || !marker.Material))
            { Dispose(marker); markers.Remove(id); }
            if(!markers.TryGetValue(id,out marker))
            {
                Material material=RedMaterial(template.sharedMaterial.mainTexture);
                if(!material) return;
                GameObject glyph=UnityEngine.Object.Instantiate(template.gameObject) as GameObject;
                if(!glyph) { UnityEngine.Object.Destroy(material); return; }
                glyph.name="ControllerSupport.StaffTargetCrosshair";
                glyph.transform.parent=null;
                glyph.transform.rotation=template.transform.rotation;
                glyph.transform.localScale=template.transform.lossyScale;
                MeshRenderer renderer=glyph.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=material; renderer.enabled=true;
                marker=new Marker { Owner=owner,Player=MenuScript.player,Glyph=glyph,Material=material,Template=template };
                markers.Add(id,marker);
            }
            // Keep the native glyph's render depth/offset, with the exact world
            // steering point in place of the original direction cursor's origin.
            marker.Glyph.transform.position=point+(template.transform.position-cursor.transform.position);
            marker.Until=Time.unscaledTime+StaleSeconds;
            marker.Glyph.SetActive(true);
        }
        internal static void Refresh(bool visible)
        {
            var expired=new List<int>();
            foreach(var pair in markers)
            {
                Marker marker=pair.Value;
                if(!marker.Owner || !marker.Owner.gameObject.activeInHierarchy || !marker.Glyph ||
                    marker.Player!=MenuScript.player || Time.unscaledTime>marker.Until || !template || !cursor || !marker.Template || marker.Template!=template)
                { Dispose(marker); expired.Add(pair.Key); continue; }
                marker.Glyph.SetActive(visible && ControllerAimAssist.StaffDisplayOwned(marker.Owner));
            }
            foreach(int id in expired) markers.Remove(id);
        }
        internal static void Hide(Projectile owner)
        {
            Marker marker;
            if(owner && markers.TryGetValue(owner.GetInstanceID(),out marker) && marker.Glyph) marker.Glyph.SetActive(false);
        }
        private static void Dispose(Marker marker)
        {
            if(marker.Glyph) { marker.Glyph.SetActive(false); UnityEngine.Object.Destroy(marker.Glyph); }
            if(marker.Material) UnityEngine.Object.Destroy(marker.Material);
        }
        internal static void Clear()
        {
            foreach(Marker marker in markers.Values) Dispose(marker);
            markers.Clear(); template=null; cursor=null; warned=false;
        }
    }
}
