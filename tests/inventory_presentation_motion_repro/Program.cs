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
 Bounds after=renderer.HeldItemBounds.Value;Check(Near(after.size.x,75)&&Near(after.size.y,75),"camera motion preserves accepted held size");
 Collider next=new Collider();next.transform.parent=Camera.main.transform;next.transform.localScale=new Vector3(.5f,2,1);next.bounds=new Bounds{min=new Vector3(230,120,0),max=new Vector3(290,180,0)};renderer.Show(next);Bounds switched=renderer.HeldItemBounds.Value;
 Check(Near(switched.size.x,75)&&Near(switched.size.y,75)&&Near(switched.min.x,230),"switching differently scaled parents preserves accepted world geometry");
 
 Collider narrow=new Collider();narrow.transform.parent=Camera.main.transform;narrow.bounds=new Bounds{min=new Vector3(100,100,0),max=new Vector3(120,120,0)};renderer.Show(narrow);InventoryFeedback.SetContext(game,narrow,true,binding,"Place",null,false,true,renderer.HeldItemBounds,badgeProvider:()=>renderer.HeldItemBounds);Draw();var smallCount=GUI.Draws[GUI.Draws.Count-1];
 Check(smallCount.text=="9999"&&smallCount.rect.x>=70&&smallCount.rect.xMax<=90&&smallCount.font<24,"four-digit quantity fits narrow selected cell without changing item");
 Console.WriteLine("PASS "+checks+" actual renderer geometry, scaled parent, post-update camera movement, same-cell quantity checks");
 }}