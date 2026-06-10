using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class BaseComputerInteraction : MonoBehaviour
{
    public string upgradeSceneName = "UpgradeScene";
    public string playerName = "Player";
    public string[] targetNameHints = { "computer", "컴퓨터", "terminal", "hit", "hitbox", "upgrade" };

    private BasePlayerController player;
    private RectTransform playerRect;
    private RectTransform targetRect;
    private float nextSearchTime;

    void Awake()
    {
        FindTargets(true);
    }

    void Update()
    {
        if (Time.unscaledTime >= nextSearchTime)
            FindTargets(false);

        if (!IsEnterPressed())
            return;

        if (player == null || targetRect == null)
            return;

        if (!IsOverlapping(playerRect, targetRect))
            return;

        player.SaveReturnPoint();
        Time.timeScale = 1f;
        SceneManager.LoadScene(upgradeSceneName);
    }

    void FindTargets(bool force)
    {
        nextSearchTime = Time.unscaledTime + 0.25f;

        if (force || player == null)
        {
            var playerGo = FindByName(playerName);
            if (playerGo != null)
            {
                player = playerGo.GetComponent<BasePlayerController>();
                if (player == null)
                    player = playerGo.AddComponent<BasePlayerController>();
                playerRect = playerGo.GetComponent<RectTransform>();
            }
        }

        if (!force && targetRect != null && targetRect.gameObject.activeInHierarchy)
            return;

        targetRect = FindInteractionRect();
    }

    bool IsEnterPressed()
    {
        if (Keyboard.current != null)
            return Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    static bool IsOverlapping(RectTransform a, RectTransform b)
    {
        if (a == null || b == null)
            return false;

        var aRect = GetWorldRect(a);
        var bRect = GetWorldRect(b);
        return aRect.Overlaps(bRect);
    }

    static Rect GetWorldRect(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float minX = corners[0].x;
        float minY = corners[0].y;
        float maxX = corners[0].x;
        float maxY = corners[0].y;

        for (int i = 1; i < corners.Length; i++)
        {
            minX = Mathf.Min(minX, corners[i].x);
            minY = Mathf.Min(minY, corners[i].y);
            maxX = Mathf.Max(maxX, corners[i].x);
            maxY = Mathf.Max(maxY, corners[i].y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    RectTransform FindInteractionRect()
    {
        var rects = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < rects.Length; i++)
        {
            var rt = rects[i];
            if (rt == null || !rt.gameObject.scene.IsValid())
                continue;
            if (rt.gameObject.scene != SceneManager.GetActiveScene())
                continue;
            if (!rt.gameObject.activeInHierarchy)
                continue;
            if (rt.name == playerName || rt.GetComponent<BasePlayerController>() != null)
                continue;

            string lowerName = rt.name.ToLowerInvariant();
            for (int h = 0; h < targetNameHints.Length; h++)
            {
                if (lowerName.Contains(targetNameHints[h].ToLowerInvariant()))
                    return rt;
            }
        }

        return null;
    }

    static GameObject FindByName(string targetName)
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var found = FindChildRecursive(roots[i].transform, targetName);
            if (found != null)
                return found.gameObject;
        }
        return null;
    }

    static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var result = FindChildRecursive(root.GetChild(i), targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallForInitialScene()
    {
        TryInstall(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    static void TryInstall(Scene scene)
    {
        if (!BaseSceneNavigation.IsBaseSceneName(scene.name))
            return;

        var existing = Object.FindObjectOfType<BaseComputerInteraction>();
        if (existing != null)
            return;

        var go = new GameObject("BaseComputerInteraction_Auto");
        go.AddComponent<BaseComputerInteraction>();
    }
}
