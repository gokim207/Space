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
    private Image forgeStoneIconImage;
    private Image forgeCopperIconImage;
    private Text forgeCountdownLabel;
    private Text forgeGainLabel;
    private Text oddsLabel;
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
    private Text slotStatusLabel;
    private Coroutine slotStatusCo;
    private GameObject confirmPanel;
    private int pendingDeleteSlot = -1;
    private readonly string[] skillIds = new[]
    {
        "atk","value","copper","forge","anvil","firerate","oxygenkill","oxygenmax","oxygendecay"
    };
    private Button[] slotButtons = new Button[3];
    private Text[] slotTexts = new Text[3];
    private float playtimeSeconds = 0f;
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
        EnsureTestSlotMoney();
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
        CreateOreList(forgePanel, new Vector2(0.92f, 0.49f), out forgeStoneLabel, out forgeCopperLabel, out forgeStoneButton, out forgeCopperButton);
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
        CreateLabel(slotPanel, "슬롯 선택", new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), Vector2.zero, 22);
        slotStatusLabel = CreateLabel(slotPanel, "", new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, 20);
        CreateButton(slotPanel, "뒤로", new Vector2(0.1f, 0.9f), new Vector2(0.1f, 0.9f), new Vector2(0f, 0f), () => ShowTitle());
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var btnGo = CreateButton(slotPanel, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 130f - (i * 120f)), () => SelectSlot(idx + 1));
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(320f, 80f);
            var b = btnGo.GetComponent<Button>();
            slotButtons[i] = b;
            var info = CreateLabel(btnGo, "파일\n돈: 0$\n플레이타임: 00:00", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 20, Color.black);
            info.alignment = TextAnchor.MiddleCenter;
            info.rectTransform.sizeDelta = new Vector2(300f, 120f);
            slotTexts[i] = info;

            var delBtn = CreateButton(btnGo, "삭제", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), () => AskDelete(idx + 1));
            var delRt = delBtn.GetComponent<RectTransform>();
            delRt.sizeDelta = new Vector2(60f, 30f);
        }

        // Confirm popup
        confirmPanel = CreatePanel("ConfirmPanel", new Color(0f, 0f, 0f, 0.55f));
        confirmPanel.SetActive(false);
        CreateLabel(confirmPanel, "정말 삭제할까요?", new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), Vector2.zero, 22);
        CreateButton(confirmPanel, "삭제", new Vector2(0.45f, 0.45f), new Vector2(0.45f, 0.45f), new Vector2(-60f, -20f), () => ConfirmDelete(true));
        CreateButton(confirmPanel, "취소", new Vector2(0.55f, 0.45f), new Vector2(0.55f, 0.45f), new Vector2(60f, -20f), () => ConfirmDelete(false));
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

    GameObject CreateButton(GameObject parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
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
        return go;
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
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(200f, 180f);
        oddsLabel = CreateLabel(go, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 15);
        oddsLabel.alignment = TextAnchor.MiddleCenter;
        oddsLabel.rectTransform.sizeDelta = new Vector2(170f, 170f);
        UpdateOddsLabel();
    }

    void CreateOreList(GameObject parent, Vector2 anchor, out Text stoneLabel, out Text copperLabel, out GameObject stoneBtn, out GameObject copperBtn)
    {
        var go = new GameObject("OreList");
        go.transform.SetParent(parent.transform, false);
        go.transform.SetAsLastSibling();
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(240f, 220f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        float rowGap = 36f;

        // Stone row
        var stoneRow = new GameObject("StoneRow");
        stoneRow.transform.SetParent(go.transform, false);
        var srt = stoneRow.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(0f, 1f);
        srt.pivot = new Vector2(0f, 1f);
        srt.anchoredPosition = new Vector2(20f, -20f);
        srt.sizeDelta = new Vector2(200f, 28f);

        stoneBtn = CreateColorButton(stoneRow, new Vector2(0f, 0.5f), new Color(0.1f, 0.1f, 0.1f), () => { Debug.Log("Stone icon clicked"); SetSelectedOre(OreSelect.Stone); }, new Vector2(-90f, 0f));
        forgeStoneIconImage = stoneBtn.GetComponent<Image>();
        // click the icon only
        stoneLabel = CreateLabel(stoneRow, "돌 : 0($1.0)", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), 14);
        stoneLabel.alignment = TextAnchor.MiddleLeft;
        stoneLabel.rectTransform.sizeDelta = new Vector2(170f, 28f);
        stoneLabel.raycastTarget = false;

        // Copper row
        var copperRow = new GameObject("CopperRow");
        copperRow.transform.SetParent(go.transform, false);
        var crt = copperRow.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(0f, 1f);
        crt.pivot = new Vector2(0f, 1f);
        crt.anchoredPosition = new Vector2(20f, -20f - rowGap);
        crt.sizeDelta = new Vector2(200f, 28f);

        copperBtn = CreateColorButton(copperRow, new Vector2(0f, 0.5f), new Color(0.72f, 0.45f, 0.2f), () => { Debug.Log("Copper icon clicked"); SetSelectedOre(OreSelect.Copper); }, new Vector2(-90f, 0f));
        forgeCopperIconImage = copperBtn.GetComponent<Image>();
        // click the icon only
        copperLabel = CreateLabel(copperRow, "구리 : 0($10.0)", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), 14);
        copperLabel.alignment = TextAnchor.MiddleLeft;
        copperLabel.rectTransform.sizeDelta = new Vector2(170f, 28f);
        copperLabel.raycastTarget = false;
    }

    GameObject CreateColorButton(GameObject parent, Vector2 anchor, Color color, UnityEngine.Events.UnityAction onClick, Vector2 offset)
    {
        var go = new GameObject("OreButton");
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        if (onClick != null) btn.onClick.AddListener(onClick);
        btn.onClick.AddListener(() => StartCoroutine(FlashImage(img, color)));
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(24f, 24f);
        rt.pivot = new Vector2(0f, 0.5f);
        return go;
    }

    // (row buttons removed: use icon buttons only)

    System.Collections.IEnumerator FlashImage(Image img, Color baseColor)
    {
        if (img == null) yield break;
        Color bright = new Color(
            Mathf.Clamp01(baseColor.r + 0.25f),
            Mathf.Clamp01(baseColor.g + 0.25f),
            Mathf.Clamp01(baseColor.b + 0.25f),
            baseColor.a
        );
        img.color = bright;
        yield return new WaitForSecondsRealtime(0.08f);
        img.color = baseColor;
    }

    void SetSelectedOre(OreSelect ore)
    {
        selectedOre = ore;
        if (ore == OreSelect.Stone && forgeStoneIconImage != null)
            StartCoroutine(FlashImage(forgeStoneIconImage, forgeStoneIconImage.color));
        if (ore == OreSelect.Copper && forgeCopperIconImage != null)
            StartCoroutine(FlashImage(forgeCopperIconImage, forgeCopperIconImage.color));
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
        if (confirmPanel != null) confirmPanel.SetActive(false);
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
        if (confirmPanel != null) confirmPanel.SetActive(false);
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
        if (confirmPanel != null) confirmPanel.SetActive(false);
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
        if (confirmPanel != null) confirmPanel.SetActive(false);
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
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    void ShowSlotSelect(SlotMode mode)
    {
        slotMode = mode;
        CurrentPhase = GamePhase.SlotSelect;
        titlePanel.SetActive(false);
        slotPanel.SetActive(true);
        if (confirmPanel != null) confirmPanel.SetActive(false);
        RefreshSlotUI();
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
            if (IsMineralUnlocked("copper"))
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
        if (CurrentPhase == GamePhase.Forge)
        {
            UpdateOddsLabel();
        }

        // playtime tracking (unscaled)
        if (CurrentPhase == GamePhase.Run || CurrentPhase == GamePhase.Forge || CurrentPhase == GamePhase.SkillTree)
        {
            playtimeSeconds += Time.unscaledDeltaTime;
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

    void EnsureTestSlotMoney()
    {
        if (PlayerPrefs.GetInt("slot_1_exists", 0) != 1) return;
        if (PlayerPrefs.GetInt("slot_1_boosted2", 0) == 1) return;
        float m = PlayerPrefs.GetFloat("slot_1_money", 0f);
        m += 10000f;
        PlayerPrefs.SetFloat("slot_1_money", m);
        PlayerPrefs.SetInt("slot_1_boosted2", 1);
        PlayerPrefs.Save();
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
        if (CurrentSlot >= 1) SaveSlot(CurrentSlot);
        SceneManager.LoadScene("RunScene");
    }

    void SyncForgeData()
    {
        float stoneValue = GetMineralValue("stone", 1f) * SkillEffects.ValueMultiplier;
        float copperValue = GetMineralValue("copper", 10f) * SkillEffects.ValueMultiplier;
        if (forgeStoneLabel != null) forgeStoneLabel.text = $"돌 : {stone}(${stoneValue:0.0})";
        if (forgeCopperLabel != null)
        {
            forgeCopperLabel.text = $"구리 : {copper}(${copperValue:0.0})";
            forgeCopperLabel.gameObject.SetActive(IsMineralUnlocked("copper"));
        }
        if (forgeCopperButton != null) forgeCopperButton.SetActive(IsMineralUnlocked("copper"));
        if (moneyLabel != null) moneyLabel.text = $"{money:0.0} $";
        UpdateOddsLabel();
    }

    void TryForge()
    {
        if (!forgeReady) return;
        if (selectedOre == OreSelect.Stone && stone <= 0) return;
        if (selectedOre == OreSelect.Copper && copper <= 0) return;

        forgeReady = false;
        forgeCooldown = Mathf.Max(0.5f, 5.0f - SkillEffects.ForgeCooldownReduction);

        float multiplier = RollForgeMultiplier();
        string oreId = selectedOre == OreSelect.Stone ? "stone" : "copper";
        float baseValue = GetMineralValue(oreId, selectedOre == OreSelect.Stone ? 1f : 10f) * SkillEffects.ValueMultiplier;
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
        float stoneValue = GetMineralValue("stone", 1f) * SkillEffects.ValueMultiplier;
        float copperValue = GetMineralValue("copper", 10f) * SkillEffects.ValueMultiplier;
        if (forgeStoneLabel != null) forgeStoneLabel.text = $"돌 : {stone}(${stoneValue:0.0})";
        if (forgeCopperLabel != null)
        {
            forgeCopperLabel.text = $"구리 : {copper}(${copperValue:0.0})";
            forgeCopperLabel.gameObject.SetActive(IsMineralUnlocked("copper"));
        }
        if (forgeCopperButton != null) forgeCopperButton.SetActive(IsMineralUnlocked("copper"));
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
            if (IsMineralUnlocked("copper"))
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
            if (HasSlot(slot))
            {
                ShowSlotStatus("이미 저장된 슬롯입니다.\n불러오기를 사용하세요.", slotButtons[slot - 1]?.GetComponent<RectTransform>());
                return;
            }
            ResetProgress();
            SaveSlot(slot);
            SceneManager.LoadScene("UpgradeScene");
        }
        else
        {
            if (!HasSlot(slot))
            {
                RectTransform rt = null;
                if (slotButtons[slot - 1] != null)
                    rt = slotButtons[slot - 1].GetComponent<RectTransform>();
                ShowSlotStatus("빈 슬롯입니다.", rt);
                return;
            }
            LoadSlot(slot);
            SceneManager.LoadScene("UpgradeScene");
        }
    }

    void ResetProgress()
    {
        money = 0f;
        stone = 0;
        copper = 0;
        playtimeSeconds = 0f;
        SkillTreeManager.ResetAllSkills();
    }

    void SaveSlot(int slot)
    {
        PlayerPrefs.SetFloat($"slot_{slot}_money", money);
        PlayerPrefs.SetInt($"slot_{slot}_stone", stone);
        PlayerPrefs.SetInt($"slot_{slot}_copper", copper);
        PlayerPrefs.SetFloat($"slot_{slot}_time", playtimeSeconds);
        PlayerPrefs.SetInt($"slot_{slot}_exists", 1);
        SkillTreeManager.SaveSkills(slot);
        PlayerPrefs.Save();
    }

    void LoadSlot(int slot)
    {
        money = PlayerPrefs.GetFloat($"slot_{slot}_money", 0f);
        stone = PlayerPrefs.GetInt($"slot_{slot}_stone", 0);
        copper = PlayerPrefs.GetInt($"slot_{slot}_copper", 0);
        playtimeSeconds = PlayerPrefs.GetFloat($"slot_{slot}_time", 0f);
        SkillTreeManager.LoadSkills(slot);
    }

    void DeleteSlot(int slot)
    {
        if (!HasSlot(slot))
        {
            ShowSlotStatus("빈 슬롯입니다.", slotButtons[slot - 1]?.GetComponent<RectTransform>());
            return;
        }
        PlayerPrefs.DeleteKey($"slot_{slot}_money");
        PlayerPrefs.DeleteKey($"slot_{slot}_stone");
        PlayerPrefs.DeleteKey($"slot_{slot}_copper");
        PlayerPrefs.DeleteKey($"slot_{slot}_time");
        PlayerPrefs.DeleteKey($"slot_{slot}_exists");
        var ids = SkillTreeManager.GetSkillIdsFromCsv();
        if (ids.Count == 0)
        {
            for (int i = 0; i < skillIds.Length; i++)
                PlayerPrefs.DeleteKey($"slot_{slot}_skill_{skillIds[i]}");
        }
        else
        {
            for (int i = 0; i < ids.Count; i++)
                PlayerPrefs.DeleteKey($"slot_{slot}_skill_{ids[i]}");
        }
        PlayerPrefs.Save();
        RefreshSlotUI();
        ShowSlotStatus("삭제 완료", slotButtons[slot - 1]?.GetComponent<RectTransform>());
    }

    void AskDelete(int slot)
    {
        pendingDeleteSlot = slot;
        if (confirmPanel != null) confirmPanel.SetActive(true);
    }

    void ConfirmDelete(bool ok)
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (!ok)
        {
            pendingDeleteSlot = -1;
            return;
        }
        if (pendingDeleteSlot >= 1)
            DeleteSlot(pendingDeleteSlot);
        pendingDeleteSlot = -1;
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    bool HasSlot(int slot)
    {
        return PlayerPrefs.GetInt($"slot_{slot}_exists", 0) == 1;
    }

    void RefreshSlotUI()
    {
        for (int i = 0; i < 3; i++)
        {
            int slot = i + 1;
            bool exists = HasSlot(slot);
            float m = PlayerPrefs.GetFloat($"slot_{slot}_money", 0f);
            float t = PlayerPrefs.GetFloat($"slot_{slot}_time", 0f);
            string time = FormatTime(t);
            if (slotTexts[i] != null)
            {
                slotTexts[i].text = exists
                    ? $"파일 {slot}\n돈: {m:0.0}$\n플레이타임: {time}"
                    : $"파일 {slot}\n(빈 슬롯)";
            }
        }
        if (slotStatusLabel != null) slotStatusLabel.text = "";
    }

    string FormatTime(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        int m = s / 60;
        int r = s % 60;
        return $"{m:00}:{r:00}";
    }

    public void SetEndReason(string reason)
    {
        if (endReasonLabel != null)
            endReasonLabel.text = $"사유 : {reason}";
    }

    float RollForgeMultiplier()
    {
        var entries = GameData.GetForgeEntries();
        if (entries == null || entries.Count == 0)
        {
            float r = Random.Range(0f, 1f);
            float adj = 0.05f * SkillEffects.ForgeStabilityLevel;
            float p05 = Mathf.Max(0.05f, 0.20f - adj);
            float p1 = 0.45f;
            float p2 = Mathf.Min(0.35f, 0.20f + adj);
            float p5 = 0.10f;
            float p10 = 0.05f;
            float c1 = p05;
            float c2 = c1 + p1;
            float c3 = c2 + p2;
            float c4 = c3 + p5;
            if (r < c1) return 0.5f;
            if (r < c2) return 1f;
            if (r < c3) return 2f;
            if (r < c4) return 5f;
            return 10f;
        }

        int total = 0;
        foreach (var e in entries) total += GetAdjustedForgeWeight(e);
        if (total <= 0) return 1f;
        int pick = Random.Range(0, total);
        int acc = 0;
        foreach (var e in entries)
        {
            acc += GetAdjustedForgeWeight(e);
            if (pick < acc) return e.multiplier;
        }
        return entries[0].multiplier;
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

    void UpdateOddsLabel()
    {
        if (oddsLabel == null) return;
        var entries = GameData.GetForgeEntries();
        if (entries == null || entries.Count == 0)
        {
            float adj = 0.05f * SkillEffects.ForgeStabilityLevel;
            float p05 = Mathf.Max(0.05f, 0.20f - adj);
            float p1 = 0.45f;
            float p2 = Mathf.Min(0.35f, 0.20f + adj);
            float p5 = 0.10f;
            float p10 = 0.05f;
            oddsLabel.text = $"0.5x : {p05 * 100f:0}%\n1x : {p1 * 100f:0}%\n2x : {p2 * 100f:0}%\n5x : {p5 * 100f:0}%\n10x : {p10 * 100f:0}%";
            return;
        }
        int total = 0;
        foreach (var e in entries) total += GetAdjustedForgeWeight(e);
        if (total <= 0) return;
        var lines = new System.Text.StringBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            float pct = (float)GetAdjustedForgeWeight(e) / total * 100f;
            string mult = e.multiplier.ToString("0.##");
            lines.Append($"{mult}x : {pct:0}%");
            if (i < entries.Count - 1) lines.Append("\n");
        }
        oddsLabel.text = lines.ToString();
    }

    float GetMineralValue(string id, float fallback)
    {
        return GameData.GetMineralValue(id, fallback);
    }

    bool IsMineralUnlocked(string id)
    {
        var m = GameData.GetMineral(id);
        if (m == null) return false;
        return GameData.IsSkillUnlocked(m.reqSkill);
    }

    int GetAdjustedForgeWeight(GameData.ForgeEntry entry)
    {
        if (entry == null) return 0;
        int w = entry.baseWeight;
        int delta = SkillEffects.ForgeStabilityLevel * 5;
        if (entry.id == "x0_5") w = Mathf.Max(5, w - delta);
        if (entry.id == "x2") w = Mathf.Min(35, w + delta);
        return Mathf.Max(0, w);
    }

    void ShowSlotStatus(string message, RectTransform source)
    {
        if (slotStatusLabel == null) return;
        slotStatusLabel.text = message;
        slotStatusLabel.gameObject.SetActive(true);
        if (source != null)
            slotStatusLabel.rectTransform.anchoredPosition = source.anchoredPosition + new Vector2(0f, -10f);
        if (slotStatusCo != null) StopCoroutine(slotStatusCo);
        slotStatusCo = StartCoroutine(SlotStatusFloat());
    }

    System.Collections.IEnumerator SlotStatusFloat()
    {
        float t = 0f;
        Vector3 start = slotStatusLabel.rectTransform.anchoredPosition;
        Vector3 end = start + new Vector3(0f, 30f, 0f);
        var c = slotStatusLabel.color;
        c.a = 1f;
        slotStatusLabel.color = c;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 1.0f;
            slotStatusLabel.rectTransform.anchoredPosition = Vector3.Lerp(start, end, t);
            c.a = Mathf.Lerp(1f, 0f, t);
            slotStatusLabel.color = c;
            yield return null;
        }
        slotStatusLabel.rectTransform.anchoredPosition = start;
        c.a = 1f;
        slotStatusLabel.color = c;
        slotStatusLabel.text = "";
        slotStatusLabel.gameObject.SetActive(false);
    }
}
