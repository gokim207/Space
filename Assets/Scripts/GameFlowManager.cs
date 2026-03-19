using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public class GameFlowManager : MonoBehaviour
{
    public enum GamePhase { Run, End, Forge, SkillTree }
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Run;

    private Canvas canvas;
    private GameObject runHud;
    private GameObject endPanel;
    private GameObject forgePanel;
    private GameObject skillPanel;
    private Text oxygenLabel;
    private Text oreResultLabel;
    private OxygenSystem oxygenSystem;
    private WaveManager waveManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureExists()
    {
        if (FindObjectOfType<GameFlowManager>() != null) return;
        var go = new GameObject("GameFlowManager");
        go.AddComponent<GameFlowManager>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        BuildUI();
        SceneManager.sceneLoaded += OnSceneLoaded;
        ShowRun();
    }

    void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("UI_Canvas");
        canvasGO.transform.SetParent(transform);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        runHud = CreatePanel("RunHUD", new Color(0f, 0f, 0f, 0f));
        endPanel = CreatePanel("EndPanel", new Color(0f, 0f, 0f, 0.35f));
        forgePanel = CreatePanel("ForgePanel", new Color(0f, 0f, 0f, 0.35f));
        skillPanel = CreatePanel("SkillPanel", new Color(0f, 0f, 0f, 0.35f));

        // Run HUD (placeholder)
        CreateLabel(runHud, "Wave : 1", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), 24);
        oxygenLabel = CreateLabel(runHud, "남은 산소 (0 / 0)", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -30f), 18);
        oxygenLabel.alignment = TextAnchor.MiddleLeft;
        oxygenLabel.color = Color.white;
        oxygenLabel.rectTransform.sizeDelta = new Vector2(320f, 40f);

        // End panel
        CreateLabel(endPanel, "원정 종료", new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f), Vector2.zero, 28);
        CreateLabel(endPanel, "획득한 광석", new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, 18);
        oreResultLabel = CreateLabel(endPanel, "돌 : 0", new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), Vector2.zero, 20);
        CreateButton(endPanel, "다시 하기", new Vector2(0.25f, 0.15f), new Vector2(0.25f, 0.15f), new Vector2(0f, 0f), () => RestartRun());
        CreateButton(endPanel, "기지로 이동", new Vector2(0.75f, 0.15f), new Vector2(0.75f, 0.15f), new Vector2(0f, 0f), () => GoToUpgradeScene());

        // Forge panel
        CreateLabel(forgePanel, "대장간", new Vector2(0.5f, 0.9f), new Vector2(0.5f, 0.9f), Vector2.zero, 24);
        CreateLabel(forgePanel, "재련 확률/결과 UI는 추후 연결", new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.75f), Vector2.zero, 18);
        CreateButton(forgePanel, "대장간", new Vector2(0.12f, 0.95f), new Vector2(0.12f, 0.95f), new Vector2(0f, 0f), () => ShowForge());
        CreateButton(forgePanel, "스킬 트리", new Vector2(0.36f, 0.95f), new Vector2(0.36f, 0.95f), new Vector2(0f, 0f), () => ShowSkillTree());
        CreateButton(forgePanel, "탐험 시작", new Vector2(0.86f, 0.08f), new Vector2(0.86f, 0.08f), new Vector2(0f, 0f), () => GoToRunScene());

        // Skill panel
        CreateLabel(skillPanel, "스킬 트리", new Vector2(0.5f, 0.9f), new Vector2(0.5f, 0.9f), Vector2.zero, 24);
        CreateLabel(skillPanel, "노드 UI/연결은 추후 연결", new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.75f), Vector2.zero, 18);
        CreateButton(skillPanel, "대장간", new Vector2(0.12f, 0.95f), new Vector2(0.12f, 0.95f), new Vector2(0f, 0f), () => ShowForge());
        CreateButton(skillPanel, "스킬 트리", new Vector2(0.36f, 0.95f), new Vector2(0.36f, 0.95f), new Vector2(0f, 0f), () => ShowSkillTree());
        CreateButton(skillPanel, "탐험 시작", new Vector2(0.86f, 0.08f), new Vector2(0.86f, 0.08f), new Vector2(0f, 0f), () => GoToRunScene());
    }

    GameObject CreatePanel(string name, Color bg)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(canvas.transform, false);
        var img = panel.AddComponent<Image>();
        img.color = bg;
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return panel;
    }

    Text CreateLabel(GameObject parent, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, int size)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent.transform, false);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text;
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(600f, 60f);
        return t;
    }

    void CreateButton(GameObject parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Button_" + label);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(140f, 40f);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var t = textGO.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = label;
        t.fontSize = 18;
        t.color = Color.black;
        t.alignment = TextAnchor.MiddleCenter;
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }

    public void ShowRun()
    {
        CurrentPhase = GamePhase.Run;
        Time.timeScale = 1f;
        runHud.SetActive(true);
        endPanel.SetActive(false);
        forgePanel.SetActive(false);
        skillPanel.SetActive(false);
    }

    public void ShowEnd()
    {
        CurrentPhase = GamePhase.End;
        Time.timeScale = 0f;
        runHud.SetActive(true);
        endPanel.SetActive(true);
        forgePanel.SetActive(false);
        skillPanel.SetActive(false);
    }

    public void ShowForge()
    {
        CurrentPhase = GamePhase.Forge;
        Time.timeScale = 0f;
        runHud.SetActive(false);
        endPanel.SetActive(false);
        forgePanel.SetActive(true);
        skillPanel.SetActive(false);
    }

    public void ShowSkillTree()
    {
        CurrentPhase = GamePhase.SkillTree;
        Time.timeScale = 0f;
        runHud.SetActive(false);
        endPanel.SetActive(false);
        forgePanel.SetActive(false);
        skillPanel.SetActive(true);
    }

    void Update()
    {
        if (oxygenLabel == null) return;
        if (oxygenSystem == null) oxygenSystem = FindObjectOfType<OxygenSystem>();
        if (waveManager == null) waveManager = FindObjectOfType<WaveManager>();
        if (oxygenSystem != null)
        {
            oxygenLabel.text = $"남은 산소 ({oxygenSystem.currentOxygen:0.0} / {oxygenSystem.MaxOxygen:0.0})";
        }
        if (oreResultLabel != null && waveManager != null && (CurrentPhase == GamePhase.End))
        {
            oreResultLabel.text = $"돌 : {waveManager.oresCollectedThisRun}";
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEventSystem();
        if (scene.name == "UpgradeScene")
        {
            ShowForge();
        }
        else
        {
            ShowRun();
        }
    }

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
        DontDestroyOnLoad(es);
    }

    void RestartRun()
    {
        Time.timeScale = 1f;
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    void GoToUpgradeScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("UpgradeScene");
    }

    void GoToRunScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("RunScene");
    }
}
