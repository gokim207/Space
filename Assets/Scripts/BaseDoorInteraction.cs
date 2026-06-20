using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BaseDoorInteraction : MonoBehaviour
{
    public string runSceneName = "RunScene";
    public string playerName = "Player";
    public string startObjectName = "startObject";
    public string bossObjectName = "bossObject";
    public string startButtonName = "btnStart";
    public string bossButtonName = "btnBoss";

    BasePlayerController player;
    RectTransform playerRect;
    RectTransform startRect;
    RectTransform bossRect;
    Button startButton;
    Button bossButton;
    TMP_Text bossText;
    GameObject bossLock;
    float nextSearchTime;
    bool nearStart;
    bool nearBoss;

    void Awake()
    {
        FindTargets();
        SetPromptVisible(startButton, false);
        SetPromptVisible(bossButton, false);
        RefreshBossPrompt();
    }

    void Update()
    {
        if (Time.unscaledTime >= nextSearchTime)
            FindTargets();

        nearStart = IsOverlapping(playerRect, startRect);
        nearBoss = IsOverlapping(playerRect, bossRect);

        SetPromptVisible(startButton, nearStart);
        SetPromptVisible(bossButton, nearBoss);

        if (!IsEnterPressed())
            return;

        if (nearStart)
            StartExploration();
        else if (nearBoss)
            InteractWithBossDoor();
    }

    void FindTargets()
    {
        nextSearchTime = Time.unscaledTime + 0.25f;

        GameObject playerObject = FindByName(playerName);
        if (playerObject != null)
        {
            player = playerObject.GetComponent<BasePlayerController>();
            if (player == null)
                player = playerObject.AddComponent<BasePlayerController>();
            playerRect = playerObject.GetComponent<RectTransform>();
        }

        startRect = FindByName(startObjectName)?.GetComponent<RectTransform>();
        bossRect = FindByName(bossObjectName)?.GetComponent<RectTransform>();

        BindPrompt(ref startButton, startButtonName, StartExploration);
        BindPrompt(ref bossButton, bossButtonName, InteractWithBossDoor);
        BindBossPromptParts();
        RefreshBossPrompt();
    }

    void BindPrompt(ref Button target, string objectName, UnityEngine.Events.UnityAction action)
    {
        GameObject promptObject = FindByName(objectName);
        Button found = promptObject != null ? promptObject.GetComponent<Button>() : null;
        if (found == null || found == target)
            return;

        target = found;
        target.onClick.RemoveListener(action);
        target.onClick.AddListener(action);
    }

    void BindBossPromptParts()
    {
        if (bossButton == null)
            return;

        Transform textTransform = FindChildRecursive(bossButton.transform, "text");
        Transform lockTransform = FindChildRecursive(bossButton.transform, "lock");
        bossText = textTransform != null ? textTransform.GetComponent<TMP_Text>() : null;
        bossLock = lockTransform != null ? lockTransform.gameObject : null;
    }

    void StartExploration()
    {
        player?.SaveReturnPoint();
        GameFlowManager.Instance?.SaveCurrentSlot();
        BossBattleSession.EnterNormalRun();
        Time.timeScale = 1f;
        SceneManager.LoadScene(runSceneName);
    }

    void InteractWithBossDoor()
    {
        if (!AreAllWeaponsUnlocked())
        {
            Debug.Log("보스전 잠금: 모든 무기를 해금해야 합니다.");
            return;
        }

        player?.SaveReturnPoint();
        GameFlowManager.Instance?.SaveCurrentSlot();
        BossBattleSession.EnterBossBattle();
        Time.timeScale = 1f;
        SceneManager.LoadScene(runSceneName);
    }

    void RefreshBossPrompt()
    {
        bool unlocked = AreAllWeaponsUnlocked();
        if (bossText != null)
            bossText.text = unlocked ? "보스전" : string.Empty;
        if (bossLock != null)
            bossLock.SetActive(!unlocked);
    }

    static bool AreAllWeaponsUnlocked()
    {
        var weapons = GameData.GetWeapons();
        if (weapons == null || weapons.Count == 0)
            return false;

        for (int i = 0; i < weapons.Count; i++)
        {
            var weapon = weapons[i];
            if (weapon == null || string.IsNullOrWhiteSpace(weapon.weaponId))
                continue;
            if (!WeaponPanelManager.IsOwned(weapon.weaponId))
                return false;
        }

        return true;
    }

    static void SetPromptVisible(Button prompt, bool visible)
    {
        if (prompt != null && prompt.gameObject.activeSelf != visible)
            prompt.gameObject.SetActive(visible);
    }

    static bool IsEnterPressed()
    {
        if (Keyboard.current != null)
            return Keyboard.current.enterKey.wasPressedThisFrame ||
                   Keyboard.current.numpadEnterKey.wasPressedThisFrame;

        return Input.GetKeyDown(KeyCode.Return) ||
               Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    static bool IsOverlapping(RectTransform a, RectTransform b)
    {
        if (a == null || b == null)
            return false;

        return GetWorldRect(a).Overlaps(GetWorldRect(b));
    }

    static Rect GetWorldRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
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

    static GameObject FindByName(string targetName)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChildRecursive(roots[i].transform, targetName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (string.Equals(root.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null)
                return found;
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
        if (Object.FindObjectOfType<BaseDoorInteraction>() != null)
            return;

        new GameObject("BaseDoorInteraction_Auto").AddComponent<BaseDoorInteraction>();
    }
}
