using UnityEditor;
using UnityEngine;

public static class SkillTools
{
    [MenuItem("Tools/Skills/Reset Slot 1 Levels To 0")]
    public static void ResetSlot1LevelsToZero()
    {
        var ids = SkillTreeManager.GetSkillIdsFromCsv();
        foreach (var id in ids)
        {
            PlayerPrefs.SetInt($"slot_1_skill_{id}", 0);
        }
        PlayerPrefs.Save();
        UnityEngine.Debug.Log($"Reset {ids.Count} skills to level 0 for slot 1.");
    }
}
