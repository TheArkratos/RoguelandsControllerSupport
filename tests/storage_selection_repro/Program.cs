using System;
using ControllerSupport;
using UnityEngine;

// Compiles the production navigator; this fixture never invokes any click or
// native item action. Screen-space coordinates model side-by-side 6-column
// inventory/chest panels, with two page rows above the chest.
internal static class Program
{
    private static int failures;
    private static int mouseStep;

    private sealed class Scene
    {
        internal readonly GameScript Game = new GameScript();
        internal readonly Collider[] Inventory = new Collider[45];
        internal readonly Collider[] Chest = new Collider[36];
        internal readonly Collider[] Pages = new Collider[12];
        internal readonly Collider Upgrade;
        internal readonly InventorySelectionNavigator Navigator = new InventorySelectionNavigator();

        internal Scene(int storageLevel = 2)
        {
            Game.SetStorageLevel(storageLevel);
            Game.inventoryMain = new GameObject("inventoryMain");
            Game.inventoryBar = new GameObject("inventoryBar");
            Game.menuStorage = new GameObject("menuStorage");

            for (int i = 0; i < 6; i++)
                Inventory[i] = Add(Game.inventoryMain, (i + 1).ToString(), "stat", i, 2f);
            for (int i = 6; i < 36; i++)
                Inventory[i] = Add(Game.inventoryMain, i.ToString(), "Untagged",
                    (i - 6) % 6, -(i - 6) / 6);
            for (int i = 36; i < 42; i++)
                Inventory[i] = Add(Game.inventoryMain, i.ToString(), "Untagged", i - 36, 1f);
            for (int i = 42; i < 45; i++)
                Inventory[i] = Add(Game.inventoryMain, i.ToString(), "Untagged", -2f, i - 43);
            for (int i = 0; i < 6; i++)
                Add(Game.inventoryBar, i.ToString(), "Untagged", -3f, -i);

            for (int i = 0; i < Chest.Length; i++)
                Chest[i] = Add(Game.menuStorage, i.ToString(), "storage",
                    10f + i % 6, -(i / 6));
            for (int i = 0; i < Pages.Length; i++)
            {
                // Native page buttons are not guaranteed to share inventory's
                // layer, so exercise the navigator's separate page hit path.
                var page = new GameObject("page" + i) { layer = 0 };
                Game.storageButton[i] = page;
                Pages[i] = new Collider(page)
                {
                    bounds = new Bounds { center = new Vector3(10f + i % 6, i < 6 ? 2f : 1f),
                        size = new Vector3(1f, 1f) }
                };
            }
            Upgrade = Add(Game.menuStorage, "bStorageU", "ButtonMenu", 12f, 4f);
            Upgrade.gameObject.layer = 0;
            Physics.Hit = null;
            GameScript.inventoryOpen = true;
            Input.mousePosition = new Vector3(++mouseStep, 0f);
        }

        private static Collider Add(GameObject root, string name, string tag, float x, float y)
        {
            var item = new GameObject(name) { tag = tag };
            root.transform.Add(item.transform);
            return new Collider(item)
            {
                bounds = new Bounds { center = new Vector3(x, y), size = new Vector3(1f, 1f) }
            };
        }

        internal void Hover(Collider collider)
        {
            Physics.Hit = collider;
            Input.mousePosition = new Vector3(++mouseStep, 0f);
            Navigator.Update(Game, new JoystickInputState());
        }

        internal void Step(float x, float y)
        {
            Physics.Hit = null;
            Navigator.Update(Game, new JoystickInputState());
            Navigator.Update(Game, new JoystickInputState { AimX = x, AimY = y });
        }

        internal void Expect(string label, Collider expected, int expectedId)
        {
            int expectedPage = expectedId >= 87 && expectedId <= 98 ? expectedId - 87 : -1;
            bool metadataMatches = Navigator.SelectedStoragePage == expectedPage &&
                Navigator.SelectedIsStoragePage == (expectedPage >= 0) &&
                Navigator.SelectedIsStorageUpgrade == (expectedId == 99);
            if (Navigator.SelectedCollider == expected && Navigator.SelectedSlot == expectedId && metadataMatches)
                Console.WriteLine("PASS " + label);
            else
            {
                failures++;
                Console.WriteLine("FAIL " + label + ": collider=" +
                    (Navigator.SelectedCollider == null ? "none" : Navigator.SelectedCollider.gameObject.name) +
                    ", slot=" + Navigator.SelectedSlot + ", expected=" + expectedId +
                    ", page=" + Navigator.SelectedStoragePage +
                    ", upgrade=" + Navigator.SelectedIsStorageUpgrade);
            }
        }
    }

    private static void MainCases()
    {
        var s = new Scene();
        for (int i = 0; i < 36; i++)
        {
            s.Hover(s.Chest[i]);
            s.Expect("chest hover " + i, s.Chest[i], 51 + i);
        }
        s.Hover(s.Chest[30]);
        s.Step(1f, 0f);
        s.Expect("extra row right 30 to 31", s.Chest[31], 82);
        s.Step(0f, -1f);
        s.Expect("extra row up 31 to 25", s.Chest[25], 76);
        s.Step(0f, 1f);
        s.Expect("extra row down 25 to 31", s.Chest[31], 82);

        s.Hover(s.Inventory[11]);
        s.Step(1f, 0f);
        s.Expect("inventory top edge to chest left edge, same row", s.Chest[0], 51);
        s.Step(-1f, 0f);
        s.Expect("chest top edge to inventory right edge, same row", s.Inventory[11], 11);
        s.Hover(s.Inventory[35]);
        s.Step(1f, 0f);
        s.Expect("inventory bottom to chest fifth row left edge", s.Chest[24], 75);
        s.Step(-1f, 0f);
        s.Expect("chest fifth row to aligned inventory bottom", s.Inventory[35], 35);
        s.Hover(s.Chest[30]);
        s.Step(-1f, 0f);
        s.Expect("sixth chest row left returns to bottom-right inventory", s.Inventory[35], 35);

        s.Hover(s.Chest[0]);
        s.Step(0f, -1f);
        s.Expect("chest top up to unlocked upper page when lower locked", s.Pages[0], 87);
        s.Step(0f, 1f);
        s.Expect("lower page down to aligned chest top", s.Chest[0], 51);
        s.Hover(s.Pages[1]);
        s.Expect("unlocked page hover", s.Pages[1], 88);
        s.Step(1f, 0f);
        s.Expect("unlocked page right to next unlocked page", s.Pages[2], 89);
        s.Step(1f, 0f);
        s.Expect("locked next page cannot be selected", s.Pages[2], 89);

        s.Hover(s.Pages[3]);
        s.Expect("locked page ignored on mouse hover", s.Pages[2], 89);
        s.Hover(s.Chest[35]);
        s.Expect("mouse retakes from controller at extra row", s.Chest[35], 86);

        s.Game.menuStorage.activeInHierarchy = false;
        GameScript.inventoryOpen = false;
        s.Navigator.Update(s.Game, new JoystickInputState());
        s.Expect("close clears selection", null, -1);
    }

    private static void UnlockedPageRowsCase()
    {
        var s = new Scene(11);
        s.Hover(s.Chest[0]);
        s.Step(0f, -1f);
        s.Expect("chest top up to lower page when unlocked", s.Pages[6], 93);
        s.Step(0f, -1f);
        s.Expect("lower page up to upper page", s.Pages[0], 87);
        s.Step(0f, 1f);
        s.Expect("upper page down to lower page", s.Pages[6], 93);
        s.Step(0f, 1f);
        s.Expect("lower page down to chest top", s.Chest[0], 51);
    }

    private static void PageLeftToInventoryCase()
    {
        // The chest is to the right of the inventory. From either leftmost
        // page icon, Left crosses back to the nearest inventory-panel edge:
        // the rightmost slot of its top item row (slot 11). Other page-column
        // Left moves continue to traverse their own page row.
        var s = new Scene(11);
        s.Hover(s.Pages[0]);
        s.Step(-1f, 0f);
        s.Expect("upper page left edge returns to inventory", s.Inventory[11], 11);

        s.Hover(s.Pages[6]);
        s.Step(-1f, 0f);
        s.Expect("lower page left edge returns to inventory", s.Inventory[11], 11);

        s.Hover(s.Pages[1]);
        s.Step(-1f, 0f);
        s.Expect("interior page left still traverses page row", s.Pages[0], 87);
    }

    private static void UpgradeCase()
    {
        var s = new Scene();
        s.Hover(s.Upgrade);
        s.Expect("native capacity upgrade discovered on button layer", s.Upgrade, 99);
        s.Hover(s.Pages[2]);
        s.Step(0f, -1f);
        s.Expect("upper page up reaches capacity upgrade", s.Upgrade, 99);
        s.Step(0f, 1f);
        s.Expect("upgrade down returns to nearest unlocked page", s.Pages[2], 89);
        s.Step(1f, 0f);
        s.Expect("locked page still excluded with upgrade present", s.Pages[2], 89);
        s.Upgrade.enabled = false;
        s.Hover(s.Upgrade);
        s.Expect("disabled upgrade cannot be selected", s.Pages[2], 89);
    }

    private static void ExistingInventoryCase()
    {
        var s = new Scene();
        s.Game.menuStorage.activeInHierarchy = false;
        s.Hover(s.Inventory[6]);
        s.Step(0f, -1f);
        s.Expect("ordinary inventory top up still reaches equipped", s.Inventory[36], 36);
        s.Step(0f, 1f);
        s.Expect("equipped down returns to inventory", s.Inventory[6], 6);
    }

    private static int Main()
    {
        MainCases();
        UnlockedPageRowsCase();
        PageLeftToInventoryCase();
        ExistingInventoryCase();
        UpgradeCase();
        Console.WriteLine("RESULT " + failures + " failures");
        return failures == 0 ? 0 : 1;
    }
}
