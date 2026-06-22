using UnityEditor;
using UnityEngine;

public static class SkillTools
{
    [MenuItem("Tools/Space Survivor/Add Money 1000")]
    public static void AddMoney1000()
    {
        var flow = GameFlowManager.Instance;
        if (flow == null)
        {
            UnityEngine.Debug.LogWarning("GameFlowManager가 없어 돈을 추가할 수 없습니다. Play Mode에서 실행 중인지 확인하세요.");
            return;
        }

        flow.AddMoney(1000000000000f);
        UnityEngine.Debug.Log("Added $1000 to the current runtime money.");
    }

    [MenuItem("Tools/Skills/Reset Slot 1 Levels To 0")]
    public static void ResetSlot1LevelsToZero()
    {
        var ids = SkillTreeManager.GetSkillIdsFromCsv();
        SkillTreeManager.ResetSkillsForSlot(1);
        UnityEngine.Debug.Log($"Reset {ids.Count} skills to level 0 for slot 1 (saved data, runtime cache, and UI).");
    }

    [MenuItem("Tools/Space Survivor/Reset Current Slot Weapon Unlocks")]
    public static void ResetCurrentSlotWeaponUnlocks()
    {
        int slot = GameFlowManager.CurrentSlot >= 1 ? GameFlowManager.CurrentSlot : 1;
        WeaponPanelManager.DeleteSlotWeaponData(slot);
        PlayerPrefs.SetString($"slot_{slot}_weapon_equipped", "starter_gun");
        PlayerPrefs.Save();

        foreach (var manager in Object.FindObjectsOfType<WeaponPanelManager>(true))
            manager.Refresh();

        UnityEngine.Debug.Log($"Reset weapon unlocks for slot {slot}. Default weapon remains available.");
    }

    [MenuItem("Tools/Space Survivor/Unlock All Weapons")]
    public static void UnlockAllWeapons()
    {
        int slot = GameFlowManager.CurrentSlot >= 1 ? GameFlowManager.CurrentSlot : 1;
        WeaponPanelManager.UnlockAllWeaponsForSlot(slot);

        foreach (var manager in Object.FindObjectsOfType<WeaponPanelManager>(true))
            manager.Refresh();

        UnityEngine.Debug.Log($"Unlocked all weapons for slot {slot}.");
    }

    [MenuItem("Tools/Space Survivor/Reset Current Slot Weapon Levels")]
    public static void ResetCurrentSlotWeaponLevels()
    {
        int slot = GameFlowManager.CurrentSlot >= 1 ? GameFlowManager.CurrentSlot : 1;
        WeaponPanelManager.ResetSlotWeaponLevels(slot);

        foreach (var manager in Object.FindObjectsOfType<WeaponPanelManager>(true))
            manager.Refresh();

        UnityEngine.Debug.Log($"Reset all weapon levels to Lv.{WeaponPanelManager.DefaultWeaponLevel} for slot {slot}.");
    }
}
