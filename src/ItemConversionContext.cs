using System.Collections;
using System.Reflection;
using GadgetCore.API;
using UnityEngine;

namespace ControllerSupport
{
    // Context/eligibility only. Native click dispatch retains item mutation,
    // slotID, values/recipes, sounds, animations and mode side effects.
    internal static class ItemConversionContext
    {
        internal const int ToggleSlot = 130;
        private sealed class Context
        {
            internal readonly FieldInfo Pane, Mode, Again;
            internal readonly MethodInfo Toggle;
            internal readonly string Button, Verb;
            internal readonly bool IsTrasher;
            internal Context(string pane, string mode, string again, string toggle, string button, string verb, bool trasher)
            {
                Pane=Field(pane); Mode=Field(mode); Again=Field(again);
                Toggle=typeof(GameScript).GetMethod(toggle,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                Button=button; Verb=verb; IsTrasher=trasher;
            }
            internal GameObject Root(GameScript game)
            { return game!=null && Pane!=null ? Pane.GetValue(game) as GameObject : null; }
        }
        private static readonly Context[] Contexts = {
            new Context("menuEmblemForge","emblemForging","emblemAgain","EmblemForging","bEmblem","forging",false),
            new Context("menuTrasher","trashing","trashAgain","Trasher","bTrasher","trashing",true)
        };
        private static readonly FieldInfo Recipes = typeof(GadgetCoreAPI).GetField("emblemForgeRecipes",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static FieldInfo Field(string name)
        { return typeof(GameScript).GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); }
        private static Context Active(GameScript game)
        {
            if(!GameScript.inventoryOpen) return null;
            foreach(Context context in Contexts)
            {
                GameObject pane=context.Root(game);
                if(pane && pane.activeInHierarchy) return context;
            }
            return null;
        }
        internal static GameObject Root(GameScript game)
        { Context context=Active(game); return context==null ? null : context.Root(game); }
        internal static bool IsOpen(GameScript game) { return Active(game)!=null; }
        internal static bool ModeActive(GameScript game)
        { Context context=Active(game); return context!=null && context.Mode!=null && (bool)context.Mode.GetValue(game); }
        internal static Collider CollectTarget(GameScript game)
        { Context context=Active(game); return context==null ? null : Find(context.Root(game).transform,context.Button); }
        private static Collider Find(Transform parent,string button)
        {
            for(int i=0;i<parent.childCount;i++)
            {
                Transform child=parent.GetChild(i);
                if(!child || !child.gameObject.activeInHierarchy) continue;
                if(child.gameObject.name==button)
                {
                    Collider collider=child.GetComponent<Collider>();
                    if(collider && collider.enabled) return collider;
                }
                Collider nested=Find(child,button); if(nested) return nested;
            }
            return null;
        }
        internal static bool CanActivate(GameScript game,int selection,Item target,out string reason)
        {
            Context context=Active(game); reason=null;
            if(context==null || context.Toggle==null || context.Mode==null || context.Again==null) reason="Item conversion unavailable";
            else if(GadgetCoreAPI.IsInputFrozen()) reason="Input is frozen";
            else if(InventoryItemLifecycle.MutationsBlocked(game)) reason="A drop needs inspection before more item actions";
            else if(selection==ToggleSlot)
            {
                Item held=InventoryItemLifecycle.Held(game);
                if(held!=null && held.id>0 && held.q>0) reason="Place or drop the held item first";
            }
            else if(!ModeActive(game)) reason="Enable "+context.Verb+" first";
            // Hotkeys/equipment require separate side-effect coverage. Both
            // contexts deliberately support backpack conversion only for now.
            else if(!(selection>=6&&selection<=35)) reason="Select a backpack item for "+context.Verb;
            else
            {
                Item held=InventoryItemLifecycle.Held(game);
                if(held!=null && held.id>0 && held.q>0) reason="Cancel "+context.Verb+" before placing the held item";
                else if(context.IsTrasher)
                {
                    if(target==null || target.id<=0 || target.q<=0) reason="Select an item to convert";
                    else if(target.id==51 || target.id==52) reason="This item cannot be converted to Credits";
                    // GetItemWorth is intentionally not called here. Native
                    // TrashItem evaluates effective values only on activation.
                }
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
            Context context=Active(game);
            if(context==null || !ModeActive(game)) return;
            context.Toggle.Invoke(game,null);
            if(context.Again!=null) context.Again.SetValue(game,false);
        }
        internal static string CancelLabel(GameScript game)
        { Context context=Active(game); return context==null ? null : "Cancel "+context.Verb; }
        internal static string SecondaryRejection(GameScript game)
        { return "Use primary for item conversion; secondary and transfer are unavailable"; }
        internal static string ActionLabel(GameScript game,int selection)
        {
            Context context=Active(game); if(context==null) return null;
            return selection==ToggleSlot ? ModeActive(game)?CancelLabel(game):"Enable "+context.Verb
                : context.IsTrasher?"Convert to Credits":"Forge emblems";
        }
    }
}
