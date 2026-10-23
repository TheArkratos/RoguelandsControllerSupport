using System;
using UnityEngine;
using ControllerSupport;
class Program
{
    static void Equal(Vector3 point, float x, float y, float z, string message) {
        if (point.x != x || point.y != y || point.z != z) throw new Exception(message + ": actual=" + point.x + "," + point.y + "," + point.z);
    }
    static void Main() {
        Menuu.usingController = true;
        ControllerCursor.staticX = 999; ControllerCursor.staticY = 20;
        Input.mousePosition = new Vector3(250,150,0);
        ActionCursor held = new ActionCursor(); held.z = -3; held.gameObject.name = "holdingItem"; held.Awake();
        GameScript game = new GameScript { holdingItemObj = held.gameObject };
        held.Update();
        Equal(held.transform.position,999,20,0,"native prerequisite: stale controller point");
        ControllerInputOwnership.Configure(() => false);
        InventoryCursorPosition.Update(held,game,null,false);
        Equal(held.transform.position,999,20,0,"mouse ownership must preserve native held icon position");
        ActionCursor hover = new ActionCursor(); hover.gameObject.name = "hoverItem"; hover.z = -2; hover.Awake(); hover.Update();
        InventoryCursorPosition.Update(hover,game,null,false);
        Equal(hover.transform.position,999,20,0,"mouse ownership must preserve native tooltip position");
        Collider selected = new Collider { bounds = new Bounds { center = new Vector3(8,9,2) } };
        InventoryCursorPosition.Update(hover,game,selected,true);
        Equal(hover.transform.position,999,20,0,"false shared ownership overrides stale local ownership");
        ControllerInputOwnership.Configure(() => true);
        InventoryCursorPosition.Update(hover,game,selected,true);
        Equal(hover.transform.position,8,9,-2,"controller tooltip must follow selection");
        ActionCursor combat = new ActionCursor(); combat.gameObject.name = "actionCursor"; combat.Awake(); combat.Update();
        InventoryCursorPosition.Update(combat,game,selected,false);
        Equal(combat.transform.position,999,20,0,"combat cursor must remain untouched");
        Camera.main = null; InventoryCursorPosition.Update(held,game,null,false);
        InventoryCursorPosition.Update(null,game,null,false);
        InventoryCursorPosition.Update(held,null,null,false);
        Camera.main = new Camera();
        // Real native ActionCursor.Update followed by the production adapter:
        // a menu opens after controller play, then the shared flag hands off.
        Menuu.usingController = true;
        ControllerInputOwnership.Configure(() => false);
        InventoryNativeCursor.BeforeNativeUpdate(held);
        held.Update();
        InventoryCursorPosition.Update(held,game,null,false);
        Equal(held.transform.position,25,15,-3,"REGRESSION mouse pickup must be visible at the physical mouse after controller/menu handoff");
        Menuu.usingController = true;
        InventoryNativeCursor.BeforeNativeUpdate(hover);
        hover.Update();
        InventoryCursorPosition.Update(hover,game,null,false);
        Equal(hover.transform.position,25,15,-2,"hover tooltip uses native physical mouse after stale controller flag");
        ControllerInputOwnership.Configure(() => true);
        Menuu.usingController = true;
        InventoryNativeCursor.BeforeNativeUpdate(held);
        held.Update();
        Equal(held.transform.position,999,20,0,"controller native cursor branch stays intact");
        Console.WriteLine("PASS native stale-controller/mouse takeover, tooltip ownership, combat exclusion, absent camera/game");
    }
}
