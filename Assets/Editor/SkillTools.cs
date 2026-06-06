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

        flow.AddMoney(1000f);
        UnityEngine.Debug.Log("Added $1000 to the current runtime money.");
    }

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
