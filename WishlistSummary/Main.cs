using BepInEx;
using HarmonyLib;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace WishlistSummary
{
    [BepInPlugin("Aidanamite.WishlistSummary", "WishlistSummary", "1.0.5")]
    public class Main : BaseUnityPlugin
    {
        internal static Assembly modAssembly = Assembly.GetExecutingAssembly();
        internal static string modName = $"{modAssembly.GetName().Name}";
        internal static string modDir = $"{Environment.CurrentDirectory}\\BepInEx\\{modName}";
        public static Main instance;

        void Awake()
        {
            instance = this;
            new Harmony($"com.Aidanamite.{modName}").PatchAll(modAssembly);
            Logger.LogInfo($"{modName} has loaded");
        }

        public static void RecalculateCosts(out List<RecipeIngredient> costs, List<KeyValuePair<ItemMaster, List<RecipeIngredient>>> wishlist)
        {
            costs = new List<RecipeIngredient>();
            foreach (var pair in wishlist)
                foreach (var ingredient in pair.Value)
                {
                    if (ingredient._requiredItem is EquipmentItemMaster)
                        continue;
                    bool flag = true;
                    foreach (var cost in costs)
                        if (cost.itemName == ingredient.itemName)
                        {
                            cost.quantity += ingredient.quantity;
                            flag = false;
                            break;
                        }
                    if (flag)
                        costs.Add(new RecipeIngredient() { itemName = ingredient.itemName, quantity = ingredient.quantity, _requiredItem = ingredient._requiredItem });
                }

        }

        public static void Log(object message) => instance.Logger.LogInfo(message);

        static ItemMaster last;
        void Update()
        {
            if (Patch_InventoryPanel.label && Patch_InventoryPanel.label.isActiveAndEnabled && Patch_InventoryPanel.wished && !string.IsNullOrEmpty(Patch_InventoryPanel.name) && GUIManager.Instance && GUIManager.Instance.input != null)
            {
                if (GUIManager.Instance.input.Action3.WasReleased)
                    Patch_InventoryPanel.label.text = Patch_InventoryPanel.name;
                else if (GUIManager.Instance.input.Action3.WasPressed || (Patch_InventoryPanel.stack.master != last && GUIManager.Instance.input.Action3.IsPressed))
                    Patch_InventoryPanel.label.text = Patch_InventoryPanel.name + $" ({Patch_Wishlist.items.Find((x) => x.itemName == Patch_InventoryPanel.stack.master.name).quantity})";
            }
            last = Patch_InventoryPanel.stack ? Patch_InventoryPanel.stack.master : null;
        }
    }

    [HarmonyPatch(typeof(WishlistManager))]
    public class Patch_Wishlist
    {
        public static List<RecipeIngredient> items = new List<RecipeIngredient>();

        [HarmonyPatch("Item", MethodType.Getter)]
        [HarmonyPrefix]
        static bool Item(ref int __0, ref KeyValuePair<ItemMaster, List<RecipeIngredient>> __result)
        {
            if (__0 >= items.Count)
            {
                __0 -= items.Count;
                return true;
            }
            __result = new KeyValuePair<ItemMaster, List<RecipeIngredient>>(items[__0]._requiredItem, new List<RecipeIngredient> { items[__0] });
            return false;
        }

        [HarmonyPatch("Count", MethodType.Getter)]
        [HarmonyPostfix]
        static void Count(ref int __result) => __result += items.Count;

        [HarmonyPatch("Remove", new Type[] { typeof(int) })]
        [HarmonyPrefix]
        static bool Remove(ref int index, ref bool __state)
        {
            index -= items.Count;
            return __state = index >= 0;
        }
        [HarmonyPatch("Remove", new Type[] { typeof(int) })]
        [HarmonyPostfix]
        static void Remove(List<KeyValuePair<ItemMaster, List<RecipeIngredient>>> ____whislistedItems, bool __state)
        {
            if (__state)
                Main.RecalculateCosts(out items, ____whislistedItems);
        }

        [HarmonyPatch("Remove", new Type[] { typeof(ItemMaster) })]
        [HarmonyPostfix]
        static void Remove(List<KeyValuePair<ItemMaster, List<RecipeIngredient>>> ____whislistedItems) => Main.RecalculateCosts(out items, ____whislistedItems);

        [HarmonyPatch("AddRecipe")]
        [HarmonyPostfix]
        static void AddRecipe(List<KeyValuePair<ItemMaster, List<RecipeIngredient>>> ____whislistedItems) => Main.RecalculateCosts(out items, ____whislistedItems);

        [HarmonyPatch("AddEnchantment")]
        [HarmonyPostfix]
        static void AddEnchantment(List<KeyValuePair<ItemMaster, List<RecipeIngredient>>> ____whislistedItems) => Main.RecalculateCosts(out items, ____whislistedItems);

    }

    [HarmonyPatch(typeof(WishlistPanel), "InitSlots")]
    public class Patch_PanelItems
    {
        static Color defaultColor = Color.clear;
        static Color highlight = new Color(0.9f, 0, 0.1f);
        static void Postfix(WishlistPanel __instance, int minIndex)
        {
            for (int i = 0; i < __instance.wishlistSlots.Count; i++)
            {
                if (defaultColor.a == 0)
                    defaultColor = __instance.wishlistSlots[i].textQuantity.color;
                __instance.wishlistSlots[i].textQuantity.color = (i + minIndex < Patch_Wishlist.items.Count) ? highlight : defaultColor;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryPanel), "OnInventorySlotSelected")]
    public class Patch_InventoryPanel
    {
        public static string name;
        public static UnityEngine.UI.Text label;
        public static ItemStack stack;
        public static bool wished = false;
        static void Postfix(InventoryPanel __instance, InventorySlotGUI obj)
        {
            label = __instance.labelSelectedItem;
            name = label ? label.text : null;
            stack = obj ? obj.itemStack : null;
            wished = stack && stack.master != null && Patch_Wishlist.items.Exists((x) => x.itemName == stack.master.name);
        }
    }
}