using System;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using UnityEngine;
using GadgetCore.API;

namespace ControllerSupport
{
    // Attack-owned state. Acquisition only occurs at a validated native attack
    // start/aimed skill activation; delayed effects retain the originating token.
    internal static class ControllerAimAssist
    {
        internal sealed class Attack
        {
            internal Collider Target;
            internal bool Lost;
            internal int TraceId;
            internal bool ResolveTraced, LossTraced;
        }
        private sealed class Entry { internal WeakReference Owner; internal Attack Attack; }
        private static readonly List<Entry> tokens = new List<Entry>();
        private static readonly Type[] Segmented = {
            typeof(EnemyScript).Assembly.GetType("WormScript"), typeof(EnemyScript).Assembly.GetType("SliverScript"),
            typeof(EnemyScript).Assembly.GetType("ScarabScript"), typeof(EnemyScript).Assembly.GetType("Millipede"),
            typeof(EnemyScript).Assembly.GetType("DestroyerTrue") };
        private static readonly Type HiveBox = typeof(EnemyScript).Assembly.GetType("HivemindBox");
        private static readonly Type Hive = typeof(EnemyScript).Assembly.GetType("Hivemind");
        private static Attack current;
        private static GameObject indicator;
        private static Attack appliedVisual;
        private static float appliedUntil;
        internal const float AppliedHighlightSeconds = .7f;
        // Tuning: world-distance penalty per unit of actual hitbox/centerline gap.
        // Zero restores distance-only ranking; higher values favor the aim axis.
        internal static float CenterlineDistanceMultiplier = 2f;
        private static void ClearAppliedVisual() { appliedVisual=null; appliedUntil=0; }
        internal static bool Enabled { get; private set; }
        internal static int SearchCount { get; private set; }
        // Temporary, attack-bounded evidence for the reported passive-mob miss.
        internal static Action<string> Trace;
        private static int nextTraceId;
        private static void Log(string message)
        { if(Trace!=null) { try { Trace("[DEBUG-aim-miss] "+message); } catch { } } }
        private static string Point(Vector3 point)
        { return string.Format(CultureInfo.InvariantCulture,"({0:0.###},{1:0.###},{2:0.###})",point.x,point.y,point.z); }
        private static string Target(Collider collider)
        { return collider ? collider.gameObject.name+"#"+collider.GetInstanceID()+" "+collider.GetType().Name+" layer="+collider.gameObject.layer+" center="+Point(collider.bounds.center)+" size="+Point(collider.bounds.size) : "none"; }
        private static void Lost(Attack attack,string reason)
        {
            attack.Lost=true;
            if(ReferenceEquals(appliedVisual,attack)) ClearAppliedVisual();
            if(!attack.LossTraced) { attack.LossTraced=true; Log("attack="+attack.TraceId+" lost="+reason); }
        }
        internal static void Configure() { ControllerAim.AttackResolver = Resolve; ControllerAim.AttackOwnershipLost = LoseOwnership; }
        private static void LoseOwnership() { if(current!=null) Lost(current,"controller ownership"); }
        internal static void Toggle()
        {
            Configure(); Enabled=!Enabled;
            if(!Enabled) ClearAppliedVisual();
            Log("toggle enabled="+Enabled);
            if(!Enabled) Invalidate();
            if(Enabled && !indicator)
            {
                indicator=new GameObject("ControllerSupport.AimAssistIndicator");
                UnityEngine.Object.DontDestroyOnLoad(indicator);
                indicator.AddComponent<AimAssistIndicator>();
            }
        }
        internal static void Reset()
        {
            Enabled=false; current=null; tokens.Clear(); SearchCount=0;
            ClearAppliedVisual();
            Trace=null; nextTraceId=0;
            ControllerAim.AttackResolver=null; ControllerAim.AttackOwnershipLost=null;
            if(indicator) UnityEngine.Object.Destroy(indicator); indicator=null;
        }
        private static void Invalidate()
        { foreach(Entry token in tokens) if(token.Attack!=null) token.Attack.Lost=true; if(current!=null) current.Lost=true; }
        internal static Attack Current { get { return current; } }
        internal static Attack Push(Attack next) { Attack old=current; current=next; return old; }
        internal static void Restore(Attack old) { current=old; }
        internal static bool Local(PlayerScript player)
        {
            if(!player || !MenuScript.player || player.gameObject!=MenuScript.player) return false;
            NetworkView view=player.GetComponent<NetworkView>(); return view && view.isMine;
        }
        internal static void Retain(object owner,Attack attack)
        {
            if(owner==null) return;
            for(int i=tokens.Count-1;i>=0;i--)
            {
                object found=tokens[i].Owner.Target;
                if(found==null) tokens.RemoveAt(i);
                else if(ReferenceEquals(found,owner)) { tokens[i].Attack=attack; return; }
            }
            tokens.Add(new Entry { Owner=new WeakReference(owner),Attack=attack });
        }
        internal static Attack Retained(object owner)
        {
            if(owner==null) return null;
            for(int i=tokens.Count-1;i>=0;i--)
            {
                object found=tokens[i].Owner.Target;
                if(found==null) tokens.RemoveAt(i);
                else if(ReferenceEquals(found,owner)) return tokens[i].Attack;
            }
            return null;
        }
        internal static PlayerScript IteratorPlayer(object iterator)
        {
            if(iterator==null) return null;
            foreach(FieldInfo field in iterator.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                if(field.FieldType==typeof(PlayerScript)) return field.GetValue(iterator) as PlayerScript;
            return null;
        }
        internal static Projectile IteratorProjectile(object iterator)
        {
            if(iterator==null) return null;
            foreach(FieldInfo field in iterator.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                if(field.FieldType==typeof(Projectile)) return field.GetValue(iterator) as Projectile;
            return null;
        }
        internal static void BeginAttack(object iterator)
        {
            // Injected immediately after native attacking=true, beyond its guards.
            PlayerScript player=IteratorPlayer(iterator);
            bool local=Local(player);
            Log("BeginAttack iterator="+(iterator==null?"null":iterator.GetType().FullName)+" player="+(player?player.gameObject.name:"none")+" local="+local+" enabled="+Enabled);
            Attack attack=local ? Acquire() : null;
            Retain(iterator,attack); current=attack;
        }
        internal static Attack Acquire()
        {
            var attack=new Attack { TraceId=++nextTraceId }; Vector3 aim;
            if(!Enabled) { Log("attack="+attack.TraceId+" Acquire exit=disabled"); return attack; }
            if(!SafeGameplay())
            {
                if(Trace!=null)
                {
                    NetworkView view=MenuScript.player ? MenuScript.player.GetComponent<NetworkView>() : null;
                    Log("attack="+attack.TraceId+" Acquire exit=unsafe gameplay player="+(bool)MenuScript.player+" camera="+(bool)Camera.main+" localOwned="+(view && view.isMine)+" combat="+GameScript.combatMode+" dead="+GameScript.dead+" pause="+GameScript.pausing+" inventory="+GameScript.inventoryOpen+" build="+GameScript.buildMode+" talking="+GameScript.talking+" beaming="+PlayerScript.beaming+" frozen="+GadgetCoreAPI.IsInputFrozen());
                }
                return attack;
            }
            if(!ControllerAim.TryWorldPoint(out aim)) { Log("attack="+attack.TraceId+" Acquire exit=no controller aim ownership"); return attack; }
            if(!Camera.main) { Log("attack="+attack.TraceId+" Acquire exit=no camera"); return attack; }
            SearchCount++;
            Vector2 origin=PlayerCenter();
            Vector2 axis=(Vector2)aim-origin;
            Vector2 min,max; Viewport(out min,out max);
            Vector2 midpoint=(min+max)*.5f;
            float radius=(max-min).magnitude*.5f;
            Collider[] candidates=Physics.OverlapSphere(new Vector3(midpoint.x,midpoint.y,MenuScript.player.transform.position.z),
                radius, (1<<9)|(1<<28));
            if(Trace!=null) Log("attack="+attack.TraceId+" Acquire enabled=true safe=true controller=true origin="+Point(origin)+" visual="+Point(aim)+" candidates="+candidates.Length);
            float bestScore=float.PositiveInfinity,bestGap=float.PositiveInfinity,bestDistance=float.PositiveInfinity;
            int traced=0;
            foreach(Collider candidate in candidates)
            {
                string verdict;
                bool eligible=Eligible(candidate,origin,axis,min,max,out verdict);
                float distance=eligible?((Vector2)candidate.bounds.center-origin).magnitude:float.PositiveInfinity;
                float gap=eligible?CenterlineGap(candidate,origin,axis):float.PositiveInfinity;
                float score=distance+CenterlineDistanceMultiplier*gap;
                if(Trace!=null && traced++<8) Log("attack="+attack.TraceId+" candidate="+Target(candidate)+" verdict="+verdict+" playerDistance="+distance.ToString("0.###",CultureInfo.InvariantCulture)+" centerlineGap="+gap.ToString("0.###",CultureInfo.InvariantCulture)+" score="+score.ToString("0.###",CultureInfo.InvariantCulture));
                if(!eligible) continue;
                if(score<bestScore || (score==bestScore && (gap<bestGap || (gap==bestGap && (distance<bestDistance || (distance==bestDistance && attack.Target && candidate.GetInstanceID()<attack.Target.GetInstanceID()))))))
                { bestScore=score; bestGap=gap; bestDistance=distance; attack.Target=candidate; }
            }
            if(Trace!=null) Log("attack="+attack.TraceId+" chosen="+Target(attack.Target));
            return attack;
        }
        internal static Vector3 PlayerCenter()
        {
            Collider body=MenuScript.player ? MenuScript.player.GetComponent<Collider>() : null;
            return body && body.enabled ? body.bounds.center : MenuScript.player.transform.position;
        }
        private static bool SafeGameplay()
        {
            if(!MenuScript.player || !Camera.main || !GameScript.combatMode || GameScript.dead ||
                GameScript.pausing || GameScript.buildMode || GameScript.inventoryOpen || GameScript.talking ||
                PlayerScript.beaming || GadgetCoreAPI.IsInputFrozen()) return false;
            NetworkView view=MenuScript.player.GetComponent<NetworkView>(); return view && view.isMine;
        }
        private static void Viewport(out Vector2 min,out Vector2 max)
        {
            Camera camera=Camera.main;
            float depth=camera.WorldToScreenPoint(MenuScript.player.transform.position).z;
            Vector3 a=camera.ViewportToWorldPoint(new Vector3(0,0,depth));
            Vector3 b=camera.ViewportToWorldPoint(new Vector3(1,1,depth));
            min=new Vector2(Math.Min(a.x,b.x),Math.Min(a.y,b.y));
            max=new Vector2(Math.Max(a.x,b.x),Math.Max(a.y,b.y));
        }
        private static bool Eligible(Collider collider,Vector2 origin,Vector2 axis,Vector2 min,Vector2 max)
        { string verdict; return Eligible(collider,origin,axis,min,max,out verdict); }
        private static bool Eligible(Collider collider,Vector2 origin,Vector2 axis,Vector2 min,Vector2 max,out string verdict)
        {
            verdict="inactive/disabled/wrong layer";
            if(!collider || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                (collider.gameObject.layer!=9 && collider.gameObject.layer!=28)) return false;
            verdict="no living supported receiver";
            if(!LivingReceiver(collider)) return false;
            bool intersects;
            SphereCollider sphere=collider as SphereCollider;
            if(sphere)
            {
                float radius=SphereRadius(sphere);
                intersects=AimAssistGeometry.IntersectsCircle(origin,axis,sphere.transform.TransformPoint(sphere.center),radius,min,max);
                verdict=intersects?"eligible":"outside cone/viewport";
                return intersects;
            }
            BoxCollider box=collider as BoxCollider;
            verdict="unsupported collider shape";
            if(!box) return false; // Unknown hitbox shape cannot be approximated by a false-positive AABB.
            intersects=AimAssistGeometry.Intersects(origin,axis,BoxVertices(box),min,max);
            verdict=intersects?"eligible":"outside cone/viewport";
            return intersects;
        }
        private static float SphereRadius(SphereCollider sphere)
        {
            Vector3 scale=sphere.transform.lossyScale;
            return sphere.radius*Math.Max(Math.Abs(scale.x),Math.Max(Math.Abs(scale.y),Math.Abs(scale.z)));
        }
        private static Vector2[] BoxVertices(BoxCollider box)
        {
            Vector3[] corners=BoxCorners(box); Vector2[] vertices=new Vector2[corners.Length];
            for(int i=0;i<corners.Length;i++) vertices[i]=corners[i];
            return vertices;
        }
        private static Vector3[] BoxCorners(BoxCollider box)
        {
            Vector3[] vertices=new Vector3[8]; int index=0;
            for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
                vertices[index++]=box.transform.TransformPoint(box.center+Vector3.Scale(box.size,new Vector3(x,y,z))*.5f);
            return vertices;
        }
        private static float CenterlineGap(Collider collider,Vector2 origin,Vector2 axis)
        {
            SphereCollider sphere=collider as SphereCollider;
            if(sphere) return AimAssistGeometry.CenterlineDistanceCircle(origin,axis,sphere.transform.TransformPoint(sphere.center),SphereRadius(sphere));
            BoxCollider box=collider as BoxCollider;
            return box ? AimAssistGeometry.CenterlineDistance(origin,axis,BoxVertices(box)) : float.PositiveInfinity;
        }
        // Native projectile damage is sent to layer-9/28 receivers. These boss
        // parts forward TD to a live main head; their own HP is not authoritative.
        private static bool LivingReceiver(Collider collider)
        {
            EnemyScript enemy=collider.GetComponentInParent<EnemyScript>();
            if(enemy) return enemy.hp>0 && !enemy.dead && !enemy.dying;
            for(Transform transform=collider.transform; transform!=null; transform=transform.parent)
            {
                foreach(Type type in Segmented)
                {
                    if(type==null) continue;
                    Component part=transform.gameObject.GetComponent(type);
                    if(!part) continue;
                    FieldInfo isHead=type.GetField("isMainHead");
                    if(isHead==null) return false;
                    if((bool)isHead.GetValue(part)) return Alive(part);
                    FieldInfo main=type.GetField("mainHead");
                    GameObject head=main==null?null:main.GetValue(part) as GameObject;
                    Component receiver=head ? head.GetComponent(type) : null;
                    return receiver && (bool)isHead.GetValue(receiver) && Alive(receiver);
                }
                Component hive=Hive==null?null:transform.gameObject.GetComponent(Hive);
                if(hive) return Alive(hive);
                Component box=HiveBox==null?null:transform.gameObject.GetComponent(HiveBox);
                if(box)
                {
                    FieldInfo forward=HiveBox.GetField("p");
                    GameObject head=forward==null?null:forward.GetValue(box) as GameObject;
                    return head && Hive!=null && Alive(head.GetComponent(Hive));
                }
            }
            return false;
        }
        private static bool Alive(Component receiver)
        {
            if(!receiver || !receiver.gameObject.activeInHierarchy) return false;
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            Type type=receiver.GetType();
            FieldInfo hp=type.GetField("hp",flags),dying=type.GetField("dying",flags),dead=type.GetField("dead",flags);
            return hp!=null && hp.GetValue(receiver) is int && (int)hp.GetValue(receiver)>0 &&
                (dying==null || !(bool)dying.GetValue(receiver)) && (dead==null || !(bool)dead.GetValue(receiver));
        }
        internal static Vector3 Resolve(Vector3 normal)
        {
            Attack attack=current;
            if(!Enabled || attack==null || attack.Lost) return normal;
            if(!SafeGameplay()) { Lost(attack,"unsafe gameplay"); return normal; }
            if(!attack.Target) { if(!attack.ResolveTraced) { attack.ResolveTraced=true; Log("attack="+attack.TraceId+" Resolve fallback=no target normal="+Point(normal)); } return normal; }
            Vector3 visual;
            if(!ControllerAim.TryWorldPoint(out visual)) { Lost(attack,"controller ownership"); return normal; }
            Vector2 origin=PlayerCenter(), axis=(Vector2)visual-origin,min,max;
            Viewport(out min,out max);
            string verdict;
            if(!Eligible(attack.Target,origin,axis,min,max,out verdict)) { Lost(attack,verdict); return normal; }
            Vector2 point=AimAssistGeometry.ClampAim(origin,axis,attack.Target.bounds.center);
            appliedVisual=attack; appliedUntil=Time.unscaledTime+AppliedHighlightSeconds;
            if(!attack.ResolveTraced) { attack.ResolveTraced=true; if(Trace!=null) Log("attack="+attack.TraceId+" Resolve applied target="+Target(attack.Target)+" assisted="+Point(point)+" normal="+Point(normal)+" origin="+Point(origin)+" visual="+Point(visual)); }
            return new Vector3(point.x,point.y,normal.z);
        }
        internal static float CursorX() { Vector3 point; return ControllerAim.TryWorldPoint(out point)?Resolve(point).x:ControllerCursor.staticX; }
        internal static float CursorY() { Vector3 point; return ControllerAim.TryWorldPoint(out point)?Resolve(point).y:ControllerCursor.staticY; }
        // Debug presentation uses the same origin, viewport and half-angle as
        // acquisition; it never searches for or acquires an enemy.
        private static Vector2 ScreenPoint(Vector3 world)
        { Vector3 point=Camera.main.WorldToScreenPoint(world); return new Vector2(point.x,Screen.height-point.y); }
        private static void Line(Vector2 a,Vector2 b,float width)
        {
            Vector2 delta=b-a;
            if(delta.sqrMagnitude<.000001f) return;
            GUI.matrix=Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,(float)(Math.Atan2(delta.y,delta.x)*180/Math.PI)),new Vector3(1,1,1));
            GUI.DrawTexture(new Rect(0,-width*.5f,delta.magnitude,width),Texture2D.whiteTexture);
        }
        private static bool ClipAxis(float origin,float direction,float min,float max,ref float enter,ref float exit)
        {
            if(Math.Abs(direction)<.000001f) return origin>=min && origin<=max;
            float a=(min-origin)/direction,b=(max-origin)/direction;
            enter=Math.Max(enter,Math.Min(a,b)); exit=Math.Min(exit,Math.Max(a,b)); return enter<=exit;
        }
        private static void ConeRay(Vector2 origin,Vector2 axis,double angle,Vector2 min,Vector2 max,float depth)
        {
            if(axis.sqrMagnitude<.000001f) return;
            float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
            Vector2 ray=new Vector2(axis.x*c-axis.y*s,axis.x*s+axis.y*c);
            float enter=0,exit=float.PositiveInfinity;
            if(!ClipAxis(origin.x,ray.x,min.x,max.x,ref enter,ref exit) || !ClipAxis(origin.y,ray.y,min.y,max.y,ref enter,ref exit)) return;
            Vector2 a=origin+ray*enter,b=origin+ray*exit;
            Line(ScreenPoint(new Vector3(a.x,a.y,depth)),ScreenPoint(new Vector3(b.x,b.y,depth)),1.5f);
        }
        private static void Outline(Collider collider)
        {
            SphereCollider sphere=collider as SphereCollider;
            if(sphere)
            {
                Vector3 center=sphere.transform.TransformPoint(sphere.center);
                float radius=SphereRadius(sphere);
                Vector2 previous=ScreenPoint(center+new Vector3(radius,0,0));
                for(int i=1;i<=32;i++)
                {
                    double angle=i*Math.PI*2/32;
                    Vector2 next=ScreenPoint(center+new Vector3(radius*(float)Math.Cos(angle),radius*(float)Math.Sin(angle),0));
                    Line(previous,next,2); previous=next;
                }
                return;
            }
            BoxCollider box=collider as BoxCollider;
            if(!box) return;
            // Projected bounds of all eight transformed corners, not sprite bounds.
            Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            foreach(Vector3 corner in BoxCorners(box))
            {
                Vector2 p=ScreenPoint(corner);
                min=new Vector2(Math.Min(min.x,p.x),Math.Min(min.y,p.y)); max=new Vector2(Math.Max(max.x,p.x),Math.Max(max.y,p.y));
            }
            Line(min,new Vector2(max.x,min.y),2);Line(new Vector2(max.x,min.y),max,2);
            Line(max,new Vector2(min.x,max.y),2);Line(new Vector2(min.x,max.y),min,2);
        }
        private sealed class AimAssistIndicator : MonoBehaviour
        {
            private void OnGUI()
            {
                // The marker represents the enabled setting, independently of
                // whether the stick currently owns aim or an attack has a target.
                if(!Enabled || !SafeGameplay() || !Camera.main) { ClearAppliedVisual(); return; }
                Vector3 point=Camera.main.WorldToScreenPoint(PlayerCenter());
                if(point.z<=0) return;
                Color old=GUI.color; Matrix4x4 matrix=GUI.matrix;
                try
                {
                    GUI.matrix=Matrix4x4.identity; GUI.color=Color.red;
                    GUI.DrawTexture(new Rect(point.x-3,Screen.height-point.y-3,6,6),Texture2D.whiteTexture);
                    Vector3 visual;
                    if(!ControllerAim.TryWorldPoint(out visual)) { ClearAppliedVisual(); return; }
                    Vector2 origin=PlayerCenter(),axis=(Vector2)visual-origin,min,max;
                    Viewport(out min,out max);
                    double angle=AimAssistGeometry.HalfAngleDegrees*Math.PI/180;
                    GUI.color=new Color(1,0,0,.7f);
                    ConeRay(origin,axis,-angle,min,max,visual.z);ConeRay(origin,axis,angle,min,max,visual.z);
                    GUI.color=new Color(1,0,0,.2f);ConeRay(origin,axis,0,min,max,visual.z);
                    if(appliedVisual==null) return;
                    if(Time.unscaledTime>appliedUntil || appliedVisual.Lost || !Eligible(appliedVisual.Target,origin,axis,min,max)) { ClearAppliedVisual(); return; }
                    GUI.color=Color.yellow;Outline(appliedVisual.Target);
                }
                finally { GUI.color=old; GUI.matrix=matrix; }
            }
        }
    }
}
