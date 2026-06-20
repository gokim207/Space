using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BossBattleBootstrap : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return null;
        ConfigureRunScene();
    }

    void ConfigureRunScene()
    {
        bool bossMode = BossBattleSession.IsBossBattle;
        GameObject boss = FindInScene("Boss");
        GameObject bossPanel = FindInScene("bossPanel");
        GameObject runPanel = FindInScene("runPanel");

        if (boss != null)
            boss.SetActive(bossMode);
        if (bossPanel != null)
            bossPanel.SetActive(bossMode);
        if (runPanel != null)
            runPanel.SetActive(!bossMode);

        var oxygenSystem = FindFirstObjectByType<OxygenSystem>();
        if (oxygenSystem != null)
            oxygenSystem.RebindUI();

        var spawners = FindObjectsByType<EnemySpawner>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
            spawners[i].enabled = !bossMode;

        if (!bossMode || boss == null)
            return;

        var controller = boss.GetComponent<BossController>();
        if (controller == null)
            controller = boss.AddComponent<BossController>();
        controller.BeginBattle();
    }

    static GameObject FindInScene(string targetName)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChild(roots[i].transform, targetName);
            if (found != null)
                return found.gameObject;
        }
        return null;
    }

    static Transform FindChild(Transform root, string targetName)
    {
        if (string.Equals(root.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    static void TryInstall(Scene scene)
    {
        if (scene.name != "RunScene")
            return;
        if (FindFirstObjectByType<BossBattleBootstrap>() != null)
            return;

        new GameObject("BossBattleBootstrap_Auto").AddComponent<BossBattleBootstrap>();
    }
}
