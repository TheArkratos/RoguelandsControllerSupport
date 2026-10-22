using System;
using System.Collections;
using System.Reflection;
using GadgetCore.API;
using UnityEngine;

namespace ControllerSupport
{
    // Native state and recipe eligibility only; native click dispatch owns slotID,
    // conversion, animations, sounds and placement-triggered mode rearming.
    internal static class EmblemForgeContext
    {
        internal const int ToggleSlot = 130;
        private static readonly FieldInfo Pane = Field("menuEmblemForge");
        private static readonly FieldInfo Mode = Field("emblemForging");
        private static readonly FieldInfo Again = Field("emblemAgain");
        private static readonly FieldInfo Recipes = typeof(GadgetCoreAPI).GetField("emblemForgeRecipes",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo Toggle = typeof(GameScript).GetMethod("EmblemForging",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static FieldInfo Field(string name)
        { return typeof(GameScript).GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); }
        internal static GameObject Root(GameScript game)
        { return game != null && Pane != null ? Pane.GetValue(game) as GameObject : null; }
        internal static bool IsOpen(GameScript game)
        { GameObject pane=Root(game); return GameScript.inventoryOpen && pane && pane.activeInHierarchy; }
        internal static bool ModeActive(GameScript game)
        { return IsOpen(game) && Mode != null && (bool)Mode.GetValue(game); }
        internal static Collider CollectTarget(GameScript game)
        { return IsOpen(game) ? Find(Root(game).transform) : null; }
        private static Collider Find(Transform parent)
        {
            for(int i=0;i<parent.childCount;i++)
            {
                Transform child=parent.GetChild(i);
                if(!child || !child.gameObject.activeInHierarchy) continue;
                if(child.gameObject.name=="bEmblem")
                {
                    Collider collider=child.GetComponent<Collider>();
                    if(collider && collider.enabled) return collider;
                }
                Collider nested=Find(child); if(nested) return nested;
            }
            return null;
        }
        internal static bool CanActivate(GameScript game,int selection,Item target,out string reason)
        {
            reason=null;
            if(!IsOpen(game) || Toggle==null || Mode==null || Again==null) reason="Emblem forge unavailable";
            else if(GadgetCoreAPI.IsInputFrozen()) reason="Input is frozen";
            else if(InventoryItemLifecycle.MutationsBlocked(game)) reason="A drop needs inspection before more item actions";
            else if(selection==ToggleSlot)
            {
                Item held=InventoryItemLifecycle.Held(game);
                if(held!=null && held.id>0 && held.q>0) reason="Place or drop the held item first";
            }
            else if(!ModeActive(game)) reason="Enable forging first";
            else if(!(selection>=6&&selection<=35)) reason="Select a backpack item for forging";
            else
            {
                Item held=InventoryItemLifecycle.Held(game);
                if(held!=null && held.id>0 && held.q>0) reason="Cancel forging before placing the held item";
                else if(target==null || target.id<=0 || target.q<10) reason="Forging needs at least 10 eligible items";
                else if(Recipes==null) reason="Forge recipes unavailable";
                else
                {
                    IDictionary recipes=Recipes.GetValue(null) as IDictionary;
                    if(recipes==null) reason="Forge recipes unavailable";
                    else if(recipes.Contains(target.id))
                    {
                        object recipe=recipes[target.id];
                        PropertyInfo output=recipe==null?null:recipe.GetType().GetProperty("Item1");
                        PropertyInfo count=recipe==null?null:recipe.GetType().GetProperty("Item2");
                        if(output==null || count==null || !(output.GetValue(recipe,null) is int) ||
                            !(count.GetValue(recipe,null) is int)) reason="Forge recipe unavailable";
                        else if((int)count.GetValue(recipe,null)!=10) reason="This recipe uses an unsafe forge quantity";
                        else if((int)output.GetValue(recipe,null)<=0) reason="Forge recipe has no valid output";
                    }
                    else if(target.id>40) reason="This item has no emblem recipe";
                }
            }
            return reason==null;
        }
        internal static void CancelMode(GameScript game)
        {
            if(!ModeActive(game)) return;
            Toggle.Invoke(game,null);
            if(Again!=null) Again.SetValue(game,false);
        }
        internal static string ActionLabel(GameScript game,int selection)
        { return selection==ToggleSlot ? ModeActive(game)?"Cancel forging":"Enable forging" : "Forge emblems"; }
    }
}
