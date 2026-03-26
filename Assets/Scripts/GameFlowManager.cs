using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public class GameFlowManager : MonoBehaviour
{
    public enum GamePhase { Run, End, Forge, SkillTree, Title, SlotSelect }
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Run;

    private Canvas canvas;
    private GameObject runHud;
    private GameObject endPanel;
    private GameObject forgePanel;
    private GameObject skillPanel;
    private GameObject titlePanel;
    private GameObject slotPanel;
    private Text oxygenLabel;
    private Text waveLabel;
    private Text timeLabel;
    private Text oreResultLabel;
    private Text endReasonLabel;
    private OxygenSystem oxygenSystem;
    private WaveManager waveManager;
    private Text moneyLabel;
    private Text skillMoneyLabel;
    private Text forgeStoneLabel;
    private Text forgeCopperLabel;
    private GameObject forgeStoneButton;
    private GameObject forgeCopperButton;
    private Text forgeCountdownLabel;
    private Text forgeGainLabel;
    private float money = 0f;
    private int stone = 0; // total owned
    private int copper = 0;
    private int runStoneGained = 0;
    private int runCopperGained = 0;
    private bool endProcessed = false;
    private enum OreSelect { Stone, Copper }
    private OreSelect selectedOre = OreSelect.Stone;
    private enum SlotMode { Save, Load }
    private SlotMode slotMode = SlotMode.Save;
    public static int CurrentSlot { get; private set; } = -1;
    private bool forgeReady = true;
    private float forgeCooldown = 0f;

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
        Instance = this;
        BuildUI();
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (SceneManager.GetActiveScene().name == "TitleScene")
            ShowTitle();
        else
            ShowRun();
    }

    public static GameFlowManager Instance { get; private set; }

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
        titlePanel = CreatePanel("TitlePanel", new Color(0f, 0f, 0f, 0.35f));
        slotPanel = CreatePanel("SlotPanel", new Color(0f, 0f, 0f, 0.35f));

        // Run HUD (placeholder)
        waveLabel = CreateLabel(runHud, "Wave : 1", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), 24);
        oxygenLabel = CreateLabel(runHud, "남은 산소 (0 / 0)", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -30f), 18);
        oxygenLabel.alignment = TextAnchor.MiddleLeft;
        oxygenLabel.color = Color.white;
        oxygenLabel.rectTransform.sizeDelta = new Vector2(320f, 40f);
        timeLabel = CreateLabel(runHud, "남은 시간 : 40s", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-200f, -30f), 18);
        timeLabel.alignment = TextAnchor.MiddleRight;
        timeLabel.color = Color.white;
        timeLabel.rectTransform.sizeDelta = new Vector2(260f, 40f);

        // End panel
        CreateLabel(endPanel, "탐험 결과", new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f), Vector2.zero, 28);
        endReasonLabel = CreateLabel(endPanel, "사유 : -", new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.75f), Vector2.zero, 18);
        CreateLabel(endPanel, "획득한 광석", new Vector2(0.5f, 0.70f), new Vector2(0.5f, 0.70f), Vector2.zero, 18);
        oreResultLabel = CreateLabel(endPanel, "돌 : 0", new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), Vector2.zero, 20);
        CreateButton(endPanel, "다시 하기", new Vector2(0.25f, 0.15f), new Vector2(0.25f, 0.15f), new Vector2(0f, 0f), () => RestartRun());
        CreateButton(endPanel, "기지로 이동", new Vector2(0.75f, 0.15f), new Vector2(0.75f, 0.15f), new Vector2(0f, 0f), () => GoToUpgradeScene());

        // Forge panel
        // Forge UI (wireframe style)
        CreateButton(forgePanel, "대장간", new Vector2(0.12f, 0.95f), new Vector2(0.12f, 0.95f), new Vector2(0f, 0f), () => ShowForge());
        CreateButton(forgePanel, "스킬 트리", new Vector2(0.36f, 0.95f), new Vector2(0.36f, 0.95f), new Vector2(0f, 0f), () => ShowSkillTree());
        CreateButton(forgePanel, "탐험 시작", new Vector2(0.86f, 0.08f), new Vector2(0.86f, 0.08f), new Vector2(0f, 0f), () => GoToRunScene());
        moneyLabel = CreateMoneyBox(forgePanel, new Vector2(0.8f, 0.92f), "0 $");
        CreatePanelBox(forgePanel, new Vector2(0.78f, 0.54f), new Vector2(180f, 180f)); // right ore list panel
        CreateOreList(forgePanel, new Vector2(0.78f, 0.54f), out forgeStoneLabel, out forgeCopperLabel, out forgeStoneButton, out forgeCopperButton);
        CreatePanelBox(forgePanel, new Vector2(0.20f, 0.60f), new Vector2(180f, 180f)); // left odds panel
        CreateOddsList(forgePanel, new Vector2(0.20f, 0.60f));
        CreatePanelBox(forgePanel, new Vector2(0.48f, 0.55f), new Vector2(140f, 240f)); // center big box (character placeholder)
        forgeCountdownLabel = CreateLabel(forgePanel, "5.0s", new Vector2(0.48f, 0.70f), new Vector2(0.48f, 0.70f), Vector2.zero, 18);
        forgeGainLabel = CreateLabel(forgePanel, "+0$", new Vector2(0.60f, 0.70f), new Vector2(0.60f, 0.70f), Vector2.zero, 18, Color.green);
        forgeGainLabel.gameObject.SetActive(false);
        CreateButton(forgePanel, "재련하기", new Vector2(0.48f, 0.18f), new Vector2(0.48f, 0.18f), Vector2.zero, () => TryForge());

        // Skill panel
        CreateButton(skillPanel, "대장간", new Vector2(0.12f, 0.95f), new Vector2(0.12f, 0.95f), new Vector2(0f, 0f), () => ShowForge());
        CreateButton(skillPanel, "스킬 트리", new Vector2(0.36f, 0.95f), new Vector2(0.36f, 0.95f), new Vector2(0f, 0f), () => ShowSkillTree());
        CreateButton(skillPanel, "탐험 시작", new Vector2(0.86f, 0.08f), new Vector2(0.86f, 0.08f), new Vector2(0f, 0f), () => GoToRunScene());
        skillMoneyLabel = CreateMoneyBox(skillPanel, new Vector2(0.8f, 0.92f), "0 $");
        var tree = new GameObject("SkillTreeManager");
        tree.transform.SetParent(skillPanel.transform, false);
        tree.AddComponent<SkillTreeManager>().Init(skillPanel.transform);

        // Title panel (bottom-right buttons)
        CreateLabel(titlePanel, "타이틀", new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f), Vector2.zero, 28);
        CreateButton(titlePanel, "시작하기", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 140f), () => ShowSlotSelect(SlotMode.Save));
        CreateButton(titlePanel, "불러오기", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 90f), () => ShowSlotSelect(SlotMode.Load));
        CreateButton(titlePanel, "나가기", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 40f), () => QuitGame());

        // Slot panel
        CreateLabel(slotPanel, "슬롯 선택", new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), Vector2.zero, 22);
        CreateButton(slotPanel, "슬롯 1", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), () => SelectSlot(1));
        CreateButton(slotPanel, "슬롯 2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), () => SelectSlot(2));
        CreateButton(slotPanel, "슬롯 3", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), () => SelectSlot(3));
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

    Text CreateLabel(GameObject parent, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, int size, Color color)
    {
        var t = CreateLabel(parent, text, anchorMin, anchorMax, anchoredPos, size);
        t.color = color;
        return t;
    }

    void CreateButton(GameObject parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Button_" + label);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        var btn = go.AddComponent<Button>();
        if (onClick != null) btn.onClick.AddListener(onClick);

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

    GameObject CreatePanelBox(GameObject parent, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject("PanelBox");
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.25f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        return go;
    }

    Text CreateMoneyBox(GameObject parent, Vector2 anchor, string text)
    {
        var box = new GameObject("MoneyBox");
        box.transform.SetParent(parent.transform, false);
        var img = box.AddComponent<Image>();
        img.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        var rt = box.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(200f, 50f);
        return CreateLabel(box, text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 20, Color.black);
    }

    void CreateOddsList(GameObject parent, Vector2 anchor)
    {
        var go = new GameObject("OddsList");
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(200f, 180f);
        var t = CreateLabel(go, "0.1x : 10%\n0.5x : 20%\n1x : 40%\n2x : 20%\n10x : 10%", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 15);
        t.alignment = TextAnchor.MiddleCenter;
        t.rectTransform.sizeDelta = new Vector2(170f, 170f);
    }

    void CreateOreList(GameObject parent, Vector2 anchor, out Text stoneLabel, out Text copperLabel, out GameObject stoneBtn, out GameObject copperBtn)
    {
        var go = new GameObject("OreList");
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(180f, 180f);

        // Stone row
        var stoneRow = new GameObject("StoneRow");
        stoneRow.transform.SetParent(go.transform, false);
        var srt = stoneRow.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 0.65f);
        srt.anchorMax = new Vector2(0.5f, 0.65f);
        srt.anchoredPosition = new Vector2(30f, 0f);
        srt.sizeDelta = new Vector2(160f, 24f);

        stoneBtn = CreateColorButton(stoneRow, new Vector2(0f, 0.5f), new Color(0.1f, 0.1f, 0.1f), () => selectedOre = OreSelect.Stone, new Vector2(0f, 0f));
        stoneLabel = CreateLabel(stoneRow, "돌 : 0($1.0)", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), 16);
        stoneLabel.alignment = TextAnchor.MiddleLeft;
        stoneLabel.rectTransform.sizeDelta = new Vector2(150f, 24f);

        // Copper row
        var copperRow = new GameObject("CopperRow");
        copperRow.transform.SetParent(go.transform, false);
        var crt = copperRow.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.30f);
        crt.anchorMax = new Vector2(0.5f, 0.30f);
        crt.anchoredPosition = new Vector2(30f, 0f);
        crt.sizeDelta = new Vector2(160f, 24f);

        copperBtn = CreateColorButton(copperRow, new Vector2(0f, 0.5f), new Color(0.72f, 0.45f, 0.2f), () => selectedOre = OreSelect.Copper, new Vector2(0f, 0f));
        copperLabel = CreateLabel(copperRow, "구리 : 0($10.0)", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), 16);
        copperLabel.alignment = TextAnchor.MiddleLeft;
        copperLabel.rectTransform.sizeDelta = new Vector2(150f, 24f);
    }

    GameObject CreateColorButton(GameObject parent, Vector2 anchor, Color color, UnityEngine.Events.UnityAction onClick, Vector2 offset)
    {
        var go = new GameObject("OreButton");
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        if (onClick != null) btn.onClick.AddListener(onClick);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(18f, 18f);
        rt.pivot = new Vector2(0f, 0.5f);
        return go;
    }

    public void ShowRun()
    {
        CurrentPhase = GamePhase.Run;
        Time.timeScale = 1f;
        runHud.SetActive(true);
        endPanel.SetActive(false);
        forgePanel.SetActive(false);
        skillPanel.SetActive(false);
        titlePanel.SetActive(false);
        slotPanel.SetActive(false);
        endProcessed = false;
    }

    public void ShowEnd()
    {
        CurrentPhase = GamePhase.End;
        Time.timeScale = 0f;
        runHud.SetActive(true);
        endPanel.SetActive(true);
        forgePanel.SetActive(false);
        skillPanel.SetActive(false);
        titlePanel.SetActive(false);
        slotPanel.SetActive(false);
        SyncEndResults();
    }

    public void ShowForge()
    {
        CurrentPhase = GamePhase.Forge;
        Time.timeScale = 0f;
        runHud.SetActive(false);
        endPanel.SetActive(false);
        forgePanel.SetActive(true);
        skillPanel.SetActive(false);
        titlePanel.SetActive(false);
        slotPanel.SetActive(false);
        SyncForgeData();
    }

    public void ShowSkillTree()
    {
        CurrentPhase = GamePhase.SkillTree;
        Time.timeScale = 0f;
        runHud.SetActive(false);
        endPanel.SetActive(false);
        forgePanel.SetActive(false);
        skillPanel.SetActive(true);
        titlePanel.SetActive(false);
        slotPanel.SetActive(false);
    }

    public void ShowTitle()
    {
        CurrentPhase = GamePhase.Title;
        Time.timeScale = 0f;
        runHud.SetActive(false);
        endPanel.SetActive(false);
        forgePanel.SetActive(false);
        skillPanel.SetActive(false);
        titlePanel.SetActive(true);
        slotPanel.SetActive(false);
    }

    void ShowSlotSelect(SlotMode mode)
    {
        slotMode = mode;
        CurrentPhase = GamePhase.SlotSelect;
        titlePanel.SetActive(false);
        slotPanel.SetActive(true);
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
        if (waveLabel != null && waveManager != null && CurrentPhase == GamePhase.Run)
        {
            waveLabel.text = $"Wave : {waveManager.currentWave}";
        }
        if (timeLabel != null && waveManager != null && CurrentPhase == GamePhase.Run)
        {
            timeLabel.text = $"남은 시간 : {waveManager.RemainingWaveSeconds}s";
        }
        if (oreResultLabel != null && waveManager != null && (CurrentPhase == GamePhase.End))
        {
            if (SkillEffects.CopperUnlocked)
                oreResultLabel.text = $"돌 : +{runStoneGained} (총 {stone})\n구리 : +{runCopperGained} (총 {copper})";
            else
                oreResultLabel.text = $"돌 : +{runStoneGained} (총 {stone})";
        }
        if (CurrentPhase == GamePhase.SkillTree && skillMoneyLabel != null)
        {
            skillMoneyLabel.text = $"{money:0.0} $";
        }
        if (CurrentPhase == GamePhase.Forge && forgeCountdownLabel != null)
        {
            if (forgeReady)
                forgeCountdownLabel.text = "";
            else
                forgeCountdownLabel.text = $"{forgeCooldown:0.0}s";
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEventSystem();
        if (scene.name == "UpgradeScene")
        {
            // read persisted ore count if WaveManager isn't in this scene
            if (CurrentSlot >= 1) LoadSlot(CurrentSlot);
            ShowForge();
        }
        else if (scene.name == "TitleScene")
        {
            ShowTitle();
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
        SyncEndResults();
        SyncForgeData();
        SceneManager.LoadScene("UpgradeScene");
    }

    void GoToRunScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("RunScene");
    }

    void SyncForgeData()
    {
        float stoneValue = 1f * SkillEffects.ValueMultiplier;
        float copperValue = 10f * SkillEffects.ValueMultiplier;
        if (forgeStoneLabel != null) forgeStoneLabel.text = $"돌 : {stone}(${stoneValue:0.0})";
        if (forgeCopperLabel != null)
        {
            forgeCopperLabel.text = $"구리 : {copper}(${copperValue:0.0})";
            forgeCopperLabel.gameObject.SetActive(SkillEffects.CopperUnlocked);
        }
        if (forgeCopperButton != null) forgeCopperButton.SetActive(SkillEffects.CopperUnlocked);
        if (moneyLabel != null) moneyLabel.text = $"{money:0.0} $";
    }

    void TryForge()
    {
        if (!forgeReady) return;
        if (selectedOre == OreSelect.Stone && stone <= 0) return;
        if (selectedOre == OreSelect.Copper && copper <= 0) return;

        forgeReady = false;
        forgeCooldown = Mathf.Max(0.5f, 5.0f - SkillEffects.ForgeCooldownReduction);

        float multiplier = RollForgeMultiplier();
        float baseValue = (selectedOre == OreSelect.Stone ? 1f : 10f) * SkillEffects.ValueMultiplier;
        float gain = baseValue * multiplier;
        money += gain;

        if (forgeGainLabel != null)
        {
            forgeGainLabel.text = $"+{gain:0.0}$";
            forgeGainLabel.gameObject.SetActive(true);
            StartCoroutine(FloatingGain());
        }

        if (selectedOre == OreSelect.Stone)
        {
            stone -= 1;
            if (stone < 0) stone = 0;
        }
        else
        {
            copper -= 1;
            if (copper < 0) copper = 0;
        }
        float stoneValue = 1f * SkillEffects.ValueMultiplier;
        float copperValue = 10f * SkillEffects.ValueMultiplier;
        if (forgeStoneLabel != null) forgeStoneLabel.text = $"돌 : {stone}(${stoneValue:0.0})";
        if (forgeCopperLabel != null)
        {
            forgeCopperLabel.text = $"구리 : {copper}(${copperValue:0.0})";
            forgeCopperLabel.gameObject.SetActive(SkillEffects.CopperUnlocked);
        }
        if (forgeCopperButton != null) forgeCopperButton.SetActive(SkillEffects.CopperUnlocked);
        if (moneyLabel != null) moneyLabel.text = $"{money:0.0} $";
        // no persistence during testing

        StartCoroutine(ForgeCooldown());
    }

    public float GetMoney()
    {
        return money;
    }

    public bool SpendMoney(float amount)
    {
        if (money + 0.0001f < amount) return false;
        money -= amount;
        if (money < 0f) money = 0f;
        if (moneyLabel != null) moneyLabel.text = $"{money:0.0} $";
        return true;
    }

    public void AddMoney(float amount)
    {
        if (amount <= 0f) return;
        money += amount;
        if (moneyLabel != null) moneyLabel.text = $"{money:0.0} $";
    }

    void SyncEndResults()
    {
        if (endProcessed) return;
        if (waveManager == null) waveManager = FindObjectOfType<WaveManager>();
        runStoneGained = waveManager != null ? waveManager.oresCollectedStone : 0;
        runCopperGained = waveManager != null ? waveManager.oresCollectedCopper : 0;
        stone += runStoneGained;
        copper += runCopperGained;
        endProcessed = true;
        if (oreResultLabel != null)
        {
            if (SkillEffects.CopperUnlocked)
                oreResultLabel.text = $"돌 : +{runStoneGained} (총 {stone})\n구리 : +{runCopperGained} (총 {copper})";
            else
                oreResultLabel.text = $"돌 : +{runStoneGained} (총 {stone})";
        }
        if (CurrentSlot >= 1) SaveSlot(CurrentSlot);
    }

    void SelectSlot(int slot)
    {
        CurrentSlot = slot;
        if (slotMode == SlotMode.Save)
        {
            ResetProgress();
            SaveSlot(slot);
            SceneManager.LoadScene("UpgradeScene");
        }
        else
        {
            LoadSlot(slot);
            SceneManager.LoadScene("UpgradeScene");
        }
    }

    void ResetProgress()
    {
        money = 0f;
        stone = 0;
        copper = 0;
        SkillEffects.SetDamageLevel(0);
        SkillEffects.SetFireRateLevel(0);
        SkillEffects.SetOxygenOnKillLevel(0);
        SkillEffects.SetMaxOxygenLevel(0);
        SkillEffects.SetOxygenDecayLevel(0);
        SkillEffects.SetForgeCooldownLevel(0);
        SkillEffects.SetValueLevel(0);
        SkillEffects.SetCopperLevel(0);
        SkillTreeManager.ResetAllSkills();
    }

    void SaveSlot(int slot)
    {
        PlayerPrefs.SetFloat($"slot_{slot}_money", money);
        PlayerPrefs.SetInt($"slot_{slot}_stone", stone);
        PlayerPrefs.SetInt($"slot_{slot}_copper", copper);
        SkillTreeManager.SaveSkills(slot);
        PlayerPrefs.Save();
    }

    void LoadSlot(int slot)
    {
        money = PlayerPrefs.GetFloat($"slot_{slot}_money", 0f);
        stone = PlayerPrefs.GetInt($"slot_{slot}_stone", 0);
        copper = PlayerPrefs.GetInt($"slot_{slot}_copper", 0);
        SkillTreeManager.LoadSkills(slot);
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("Quit");
#else
        Application.Quit();
#endif
    }

    public void SetEndReason(string reason)
    {
        if (endReasonLabel != null)
            endReasonLabel.text = $"사유 : {reason}";
    }

    float RollForgeMultiplier()
    {
        float r = Random.Range(0f, 1f);
        if (r < 0.10f) return 0.1f;
        if (r < 0.30f) return 0.5f;
        if (r < 0.70f) return 1f;
        if (r < 0.90f) return 2f;
        return 10f;
    }

    System.Collections.IEnumerator ForgeCooldown()
    {
        while (forgeCooldown > 0f)
        {
            forgeCooldown -= 0.1f;
            if (forgeCooldown < 0f) forgeCooldown = 0f;
            yield return new WaitForSecondsRealtime(0.1f);
        }
        forgeReady = true;
    }

    System.Collections.IEnumerator FloatingGain()
    {
        float t = 0f;
        Vector3 start = forgeGainLabel.rectTransform.anchoredPosition;
        Vector3 end = start + new Vector3(0f, 40f, 0f);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 1.2f;
            forgeGainLabel.rectTransform.anchoredPosition = Vector3.Lerp(start, end, t);
            var c = forgeGainLabel.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            forgeGainLabel.color = c;
            yield return null;
        }
        var reset = forgeGainLabel.color;
        reset.a = 1f;
        forgeGainLabel.color = reset;
        forgeGainLabel.rectTransform.anchoredPosition = start;
        forgeGainLabel.gameObject.SetActive(false);
    }
}
