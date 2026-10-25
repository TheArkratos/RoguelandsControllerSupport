using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ControllerSupport;
using UnityEngine;
using HarmonyLib;
internal static class Program {
 private sealed class AttackToken {public PlayerScript Player;}
 private sealed class Helper:IEnumerator {public object Current=>null;public bool MoveNext()=>false;public void Reset(){}}
 // Native-shaped iterator keeps its player private and resumes firing after a yield.
 private sealed class DelayedGun:IEnumerator {
  private readonly PlayerScript player;private int state;public Vector3 Fired;
  public DelayedGun(PlayerScript player){this.player=player;}
  public object Current=>null;public void Reset(){throw new NotSupportedException();}
  public bool MoveNext(){if(state++==0){player.attacking=true;ControllerAimAssist.BeginAttack(this);return true;}Fired=new Vector3(ControllerAimAssist.CursorX(),ControllerAimAssist.CursorY());return false;}
 }
 private static JoystickInputState pad;private static PlayerScript local;private static int checks;
 private static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 private static void Setup(){ControllerAimAssist.Reset();PlayerScript.beaming=false;GadgetCore.API.GadgetCoreAPI.Frozen=false;Physics.Searches=0;Physics.Candidates=new Collider[0];Input.Click=false;Input.mousePosition=Vector3.zero;GameScript.combatMode=true;GameScript.dead=GameScript.inventoryOpen=GameScript.pausing=GameScript.talking=GameScript.buildMode=false;
  Resources.Assets.Clear();GameScript.equippedIDs=new[]{404};var p=new GameObject("player");local=p.AddComponent<PlayerScript>();local.shot=p.transform;p.AddComponent<NetworkView>();MenuScript.player=p;pad=new JoystickInputState();pad.Connect();pad.Apply(new RawJoystickEvent{Type=2,Number=3,Value=32767});Time.frameCount++;Time.unscaledTime+=1;ControllerAim.Configure(()=>pad);ControllerAimAssist.Configure();ControllerAimAssist.Toggle();}
 private static BoxCollider Enemy(float x,float y,float width=1,float height=1){var e=new GameObject("enemy"){layer=9};e.transform.position=new Vector3(x,y,0);e.AddComponent<EnemyScript>();var c=e.AddComponent<BoxCollider>();c.size=new Vector3(width,height,1);return c;}
 private static Vector3 Point(){return ControllerAim.AttackMousePosition();}
 private static void Next(){Time.frameCount++;Time.unscaledTime+=.5f;}
 private static MethodInfo Private(Type t,string n)=>t.GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic);
 private static Projectile SpawnStaff(float speed,Vector3 position) {
  var obj=new GameObject("staff-live");obj.transform.position=position;var projectile=obj.AddComponent<Projectile>();projectile.speed=speed;projectile.isStaff=true;
  object[] scope={local,Network.player,null};Private(typeof(AimAssistStaffSpawnPatch),"Prefix").Invoke(null,scope);
  AimAssistStaffSpawnPatch.Capture(obj);Private(typeof(AimAssistStaffSpawnPatch),"Finalizer").Invoke(null,new[]{null,scope[2]});return projectile;
 }
 private static Vector3 StaffTick(Projectile projectile) {
  object[] scope={new Projectile.UpdatePosToken{Owner=projectile},null};Private(typeof(AimAssistIteratorPatch),"Prefix").Invoke(null,scope);
  try{return ControllerAimAssist.StaffMousePosition();}finally{Private(typeof(AimAssistIteratorPatch),"Finalizer").Invoke(null,new[]{null,scope[1]});}
 }
 private static void GunPrefab(float speed) {var prefab=new GameObject("shot404");prefab.AddComponent<Projectile>().speed=speed;Resources.Assets["proj/shot404"]=prefab;}
 private static GameObject NativeGlyph(out GameObject root) {
  root=new GameObject("actionCursor");root.AddComponent<ActionCursor>();
  var glyph=new GameObject("-1");glyph.transform.parent=root.transform;glyph.transform.position=new Vector3(0,0,-.43286f);glyph.transform.rotation=Quaternion.Euler(180,0,0);
  glyph.AddComponent<MeshFilter>().sharedMesh=new UnityEngine.Object();glyph.AddComponent<Animation>();var material=new Material(new Shader{name="native-no-tint"});material.mainTexture=new Texture2D{name="actionCursor"};glyph.AddComponent<MeshRenderer>().sharedMaterial=material;
  var guides=new GameObject("-1 giant guide");guides.transform.parent=root.transform;guides.transform.lossyScale=new Vector3(100,100,100);guides.AddComponent<MeshRenderer>().sharedMaterial=new Material(new Shader()){mainTexture=new Texture2D{name="guide"}};
  return glyph;
 }
 private static GameObject[] RedGlyphs()=>UnityEngine.Object.FindObjectsOfType<GameObject>().Where(g=>g.name=="ControllerSupport.StaffTargetCrosshair").ToArray();
 private static void DebugPresentationToggle(){
  ControllerInputOwnership.Configure(()=>true);
  var indicatorType=typeof(ControllerAimAssist).GetNestedType("AimAssistIndicator",BindingFlags.NonPublic);
  var render=indicatorType.GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic);
  var instance=Activator.CreateInstance(indicatorType,true);
  bool previousDebugLines=ControllerAimAssist.ControllerCombatModeDebugLinesEnabled;
  try {
  foreach(bool staff in new[]{false,true}){
   Setup();Shader.Available.Clear();Shader.Available["Sprites/Default"]=new Shader{name="Sprites/Default",Color=true};
   GameObject nativeRoot;var nativeGlyph=NativeGlyph(out nativeRoot);
   var target=Enemy(8,.4f);Physics.Candidates=new[]{target};Projectile projectile=null;Vector3 assisted;
   if(staff){GameScript.equippedIDs[0]=550;projectile=SpawnStaff(10,Vector3.zero);assisted=StaffTick(projectile);}
   else{ControllerAimAssist.BeginAttack(new AttackToken{Player=local});assisted=Point();}
   ControllerAimAssist.ControllerCombatModeDebugLinesEnabled=true;GUI.Recorded.Clear();render.Invoke(instance,null);
   Check(GUI.Recorded.Any(d=>d.Color.r==1&&d.Color.g==0&&d.Color.a==.7f),"enabled toggle draws diagnostic cone for "+(staff?"staff":"normal weapon"));
   Check(GUI.Recorded.Any(d=>d.Color.r==1&&d.Color.g==1&&d.Color.b==0),"enabled toggle draws applied target outline for "+(staff?"staff":"normal weapon"));
   if(!staff)Check(GUI.Recorded.Any(d=>d.Color.r==0&&d.Color.g==1&&d.Color.b==1),"normal weapon enabled diagnostics include cyan prediction marker");
   int searches=Physics.Searches;ControllerAimAssist.ControllerCombatModeDebugLinesEnabled=false;GUI.Recorded.Clear();render.Invoke(instance,null);
   Check(GUI.Recorded.Count==1&&GUI.Recorded[0].Color.r==1&&GUI.Recorded[0].Color.g==0&&GUI.Recorded[0].Rect.width==6,"disabled debug lines leave only functional enabled square for "+(staff?"staff":"normal weapon"));
   Check(Physics.Searches==searches&&ControllerAimAssist.Enabled,"disabled diagnostic rendering does not scan or disable assistance");
   Vector3 after=staff?StaffTick(projectile):Point();Check(Math.Abs(after.x-assisted.x)<.00001&&Math.Abs(after.y-assisted.y)<.00001,"diagnostic toggle leaves actual assisted aim unchanged");
   if(staff)Check(RedGlyphs().Any(g=>g.activeInHierarchy),"diagnostic toggle preserves functional red staff target crosshair");
   ControllerInputOwnership.Configure(()=>false);GUI.Recorded.Clear();render.Invoke(instance,null);Check(GUI.Recorded.Count==0,"disabled diagnostics still respect shared mouse ownership");
   ControllerInputOwnership.Configure(()=>true);ControllerAim.Configure(()=>pad);Next();
   if(staff)StaffTick(projectile);ControllerAimAssist.ControllerCombatModeDebugLinesEnabled=true;GUI.Recorded.Clear();render.Invoke(instance,null);
   Check(GUI.Recorded.Count>1,"diagnostic flag can re-enable overlays for "+(staff?"staff":"normal weapon"));
   ControllerAimAssist.ControllerCombatModeDebugLinesEnabled=false;ControllerAimAssist.Configure();Check(!ControllerAimAssist.ControllerCombatModeDebugLinesEnabled,"adapter configuration does not overwrite debug flag");
   ControllerAimAssist.Reset();Check(!ControllerAimAssist.ControllerCombatModeDebugLinesEnabled,"session reset does not overwrite static debug preference");
   UnityEngine.Object.Destroy(nativeGlyph);UnityEngine.Object.Destroy(nativeRoot);ControllerAimAssist.ControllerCombatModeDebugLinesEnabled=true;
  }
  } finally {ControllerAimAssist.ControllerCombatModeDebugLinesEnabled=previousDebugLines;}
 }
 private static void Main(){
  Check(!ControllerAimAssist.ControllerCombatModeDebugLinesEnabled,"diagnostic lines default disabled");
  ControllerAimAssist.ControllerCombatModeDebugLinesEnabled=true; // Explicit opt-in for legacy diagnostic rendering checks.
  // Exercise real effect/render entry points with shared mouse ownership while
  // the raw controller remains connected and its aim stick remains held.
  Setup();bool controllerOwner=true;ControllerInputOwnership.Configure(()=>controllerOwner);
  var ownershipEnemy=Enemy(8,.4f);Physics.Candidates=new[]{ownershipEnemy};
  var ownershipAttack=new AttackToken{Player=local};ControllerAimAssist.BeginAttack(ownershipAttack);
  var ownershipStaff=SpawnStaff(10,Vector3.zero);StaffTick(ownershipStaff);
  ControllerAim.TryWorldPoint(out _);controllerOwner=false;int ownershipSearches=Physics.Searches;
  Input.mousePosition=new Vector3(45,35,0);
  Check(Point().x==45&&!Menuu.usingController,"Shared false defeats cached attack aim immediately");
  Check(StaffTick(ownershipStaff).x==45&&Physics.Searches==ownershipSearches,"Shared false stops existing staff steering and scans");
  var ownershipIndicator=typeof(ControllerAimAssist).GetNestedType("AimAssistIndicator",BindingFlags.NonPublic);
  var ownershipDraw=ownershipIndicator.GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic);
  GUI.Draws=0;ownershipDraw.Invoke(Activator.CreateInstance(ownershipIndicator,true),null);
  Check(GUI.Draws==0&&ControllerAimAssist.Enabled,"Shared false hides all assist rendering without disabling preference");
  int ownershipLoads=Resources.Loads;var unownedAttack=new AttackToken{Player=local};ControllerAimAssist.BeginAttack(unownedAttack);
  Check(ControllerAimAssist.Current==null&&ControllerAimAssist.Retained(unownedAttack)==null&&Physics.Searches==ownershipSearches,"Shared false creates no new attack assist token/search");
  Check(Resources.Loads==ownershipLoads,"Shared false does not load equipped projectile prefab");
  var unownedStaff=SpawnStaff(10,Vector3.zero);Check(ControllerAimAssist.Retained(unownedStaff)==null,"Shared false creates no new staff correlation");
  controllerOwner=true;ControllerAim.Configure(()=>pad);Next();StaffTick(ownershipStaff);
  Check(ControllerAimAssist.Retained(ownershipStaff).Target==ownershipEnemy,"Existing staff correlation resumes freshly after shared controller return");
  var ownershipOld=ControllerAimAssist.Push(ControllerAimAssist.Retained(ownershipAttack));
  Check(ControllerAimAssist.Current.Lost&&ControllerAimAssist.Current.Target==null&&Point().y==0,"Ordinary old attack target cannot resume after device handoff");ControllerAimAssist.Restore(ownershipOld);
  ControllerInputOwnership.Configure(()=>true);
  Setup();var near=Enemy(8,.4f);var far=Enemy(14,0);Physics.Candidates=new Collider[]{far,near};Vector3 visual;ControllerAim.TryWorldPoint(out visual);
  Check(Physics.Searches==0&&ControllerAimAssist.SearchCount==0,"Enabled idle/visual aim never searches");Check(Point().y==0&&Physics.Searches==0,"Unscoped mouse adapter remains normal");
  var token=new AttackToken{Player=local};var old=ControllerAimAssist.Push(null);ControllerAimAssist.BeginAttack(token);
  Check(Physics.Searches==1&&ControllerAimAssist.Retained(token).Target==near,"Actual attack chooses nearest center qualifying hitbox");
  Vector3 assisted=Point();Check(assisted.y>.1f&&assisted.x==8,"Scoped firing aims at selected enemy center");ControllerAim.TryWorldPoint(out visual);Check(visual.x==14&&visual.y==0,"Visible crosshair remains unchanged");
  var helper=new Helper();Private(typeof(AimAssistFactoryPatch),"Postfix").Invoke(null,new object[]{local,helper,typeof(PlayerScript).GetMethod("Repeater")});
  Check(ReferenceEquals(ControllerAimAssist.Retained(helper),ControllerAimAssist.Current),"Native helper factory captures original active attack token");
  var unstarted=new Helper();Private(typeof(AimAssistFactoryPatch),"Postfix").Invoke(null,new object[]{local,unstarted,typeof(PlayerScript).GetMethod("Attack")});
  Check(ControllerAimAssist.Retained(unstarted)==null&&Physics.Searches==1,"Fresh Attack factory neither acquires nor inherits an older activation");ControllerAimAssist.Restore(old);
  var second=new AttackToken{Player=local};near.gameObject.GetComponent<EnemyScript>().dead=true;ControllerAimAssist.BeginAttack(second);Check(Physics.Searches==2&&ControllerAimAssist.Current.Target==far,"Next native attack reacquires independently");
  var oldSecond=ControllerAimAssist.Push(ControllerAimAssist.Retained(helper));Check(Point().y==0&&ControllerAimAssist.Current.Lost,"Delayed original helper loses dead original without borrowing next attack");ControllerAimAssist.Restore(oldSecond);
  Check(ControllerAimAssist.Current.Target==far&&!ControllerAimAssist.Current.Lost,"Nested helper restores next attack context");
  var projectile=new GameObject("staff").AddComponent<Projectile>();ControllerAimAssist.Retain(projectile,ControllerAimAssist.Current);ControllerAimAssist.Restore(null);
  near.gameObject.GetComponent<EnemyScript>().dead=false;ControllerAimAssist.BeginAttack(new AttackToken{Player=local});
  old=ControllerAimAssist.Push(ControllerAimAssist.Retained(projectile));Check(ControllerAimAssist.Current.Target==far,"Delayed effect retains originating attack independently of newer attack");far.transform.position=new Vector3(14,5,0);Check(Point().y==0&&ControllerAimAssist.Current.Lost,"Ordinary delayed effect leaving cone falls back without reacquisition");far.transform.position=new Vector3(14,0,0);Check(Point().y==0&&ControllerAimAssist.Current.Lost&&Physics.Searches==3,"Lost ordinary delayed effect remains lost even when returning");ControllerAimAssist.Restore(old);
  old=ControllerAimAssist.Push(ControllerAimAssist.Acquire());Input.mousePosition=new Vector3(100,90,0);Next();Check(Point().x==100&&ControllerAimAssist.Current.Lost,"Physical mouse takeover returns exact native mouse and loses effect lock");ControllerAimAssist.Restore(old);
  Setup();near=Enemy(8,1.5f,1,1);Physics.Candidates=new[]{near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});Check(ControllerAimAssist.Current.Target==near,"Hitbox crossing cone qualifies even when center outside");assisted=Point();Check(Math.Abs(Math.Atan2(assisted.y,assisted.x)*180/Math.PI)<=8.0001&&Math.Abs(Math.Atan2(assisted.y,assisted.x)*180/Math.PI)>=7.9999,"Outside center clamps firing axis to eight-degree cone boundary");
  Setup();float sevenY=(float)(8*Math.Tan(7*Math.PI/180));near=Enemy(8,sevenY,.01f,.01f);Physics.Candidates=new[]{near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});
  Check(ControllerAimAssist.Current.Target==near,"Actual tiny hitbox entirely in expanded five-to-eight-degree band now qualifies");
  assisted=Point();Check(Math.Abs(Math.Atan2(assisted.y,assisted.x)*180/Math.PI-7)<.0001&&Math.Abs(assisted.y-sevenY)<.00001,"Newly eligible seven-degree target retains center aim without old five-degree clamp");
  Setup();var behind=Enemy(-3,0);var offscreen=Enemy(30,0);var dead=Enemy(5,0);dead.gameObject.GetComponent<EnemyScript>().dying=true;Physics.Candidates=new Collider[]{behind,offscreen,dead};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});Check(ControllerAimAssist.Current.Target==null,"Behind, offscreen and dying enemies are ineligible");Check(Point().y==0,"No eligible target uses normal aim");
  Setup();near=Enemy(8,0);near.gameObject.AddComponent<NetworkView>().isMine=false;Physics.Candidates=new[]{near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});Check(ControllerAimAssist.Current.Target==near,"Enemy network ownership does not exclude local aiming");
  var remote=new GameObject("remote").AddComponent<PlayerScript>();remote.gameObject.AddComponent<NetworkView>().isMine=false;int searches=Physics.Searches;ControllerAimAssist.BeginAttack(new AttackToken{Player=remote});Check(Physics.Searches==searches&&ControllerAimAssist.Current==null,"Remote player attack never acquires local assist");
  Setup();near=Enemy(8,0);Physics.Candidates=new[]{near};var chip=typeof(AimAssistSkillPatch);object[] args={12,null};Private(chip,"Prefix").Invoke(null,args);Check(Physics.Searches==1&&ControllerAimAssist.Current.Target==near,"Validated offensive chip activation acquires once");Private(chip,"Finalizer").Invoke(null,new[]{null,args[1]});Check(ControllerAimAssist.Current==null,"Offensive chip scope cleans up");args=new object[]{16,null};Private(chip,"Prefix").Invoke(null,args);Check(Physics.Searches==1&&ControllerAimAssist.Current==null,"Buff chip neither acquires nor changes targeting");Private(chip,"Finalizer").Invoke(null,new[]{null,args[1]});
  var owner=typeof(Projectile.UpdatePosToken);var move=owner.GetMethod("MoveNext");var mouse=typeof(Input).GetProperty("mousePosition").GetGetMethod();var code=new[]{new CodeInstruction(OpCodes.Call,mouse)};var replaced=AimAssistIL.Replace(code,move).ToArray();Check(Equals(replaced[0].operand,typeof(ControllerAimAssist).GetMethod("StaffMousePosition",BindingFlags.Static|BindingFlags.NonPublic)),"Verified staff continuation uses projectile-owned tracking adapter");Check(Equals(AimAssistIL.Replace(new[]{code[0],code[0]},move).First().operand,mouse),"Changed staff input IL fails closed");
  var assembly=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("NativeAttackShape"),AssemblyBuilderAccess.Run);var module=assembly.DefineDynamicModule("m");var tb=module.DefineType("<Attack>c__Iterator31");var mb=tb.DefineMethod("MoveNext",MethodAttributes.Public,typeof(bool),Type.EmptyTypes);var il=mb.GetILGenerator();il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Ret);var native=tb.CreateType().GetMethod("MoveNext");var attacking=typeof(PlayerScript).GetField("attacking");var attackCode=new[]{new CodeInstruction(OpCodes.Ldc_I4_1),new CodeInstruction(OpCodes.Stfld,attacking)};var patched=AimAssistIL.Replace(attackCode,native).ToArray();Check(patched.Length==4&&Equals(patched[3].operand,typeof(ControllerAimAssist).GetMethod("BeginAttack",BindingFlags.Static|BindingFlags.NonPublic)),"Acquisition hook injected immediately after actual native attacking assignment");Check(AimAssistIL.Replace(new[]{new CodeInstruction(OpCodes.Nop)},native).Count()==1,"Unknown native attack guard layout unchanged");
  Setup();near=Enemy(8,.4f);Physics.Candidates=new[]{near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});ControllerAimAssist.Toggle();Check(!ControllerAimAssist.Enabled&&ControllerAimAssist.Current.Lost&&Point().y==0,"Turning assist off invalidates active effects");ControllerAimAssist.Toggle();Check(ControllerAimAssist.Enabled&&Physics.Searches==1&&Point().y==0,"Re-enabling does not reacquire for existing effect");ControllerAimAssist.Reset();Check(!ControllerAimAssist.Enabled&&ControllerAimAssist.Current==null,"Session reset clears toggle and contexts");
  Setup();near=Enemy(8,.4f);Physics.Candidates=new[]{near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});
  var spawned=new GameObject("spawned");var staff=spawned.AddComponent<Projectile>();object[] spawnScope={local,Network.player,null};Private(typeof(AimAssistStaffSpawnPatch),"Prefix").Invoke(null,spawnScope);AimAssistStaffSpawnPatch.Capture(spawned);Private(typeof(AimAssistStaffSpawnPatch),"Finalizer").Invoke(null,new[]{null,spawnScope[2]});
  var original=ControllerAimAssist.Retained(staff);ControllerAimAssist.BeginAttack(new AttackToken{Player=local});
  Check(original!=null&&original.Staff==staff&&!ReferenceEquals(original,ControllerAimAssist.Current),"Correlated local staff spawn owns independent projectile state");
  var staffToken=new Projectile.UpdatePosToken{Owner=staff};object[] staffArgs={staffToken,null};Private(typeof(AimAssistIteratorPatch),"Prefix").Invoke(null,staffArgs);
  Check(ReferenceEquals(ControllerAimAssist.Current,original),"Staff continuation enters its own projectile scope");
  Private(typeof(AimAssistIteratorPatch),"Finalizer").Invoke(null,new[]{null,staffArgs[1]});
  Check(!ReferenceEquals(ControllerAimAssist.Current,original),"Staff continuation restores overlapping activation context");
  Setup();near=Enemy(8,.4f);var tied=Enemy(8,-.4f);Physics.Candidates=new[]{tied,near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});
  Check(ControllerAimAssist.Current.Target==near,"Equal-distance target tie is stable instance order, not physics enumeration");
  Setup();var body=MenuScript.player.AddComponent<BoxCollider>();body.center=new Vector3(0,2,0);MenuScript.player.Put<Collider>(body);
  near=Enemy(7,1);Physics.Candidates=new[]{near};Check(ControllerAimAssist.PlayerCenter().y==2,"Character center uses receiving body collider rather than feet pivot");
  ControllerAimAssist.BeginAttack(new AttackToken{Player=local});Check(ControllerAimAssist.Current.Target==near,"Cone origin at body center follows unchanged visual crosshair");
  foreach(string gate in new[]{"inventory","pause","dead","build","talking","beaming","frozen","remote"}) {
   Setup();near=Enemy(8,.4f);Physics.Candidates=new[]{near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});ControllerAim.TryWorldPoint(out visual);
   if(gate=="inventory")GameScript.inventoryOpen=true;if(gate=="pause")GameScript.pausing=true;if(gate=="dead")GameScript.dead=true;if(gate=="build")GameScript.buildMode=true;if(gate=="talking")GameScript.talking=true;if(gate=="beaming")PlayerScript.beaming=true;if(gate=="frozen")GadgetCore.API.GadgetCoreAPI.Frozen=true;if(gate=="remote")MenuScript.player.GetComponent<NetworkView>().isMine=false;
   Check(Point().y==0&&ControllerAimAssist.Current.Lost,"Fresh same-frame gameplay safety defeats cached visual ownership: "+gate);
   int before=Physics.Searches;ControllerAimAssist.Acquire();Check(Physics.Searches==before,"Unsafe context never performs acquisition scan: "+gate);
  }
  Setup();var head=new GameObject("worm-head").AddComponent<WormScript>();head.isMainHead=true;head.hp=100;
  var segment=new GameObject("segment"){layer=28};segment.transform.position=new Vector3(8,0,0);var part=segment.AddComponent<WormScript>();part.mainHead=head.gameObject;part.hp=0;var hitbox=segment.AddComponent<BoxCollider>();Physics.Candidates=new Collider[]{hitbox};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});
  Check(ControllerAimAssist.Current.Target==hitbox,"Segmented receiving hitbox follows main-head life, not stale part HP");head.dying=true;Check(Point().y==0&&ControllerAimAssist.Current.Lost,"Dying main head invalidates retained segment");
  Setup();var hive=new GameObject("hive").AddComponent<Hivemind>();hive.hp=100;var box=new GameObject("hive-box"){layer=9};box.transform.position=new Vector3(8,0,0);box.AddComponent<HivemindBox>().p=hive.gameObject;hitbox=box.AddComponent<BoxCollider>();Physics.Candidates=new Collider[]{hitbox};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});Check(ControllerAimAssist.Current.Target==hitbox,"Hivemind TD proxy resolves authoritative living owner");
  Setup();var dummy=new GameObject("inert-TD-like-object"){layer=9};dummy.transform.position=new Vector3(8,0,0);Physics.Candidates=new Collider[]{dummy.AddComponent<BoxCollider>()};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});Check(ControllerAimAssist.Current.Target==null,"Unverified layer-only damage-like props are excluded");
  Setup();pad=new JoystickInputState();pad.Connect();ControllerAim.Configure(()=>pad);ControllerAimAssist.Configure();Next();
  Check(!ControllerAim.TryWorldPoint(out visual),"Neutral stick has not claimed aim before enabling indicator");
  var indicatorType=typeof(ControllerAimAssist).GetNestedType("AimAssistIndicator",BindingFlags.NonPublic);
  object marker=Activator.CreateInstance(indicatorType,true);var draw=indicatorType.GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic);
  GUI.Draws=0;draw.Invoke(marker,null);Check(GUI.Draws==1&&Physics.Searches==0,"Enabled marker draws immediately without aim ownership or idle acquisition");
  Input.mousePosition=new Vector3(40,20,0);Next();draw.Invoke(marker,null);Check(GUI.Draws==2,"Enabled marker remains visible during mouse aim ownership");
  GameScript.inventoryOpen=true;draw.Invoke(marker,null);Check(GUI.Draws==2,"Inventory hides enabled marker");GameScript.inventoryOpen=false;
  GadgetCore.API.GadgetCoreAPI.Frozen=true;draw.Invoke(marker,null);Check(GUI.Draws==2,"Frozen gameplay hides enabled marker");GadgetCore.API.GadgetCoreAPI.Frozen=false;
  ControllerAimAssist.Toggle();draw.Invoke(marker,null);Check(GUI.Draws==2,"Disabled setting hides marker");
  // Shmoo prefab evidence: resources.assets layer28, unit transform,
  // BoxCollider size(3,3,3), center(.2,-.5,0), EnemyScript starting hp15.
  Setup();var logs=new List<string>();ControllerAimAssist.Trace=logs.Add;
  pad.Apply(new RawJoystickEvent{Type=2,Number=4,Value=-2340});ControllerAim.Configure(()=>pad);Next();
  var shmoo=Enemy(10,0,3,3);shmoo.gameObject.name="Shmoo";shmoo.gameObject.layer=28;shmoo.size=new Vector3(3,3,3);shmoo.center=new Vector3(.2f,-.5f,0);shmoo.gameObject.GetComponent<EnemyScript>().hp=15;Physics.Candidates=new[]{shmoo};
  var delayed=new DelayedGun(local);var iteratorPatch=typeof(AimAssistIteratorPatch);
  object[] scope={delayed,null};Private(iteratorPatch,"Prefix").Invoke(null,scope);
  Check(delayed.MoveNext(),"Private-player gun iterator yields after actual start");
  Private(iteratorPatch,"Finalizer").Invoke(null,new[]{null,scope[1]});
  Check(ControllerAimAssist.Current==null&&ControllerAimAssist.Retained(delayed).Target==shmoo,"Yield restores outer scope while retaining Shmoo acquisition");
  scope=new object[]{delayed,null};Private(iteratorPatch,"Prefix").Invoke(null,scope);
  Check(!delayed.MoveNext(),"Private-player gun resumes delayed firing step");Private(iteratorPatch,"Finalizer").Invoke(null,new[]{null,scope[1]});
  Check(Math.Abs(delayed.Fired.x-10.2f)<.0001&&Math.Abs(delayed.Fired.y+.5f)<.0001,"Shmoo asset hitbox aims exactly at center when crosshair is slightly above sprite");
  Check(ControllerAimAssist.Current==null&&Physics.Searches==1,"Resume uses retained attack without new acquisition and restores scope");
  Check(logs.Any(s=>s.Contains("candidate=Shmoo")&&s.Contains("verdict=eligible"))&&logs.Any(s=>s.Contains("Resolve applied")),"Diagnostic distinguishes eligible Shmoo and applied aim");
  Check(logs.All(s=>s.StartsWith("[DEBUG-aim-miss] "))&&logs.Count(s=>s.Contains("Resolve applied"))==1,"Temporary trace prefix is unique and cursor X/Y resolve logs once per attack");
  int logCount=logs.Count;Point();ControllerAim.TryWorldPoint(out visual);Check(logs.Count==logCount,"Idle/unscoped visual aim does not log");
  pad.Apply(new RawJoystickEvent{Type=2,Number=4,Value=-14043});ControllerAim.Configure(()=>pad);Next();ControllerAimAssist.BeginAttack(new AttackToken{Player=local});
  Check(ControllerAimAssist.Current.Target==null&&logs.Any(s=>s.Contains("candidate=Shmoo")&&s.Contains("outside cone/viewport")),"Shmoo hitbox rejects clearly outside cone at approximately (14,6) axis");Point();
  Check(logs.Any(s=>s.Contains("Resolve fallback=no target")),"No-target fallback is distinguishable from applied aim");
  logCount=logs.Count;Point();Check(logs.Count==logCount,"No-target fallback log is bounded to first resolve");
  ControllerAimAssist.Toggle();ControllerAimAssist.Acquire();Check(logs.Any(s=>s.Contains("toggle enabled=False"))&&logs.Any(s=>s.Contains("Acquire exit=disabled")),"Disabled acquisition exit and toggle are observable");
  ControllerAimAssist.Toggle();ControllerAim.Configure(()=>null);Next();ControllerAimAssist.Acquire();Check(logs.Any(s=>s.Contains("Acquire exit=no controller aim ownership")),"Noncontroller acquisition exit is observable");
  ControllerAimAssist.Reset();Check(ControllerAimAssist.Trace==null,"Reset clears optional diagnostic sink");
  Setup();var offset=Enemy(8,1,.2f,.2f);var centered=Enemy(9.5f,0,.2f,.2f);Physics.Candidates=new[]{offset,centered};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});
  Check(ControllerAimAssist.Current.Target==centered,"Hybrid score lets slightly farther centerline hitbox beat nearer offset target");
  ControllerAimAssist.CenterlineDistanceMultiplier=0;
  try {ControllerAimAssist.BeginAttack(new AttackToken{Player=local});Check(ControllerAimAssist.Current.Target==offset,"Zero centerline multiplier restores distance-only selection");}
  finally {ControllerAimAssist.CenterlineDistanceMultiplier=2;}
  centered.transform.position=new Vector3(13,0,0);ControllerAimAssist.BeginAttack(new AttackToken{Player=local});
  Check(ControllerAimAssist.Current.Target==offset,"Much nearer off-axis target still beats distant centerline target");
  Setup();var equalOffset=Enemy(7.96875f,1,0,0);var equalCentered=Enemy(10.03125f,0,0,0);Physics.Candidates=new[]{equalOffset,equalCentered};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});
  Check(ControllerAimAssist.Current.Target==equalCentered,"Equal hybrid scores prefer smaller actual centerline gap");
  Setup();var multipartHead=new GameObject("head"){layer=28};multipartHead.transform.position=new Vector3(8,1,0);var headReceiver=multipartHead.AddComponent<WormScript>();headReceiver.isMainHead=true;headReceiver.hp=100;var headHit=multipartHead.AddComponent<SphereCollider>();headHit.radius=.1f;
  var multipartBody=new GameObject("body"){layer=28};multipartBody.transform.position=new Vector3(9.5f,0,0);multipartBody.AddComponent<WormScript>().mainHead=multipartHead;var bodyHit=multipartBody.AddComponent<SphereCollider>();bodyHit.radius=.1f;Physics.Candidates=new Collider[]{headHit,bodyHit};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});
  Check(ControllerAimAssist.Current.Target==bodyHit,"Same-enemy body hitbox beats closer head using hybrid score and forwarded authoritative life");
  int multipartSearches=Physics.Searches;Point();ControllerAim.TryWorldPoint(out visual);Check(Physics.Searches==multipartSearches,"Hybrid applied resolution and visible aim do not acquire again");
  Setup();GUI.Recorded.Clear();draw.Invoke(marker,null);
  var rays=GUI.Recorded.Where(d=>d.Color.r==1&&d.Color.g==0&&d.Color.a==.7f).ToArray();
  Check(rays.Length==2&&Math.Abs(rays[0].Matrix.angle-8)<.0001&&Math.Abs(rays[1].Matrix.angle+8)<.0001,"Visible cone boundaries represent eight degrees from the same horizontal controller axis");
  Check(rays.All(d=>Math.Abs(d.Matrix.origin.x)<.0001&&Math.Abs(d.Rect.width*Math.Cos(8*Math.PI/180)-20)<.0001),"Cone starts at player center and reaches actual viewport edge");
  Check(Physics.Searches==0&&!GUI.Recorded.Any(d=>d.Color.g==1&&d.Color.r==1),"Idle cone draws without acquisition or yellow target");
  near=Enemy(8,.4f);Physics.Candidates=new[]{near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});GUI.Recorded.Clear();draw.Invoke(marker,null);
  Check(!GUI.Recorded.Any(d=>d.Color.g==1&&d.Color.r==1),"Acquisition alone does not show yellow outline");
  Point();GUI.Recorded.Clear();GUI.color=new Color(.3f,.4f,.5f);GUI.matrix=new Matrix4x4{angle=37};draw.Invoke(marker,null);
  Check(GUI.Recorded.Count(d=>d.Color.g==1&&d.Color.r==1)==4,"Yellow hitbox bounds appear only after actual assisted aim resolution");
  Check(GUI.color.r==.3f&&GUI.color.g==.4f&&GUI.matrix.angle==37,"Overlay restores caller GUI color and matrix");
  Time.unscaledTime+=.2f;near.transform.position=new Vector3(9,.4f,0);GUI.Recorded.Clear();draw.Invoke(marker,null);
  Check(GUI.Recorded.Any(d=>d.Color.g==1&&d.Color.r==1&&Math.Abs(d.Matrix.origin.x-8.5f)<.0001),"Brief applied-shot outline follows moving live hitbox");
  Time.unscaledTime+=.16f;GUI.Recorded.Clear();draw.Invoke(marker,null);Check(GUI.Recorded.Any(d=>d.Color.g==1&&d.Color.r==1),"Discrete shot outline remains visible past the former 0.35-second duration");
  Time.unscaledTime+=.35f;GUI.Recorded.Clear();draw.Invoke(marker,null);Check(!GUI.Recorded.Any(d=>d.Color.g==1&&d.Color.r==1),"Discrete shot outline expires after 0.7 seconds");
  foreach(string loss in new[]{"target","context","mouse","off","reset"}) {
   Setup();near=Enemy(8,.4f);Physics.Candidates=new[]{near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});Point();
   if(loss=="target")near.gameObject.GetComponent<EnemyScript>().dead=true;
   if(loss=="context")GameScript.inventoryOpen=true;
   if(loss=="mouse"){Input.mousePosition=new Vector3(40,20,0);Next();}
   if(loss=="off")ControllerAimAssist.Toggle();if(loss=="reset")ControllerAimAssist.Reset();
   GUI.Recorded.Clear();draw.Invoke(marker,null);Check(!GUI.Recorded.Any(d=>d.Color.g==1&&d.Color.r==1),"Applied outline clears on "+loss);
   if(loss=="mouse")Check(GUI.Recorded.Count==1,"Mouse ownership retains enabled square but hides cone and applied outline");
  }
  Setup();near=Enemy(8,.4f);Physics.Candidates=new[]{near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});Point();Time.unscaledTime+=.6f;Point();Time.unscaledTime+=.2f;
  GUI.Recorded.Clear();draw.Invoke(marker,null);Check(GUI.Recorded.Any(d=>d.Color.g==1&&d.Color.r==1),"Continuing applied staff resolutions refresh outline lifetime");
  int visualSearches=Physics.Searches;draw.Invoke(marker,null);Check(Physics.Searches==visualSearches,"Applied hitbox drawing validates retained target without enemy scanning");
  Setup();GunPrefab(30);near=Enemy(8,0);near.gameObject.AddComponent<Rigidbody>().velocity=new Vector3(0,6,0);Physics.Candidates=new[]{near};
  var muzzle=new GameObject("muzzle");muzzle.transform.position=new Vector3(1,0,0);local.shot=muzzle.transform;ControllerAimAssist.BeginAttack(new AttackToken{Player=local});assisted=Point();
  Check(Math.Abs(assisted.x-8)<.0001&&Math.Abs(assisted.y-.7f)<.0001,"Ordinary native gun leads using actual prefab speed, target velocity and muzzle distance");
  ControllerAim.TryWorldPoint(out visual);Check(visual.y==0&&Math.Abs(visual.x-14)<.0001,"Partial lead leaves visible crosshair unchanged at full radius");
  GUI.Recorded.Clear();draw.Invoke(marker,null);Check(GUI.Recorded.Any(d=>d.Color.r==0&&d.Color.g==1&&d.Color.b==1&&Math.Abs(d.Rect.x-6)<.0001&&Math.Abs(d.Rect.y-(Screen.height-.7f-2))<.0001),"Cyan marker shows final clamped lead point in screen coordinates");
  GameScript.equippedIDs[0]=405;assisted=Point();Check(assisted.x==8&&assisted.y==0,"Mid-windup equipment mismatch falls back to zero lead rather than old weapon speed");
  Setup();near=Enemy(8,0);near.gameObject.AddComponent<Rigidbody>().velocity=new Vector3(0,6,0);Physics.Candidates=new[]{near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});Check(Point().y==0,"Missing projectile speed retains normal center assistance");
  Setup();GunPrefab(30);near=Enemy(8,0);Physics.Candidates=new[]{near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});Check(Point().y==0,"Missing target velocity retains center assistance on first observation");
  near.transform.position=new Vector3(8,.2f,0);Time.unscaledTime+=.1f;assisted=Point();Check(assisted.y>.2f,"Own-hitbox center samples provide lead for transform-driven segments without Rigidbody velocity");
  Setup();GunPrefab(30);near=Enemy(8,0);near.gameObject.AddComponent<Rigidbody>().velocity=new Vector3(0,100,0);Physics.Candidates=new[]{near};ControllerAimAssist.BeginAttack(new AttackToken{Player=local});assisted=Point();
  Check(Math.Abs(Math.Atan2(assisted.y,assisted.x)*180/Math.PI-8)<.0001,"Large partial prediction clamps final aim to unchanged eight-degree player cone");
  Setup();var liveStaff=SpawnStaff(10,new Vector3(20,0,0));ControllerAimAssist.Restore(null);local.attacking=false;int beforeStaff=Physics.Searches;
  assisted=StaffTick(liveStaff);Check(Math.Abs(assisted.x-50)<.0001&&assisted.y==0,"Staff without target aims forward from its own position beyond fixed fourteen-unit cursor");
  Check(Physics.Searches==beforeStaff+1&&ControllerAimAssist.Current==null,"Only actual tracking tick searches and restores outer scope after attack release");
  near=Enemy(8,.2f);Physics.Candidates=new[]{near};assisted=StaffTick(liveStaff);Check(ControllerAimAssist.Retained(liveStaff).Target==near&&assisted.x==8,"Existing released staff acquires target on later tracking tick");
  near.transform.position=new Vector3(8,5,0);assisted=StaffTick(liveStaff);Check(assisted.x==50&&ControllerAimAssist.Retained(liveStaff).Target==null,"Staff target leaving cone falls back to forward direction");
  near.transform.position=new Vector3(8,.2f,0);StaffTick(liveStaff);Check(ControllerAimAssist.Retained(liveStaff).Target==near,"Same staff reacquires returning target without another attack press");
  var better=Enemy(4,0);Physics.Candidates=new[]{near,better};StaffTick(liveStaff);Check(ControllerAimAssist.Retained(liveStaff).Target==better,"Active staff switches to newly better hybrid target each tick");
  ControllerAimAssist.Toggle();beforeStaff=Physics.Searches;assisted=StaffTick(liveStaff);Check(assisted.x==50&&Physics.Searches==beforeStaff,"Assist-off staff keeps directional fallback while stopping all target acquisition");
  ControllerAimAssist.Toggle();StaffTick(liveStaff);Check(ControllerAimAssist.Retained(liveStaff).Target==better,"Re-enabling lets same staff target on next tracking tick");
  Setup();liveStaff=SpawnStaff(10,new Vector3(2,0,0));near=Enemy(4,0);near.gameObject.AddComponent<Rigidbody>().velocity=new Vector3(0,4,0);Physics.Candidates=new[]{near};GameScript.equippedIDs[0]=999;
  assisted=StaffTick(liveStaff);Check(Math.Abs(assisted.y-.4f)<.0001,"Staff lead uses its own current position and speed despite changed equipped weapon");
  liveStaff.isSwathe=true;assisted=StaffTick(liveStaff);Check(Math.Abs(assisted.y-.2f)<.0001,"Staff Swathe native plus-ten speed participates in lead prediction");
  var independent=SpawnStaff(30,new Vector3(3,0,0));var another=Enemy(6,0);Physics.Candidates=new[]{another};StaffTick(independent);
  Check(ControllerAimAssist.Retained(liveStaff).Target==near&&ControllerAimAssist.Retained(independent).Target==another,"Two active staff projectiles retain independent per-tick target states");
  Input.mousePosition=new Vector3(70,60,0);Next();assisted=StaffTick(liveStaff);Check(assisted.x==70&&assisted.y==60,"Mouse takeover returns actual native mouse target for controlled staff");
  Setup();liveStaff=SpawnStaff(10,Vector3.zero);near=Enemy(8,0);Physics.Candidates=new[]{near};liveStaff.SetController(false);beforeStaff=Physics.Searches;assisted=StaffTick(liveStaff);
  Check(assisted.x==Input.mousePosition.x&&Physics.Searches==beforeStaff,"Remote/noncontrolling staff never steers or scans with local controller");
  var uncorrelated=new GameObject("unknown-staff").AddComponent<Projectile>();beforeStaff=Physics.Searches;StaffTick(uncorrelated);Check(ControllerAimAssist.Retained(uncorrelated)==null&&Physics.Searches==beforeStaff,"Uncorrelated staff RPC cannot borrow latest local activation");
  object[] remoteSpawn={local,new NetworkPlayer{Id=1},null};Private(typeof(AimAssistStaffSpawnPatch),"Prefix").Invoke(null,remoteSpawn);AimAssistStaffSpawnPatch.Capture(uncorrelated.gameObject);Private(typeof(AimAssistStaffSpawnPatch),"Finalizer").Invoke(null,new[]{null,remoteSpawn[2]});Check(ControllerAimAssist.Retained(uncorrelated)==null,"Spawn correlation rejects different controlling NetworkPlayer even on local PlayerScript");
  Setup();liveStaff=SpawnStaff(10,Vector3.zero);near=Enemy(8,0);Physics.Candidates=new[]{near};GameScript.inventoryOpen=true;beforeStaff=Physics.Searches;StaffTick(liveStaff);Check(Physics.Searches==beforeStaff,"Menu/freeze gameplay guard blocks staff target scanning");
  GameScript.inventoryOpen=false;Next();StaffTick(liveStaff);Check(ControllerAimAssist.Retained(liveStaff).Target==near,"Returning to gameplay permits retained staff targeting next tick");
  Setup();Shader.Available.Clear();Shader.Available["Sprites/Default"]=new Shader{name="Sprites/Default",Color=true};GameObject nativeRoot;var nativeGlyph=NativeGlyph(out nativeRoot);var originalMaterial=nativeGlyph.GetComponent<MeshRenderer>().sharedMaterial;var originalTexture=originalMaterial.mainTexture;
  liveStaff=SpawnStaff(10,new Vector3(20,0,0));local.attacking=false;GameScript.equippedIDs[0]=999;var staffPoint=StaffTick(liveStaff);var red=RedGlyphs().Single();var redMaterial=red.GetComponent<MeshRenderer>().sharedMaterial;
  Check(red.transform.position.x==staffPoint.x&&red.transform.position.y==staffPoint.y&&Math.Abs(red.transform.position.z+.43286f)<.00001,"Red staff glyph uses exact returned fallback world point with only native render-depth offset");
  Check(!ReferenceEquals(redMaterial,originalMaterial)&&ReferenceEquals(redMaterial.mainTexture,originalTexture)&&redMaterial.Tint.r==1&&redMaterial.Tint.g==0,"Red glyph owns tinted material while sharing exact native crosshair texture");
  ControllerInputOwnership.Configure(()=>false);GUI.Draws=0;draw.Invoke(marker,null);
  Check(GUI.Draws==0&&!red.activeInHierarchy&&ControllerAimAssist.Enabled,"Shared mouse ownership immediately hides existing staff glyph and debug graphics");
  int unownedGlyphs=RedGlyphs().Length;StaffTargetCrosshair.Set(liveStaff,new Vector3(99,99,0));StaffTargetCrosshair.Refresh(true);
  Check(RedGlyphs().Length==unownedGlyphs&&!red.activeInHierarchy,"Direct marker publication cannot bypass shared ownership");
  ControllerInputOwnership.Configure(()=>true);ControllerAim.Configure(()=>pad);StaffTick(liveStaff);
  Check(ReferenceEquals(nativeGlyph.GetComponent<MeshRenderer>().sharedMaterial,originalMaterial)&&originalMaterial.Tint.r==0,"Native direction crosshair material is untouched");
  Check(red.GetComponent<ActionCursor>()==null&&red.GetComponentsInChildren<MeshRenderer>(true).Length==1&&red.GetComponent<MeshFilter>().sharedMesh==nativeGlyph.GetComponent<MeshFilter>().sharedMesh&&red.GetComponent<Animation>()!=null,"Only native mesh glyph and rotation animation are copied, excluding position updater and giant guides");
  UnityEngine.Object.Destroy(red);StaffTick(liveStaff);red=RedGlyphs().Single();Check(!redMaterial&&red&&ReferenceEquals(red.GetComponent<MeshRenderer>().sharedMaterial.mainTexture,originalTexture),"Destroyed glyph is rebuilt safely on next native tick and abandoned owned material is disposed");
  var secondStaff=SpawnStaff(10,new Vector3(30,0,0));StaffTick(secondStaff);Check(RedGlyphs().Length==2&&RedGlyphs().Any(g=>g.transform.position.x==50)&&RedGlyphs().Any(g=>g.transform.position.x==60),"Two released staff projectiles keep independent exact fallback targets after weapon switch");
  var unknownStaff=new GameObject("uncorrelated").AddComponent<Projectile>();StaffTick(unknownStaff);Check(RedGlyphs().All(g=>g.activeInHierarchy),"Uncorrelated remote tracking does not hide valid local staff target glyphs");
  near=Enemy(8,0);near.gameObject.AddComponent<Rigidbody>().velocity=new Vector3(0,4,0);Physics.Candidates=new[]{near};staffPoint=StaffTick(liveStaff);Check(red.transform.position.x==staffPoint.x&&Math.Abs(red.transform.position.y-staffPoint.y)<.00001&&red.transform.position.y>0,"Red glyph follows actual final predicted and clamped staff steering point");
  GUI.Recorded.Clear();int glyphSearches=Physics.Searches;draw.Invoke(marker,null);Check(!GUI.Recorded.Any(d=>d.Color.r==0&&d.Color.g==1&&d.Color.b==1)&&RedGlyphs().Any(g=>g.activeInHierarchy),"Staff red target glyph replaces cyan dot while leaving native direction indicator separate");
  near.transform.position=new Vector3(8,.2f,0);draw.Invoke(marker,null);Check(Math.Abs(red.transform.position.y-staffPoint.y)<.00001&&Physics.Searches==glyphSearches,"Rendering retains last native-tick target without enemy tracking or searches");
  liveStaff.SetController(false);draw.Invoke(marker,null);Check(!red.activeInHierarchy&&RedGlyphs().Any(g=>g!=red&&g.activeInHierarchy),"Single projectile ownership loss hides only its glyph");liveStaff.SetController(true);StaffTick(liveStaff);
  GameScript.inventoryOpen=true;draw.Invoke(marker,null);Check(RedGlyphs().All(g=>!g.activeInHierarchy),"Inventory context hides all staff target glyphs");GameScript.inventoryOpen=false;Next();StaffTick(liveStaff);
  foreach(string gate in new[]{"dead","frozen","player ownership"}) {
   if(gate=="dead")GameScript.dead=true;if(gate=="frozen")GadgetCore.API.GadgetCoreAPI.Frozen=true;if(gate=="player ownership")local.GetComponent<NetworkView>().isMine=false;
   draw.Invoke(marker,null);Check(RedGlyphs().All(g=>!g.activeInHierarchy),"Unsafe staff target display hides on "+gate);
   GameScript.dead=false;GadgetCore.API.GadgetCoreAPI.Frozen=false;local.GetComponent<NetworkView>().isMine=true;StaffTick(liveStaff);
  }
  Input.mousePosition=new Vector3(30,30,0);Next();draw.Invoke(marker,null);Check(RedGlyphs().All(g=>!g.activeInHierarchy),"Mouse takeover hides staff target glyphs");
  ControllerAim.Configure(()=>pad);Next();StaffTick(liveStaff);var ownedMaterial=red.GetComponent<MeshRenderer>().sharedMaterial;UnityEngine.Object.Destroy(liveStaff.gameObject);draw.Invoke(marker,null);Check(!red&&!ownedMaterial&&originalTexture,"Destroyed projectile destroys owned glyph/material but keeps shared native texture");
  Time.unscaledTime+=.71f;draw.Invoke(marker,null);Check(RedGlyphs().Length==0,"Stale staff glyph expires after 0.7 seconds without another native steering tick");
  Setup();liveStaff=SpawnStaff(10,Vector3.zero);StaffTick(liveStaff);red=RedGlyphs().Single();ownedMaterial=red.GetComponent<MeshRenderer>().sharedMaterial;ControllerAimAssist.Toggle();Check(!red&&!ownedMaterial&&RedGlyphs().Length==0,"Disabling assistance immediately clears owned staff glyphs and materials");
  ControllerAimAssist.Toggle();StaffTick(liveStaff);red=RedGlyphs().Single();ownedMaterial=red.GetComponent<MeshRenderer>().sharedMaterial;ControllerAimAssist.Reset();Check(!red&&!ownedMaterial,"Reset disposes owned staff target rendering resources");
  Setup();Shader.Available.Clear();Shader.Available["Particles/Alpha Blended"]=new Shader{name="Particles/Alpha Blended",Tint=true};liveStaff=SpawnStaff(10,Vector3.zero);StaffTick(liveStaff);red=RedGlyphs().Single();Check(red.GetComponent<MeshRenderer>().sharedMaterial.Tint.r==.5f&&red.GetComponent<MeshRenderer>().sharedMaterial.Tint.a==.5f,"Verified particle shader fallback compensates its native two-times tint");
  UnityEngine.Object.Destroy(nativeRoot);UnityEngine.Object.Destroy(nativeGlyph);GameObject replacementRoot;var replacementGlyph=NativeGlyph(out replacementRoot);StaffTick(liveStaff);red=RedGlyphs().Single();Check(ReferenceEquals(red.GetComponent<MeshRenderer>().sharedMaterial.mainTexture,replacementGlyph.GetComponent<MeshRenderer>().sharedMaterial.mainTexture),"Scene/template replacement reacquires current native glyph and rebuilds owned marker");
  ControllerAimAssist.Reset();Shader.Available.Clear();Setup();liveStaff=SpawnStaff(10,Vector3.zero);beforeStaff=Debug.Warnings.Count;StaffTick(liveStaff);StaffTick(liveStaff);Check(RedGlyphs().Length==0&&Debug.Warnings.Count==beforeStaff+1,"Unavailable tintable shaders fail gracefully with one diagnostic warning");
  ControllerAimAssist.Reset();ControllerAimAssist.StartSession();
  Check(ControllerAimAssist.Enabled&&ControllerAimAssist.Current==null&&ControllerAimAssist.SearchCount==0,"Gadget startup enables assistance without acquiring a target");
  int indicators=UnityEngine.Object.FindObjectsOfType<GameObject>().Count(g=>g.name=="ControllerSupport.AimAssistIndicator");ControllerAimAssist.StartSession();
  Check(indicators==1&&UnityEngine.Object.FindObjectsOfType<GameObject>().Count(g=>g.name=="ControllerSupport.AimAssistIndicator")==1,"Session initialization creates only one enabled indicator");
  ControllerAimAssist.Toggle();ControllerAimAssist.Configure();Check(!ControllerAimAssist.Enabled,"L3 can disable default-on assistance and adapter refresh preserves that choice");
  ControllerAimAssist.Reset();ControllerAimAssist.StartSession();Check(ControllerAimAssist.Enabled,"Next gadget session starts enabled again");ControllerAimAssist.Reset();
  ControllerAimAssist.ControllerCombatModeDebugLinesEnabled=false;
  DebugPresentationToggle();
  Console.WriteLine("Aim assist integration: "+checks+" checks passed.");
 }
}
