using System;using System.Reflection;using ControllerSupport;using UnityEngine;
class Program{
 static int checks; static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}static bool Near(float a,float b){return Math.Abs(a-b)<.001f;}static void Draw(){GUI.Draws.Clear();GUI.Labels.Clear();GameObject.Runner.GetType().GetMethod("OnGUI",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(GameObject.Runner,null);}
 static void Main(string[] args){
 var binding=BindingConfig.Parse(BindingConfig.DefaultText.Split('\n'));var game=new GameScript();GameScript.inventoryOpen=true;game.Hold(7,9999);Screen.width=1920;Screen.height=1200;
 Collider slot=new Collider();slot.transform.parent=Camera.main.transform;slot.transform.localScale=new Vector3(2,3,1);slot.bounds=new Bounds{min=new Vector3(100,100,0),max=new Vector3(160,160,0)};
 var renderer=SelectionCornerRenderer.TryCreate(6,30000,m=>{throw new Exception(m);});renderer.SetHeldItemIcon(7,100,100);renderer.Show(slot);Bounds before=renderer.HeldItemBounds.Value;
 Check(Near(before.min.x,100)&&Near(before.max.x,175)&&Near(before.max.y,160)&&Near(before.min.y,85),"accepted held icon placement and 75%/1.25slot size unchanged under scaled cell");
 if(args.Length==0)Camera.main.transform.position=new Vector3(12,0,0);Bounds moved=renderer.HeldItemBounds.Value;
 if(args.Length==0)Check(Near(moved.min.x-before.min.x,12)&&Near(moved.max.y,before.max.y),"badge follows camera/cell movement after selection Update before rendering");
 InventoryFeedback.SetContext(game,slot,true,binding,"Place","Place one",false,true,renderer.HeldItemBounds, badgeProvider:()=>renderer.HeldItemBounds);Camera.main.transform.position=new Vector3(30,20,0);Draw();var number=GUI.Draws[GUI.Draws.Count-1];
 Check(number.rect.x>=100&&number.rect.xMax<=160,"counter stays inside selected cell instead of next cell");
 Check(Near(number.rect.y,1040),"quantity reads live badge after camera moves vertically between Update and OnGUI");
 var corners=(SpriteRenderer[])typeof(SelectionCornerRenderer).GetField("renderers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
 Check(Near(corners[0].transform.position.x-Camera.main.transform.position.x,100)&&Near(corners[0].transform.position.y-Camera.main.transform.position.y,160),"corners remain on cell after horizontal/vertical post-Update movement");

 var style=typeof(SelectionCornerRenderer).GetMethod("SetStyle",BindingFlags.Instance|BindingFlags.NonPublic);
 Check(style!=null,"actual corner renderer supports reversible native-mode styles");
 var originalScale=new Vector3[4];var originalColor=new Color[4];var anchor=new Vector3[4];
 for(int i=0;i<4;i++){originalScale[i]=corners[i].transform.localScale;originalColor[i]=corners[i].color;anchor[i]=corners[i].transform.position;}
 var icon=(SpriteRenderer)typeof(SelectionCornerRenderer).GetField("heldIconRenderer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
 Color iconColor=icon.color;Bounds iconBefore=renderer.HeldItemBounds.Value;
 foreach(var mode in new[]{SelectionCornerStyle.Red,SelectionCornerStyle.Blue,SelectionCornerStyle.Purple}) {
 for(int frame=0;frame<100;frame++){style.Invoke(renderer,new object[]{mode});renderer.Show(slot);}
 for(int i=0;i<4;i++){
 Check(Near(corners[i].color.r,mode==SelectionCornerStyle.Red?1:mode==SelectionCornerStyle.Purple?.65f:0)&&Near(corners[i].color.g,mode==SelectionCornerStyle.Purple?.15f:0)&&Near(corners[i].color.b,mode==SelectionCornerStyle.Red?0:1)&&Near(corners[i].color.a,originalColor[i].a),"active corner uses "+mode+" and retains alpha: "+i);
 Check(Near(corners[i].transform.localScale.x,originalScale[i].x*1.5f)&&Near(corners[i].transform.localScale.y,originalScale[i].y*1.5f)&&Near(corners[i].transform.localScale.z,originalScale[i].z),"repeated active frames enlarge each corner exactly 50 percent without stacking: "+i);
 Check(Near(corners[i].transform.position.x,anchor[i].x)&&Near(corners[i].transform.position.y,anchor[i].y),"active style preserves true hitbox anchors: "+i);}
 Bounds iconActive=renderer.HeldItemBounds.Value;
 Check(Near(iconActive.min.x,iconBefore.min.x)&&Near(iconActive.max.y,iconBefore.max.y)&&Near(iconActive.size.x,iconBefore.size.x)&&Near(iconActive.size.y,iconBefore.size.y),"active style preserves held-icon geometry and quantity badge bounds");
 Check(Near(icon.color.r,iconColor.r)&&Near(icon.color.g,iconColor.g)&&Near(icon.color.b,iconColor.b)&&Near(icon.color.a,iconColor.a),"active style never tints the held icon");
 }
 style.Invoke(renderer,new object[]{SelectionCornerStyle.Normal});
 for(int i=0;i<4;i++){Check(Near(corners[i].color.r,originalColor[i].r)&&Near(corners[i].color.g,originalColor[i].g)&&Near(corners[i].color.b,originalColor[i].b)&&Near(corners[i].color.a,originalColor[i].a)&&Near(corners[i].transform.localScale.x,originalScale[i].x)&&Near(corners[i].transform.localScale.y,originalScale[i].y),"inactive restores captured corner tint and dimensions: "+i);}
 style.Invoke(renderer,new object[]{SelectionCornerStyle.Red});renderer.Hide();
 Check(Near(corners[0].transform.localScale.x,originalScale[0].x)&&Near(corners[0].color.g,originalColor[0].g),"Hide resets active style before a later menu can reuse renderer");
 renderer.Show(slot);style.Invoke(renderer,new object[]{SelectionCornerStyle.Normal});
 Check(Near(corners[0].transform.localScale.x,originalScale[0].x)&&Near(corners[0].color.g,originalColor[0].g),"hidden/reused renderer can restore normal style");
 Bounds after=renderer.HeldItemBounds.Value;Check(Near(after.size.x,75)&&Near(after.size.y,75),"camera motion preserves accepted held size");
 Collider next=new Collider();next.transform.parent=Camera.main.transform;next.transform.localScale=new Vector3(.5f,2,1);next.bounds=new Bounds{min=new Vector3(230,120,0),max=new Vector3(290,180,0)};renderer.Show(next);Bounds switched=renderer.HeldItemBounds.Value;
 Check(Near(switched.size.x,75)&&Near(switched.size.y,75)&&Near(switched.min.x,230),"switching differently scaled parents preserves accepted world geometry");
 
 Collider narrow=new Collider();narrow.transform.parent=Camera.main.transform;narrow.bounds=new Bounds{min=new Vector3(100,100,0),max=new Vector3(120,120,0)};renderer.Show(narrow);InventoryFeedback.SetContext(game,narrow,true,binding,"Place",null,false,true,renderer.HeldItemBounds,badgeProvider:()=>renderer.HeldItemBounds);Draw();var smallCount=GUI.Draws[GUI.Draws.Count-1];
 Check(smallCount.text=="9999"&&smallCount.rect.x>=70&&smallCount.rect.xMax<=90&&smallCount.font<24,"four-digit quantity fits narrow selected cell without changing item");
 renderer.SetHeldItemIcon(7,100,100,true);Check(Resources.LastPath=="cc/cc7","chip art resolves native chip resource for same numeric item ID");
 int loaded=Resources.Loads;renderer.SetHeldItemIcon(7,100,100,true);Check(Resources.Loads==loaded,"same chip icon reuses cached art");
 renderer.SetHeldItemIcon(7,100,100,false);Check(Resources.LastPath=="i/i7"&&Resources.Loads==loaded+1,"switching same ID from chip to item invalidates art cache");
 Console.WriteLine("PASS "+checks+" actual renderer geometry, scaled parent, post-update camera movement, same-cell quantity checks");
 }}