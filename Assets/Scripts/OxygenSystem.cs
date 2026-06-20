using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class OxygenSystem : MonoBehaviour
{
    public float startOxygen = 30f;
    public float currentOxygen { get; private set; }
    public float MaxOxygen => maxOxygen;
    public float oxygenDecreaseInterval = 1f;
    private float timer;
    public WaveManager waveManager;
    public string oxygenId = "default";
    public float killReward = 0f;
    public float decayMin = 0.8f;
    public float decayMax = 1.5f;
    // UI
    public Image oxygenFill; // assign Image with Fill method
    public Text oxygenText; // optional numeric text
    public TMP_Text oxygenTextTMP;
    private float maxOxygen;
    private bool reviveUsed;

    static readonly string[] OxygenFillNames =
    {
        "hpBar", "HPBar", "hpFill", "HPFill",
        "oxygenBar", "OxygenBar", "oxygenFill", "OxygenFill"
    };

    void Awake()
    {
        ApplyOxygenConfig();
        maxOxygen = startOxygen + SkillEffects.MaxOxygenBonus;
        currentOxygen = maxOxygen;
        reviveUsed = false;
        timer = 0f;
    }

    void Start()
    {
        if (waveManager == null)
            waveManager = FindObjectOfType<WaveManager>();
        RebindUI();
        RefreshUI();
    }

    void Update()
    {
        if (waveManager == null || waveManager.CurrentState != WaveManager.GameState.Run)
            return;
        if (BossBattleSession.IsCombatPaused)
            return;

        timer += Time.deltaTime;
        if (timer >= oxygenDecreaseInterval)
        {
            timer = 0f;
            float dec = Random.Range(decayMin, decayMax) *
                SkillEffects.OxygenDecayMultiplier *
                BossBattleSession.OxygenDecayMultiplier;
            ChangeOxygen(-dec);
        }
    }

    public void ChangeOxygen(float amount)
    {
        currentOxygen += amount;
        if (currentOxygen > maxOxygen) currentOxygen = maxOxygen;
        if (currentOxygen <= 0)
        {
            if (!reviveUsed && SkillEffects.ReviveOxygenPercent > 0f)
            {
                reviveUsed = true;
                currentOxygen = Mathf.Max(0.1f, maxOxygen * Mathf.Clamp01(SkillEffects.ReviveOxygenPercent));
                RefreshUI();
                return;
            }

            currentOxygen = 0;
            var flow = GameFlowManager.Instance;
            if (flow != null) flow.SetEndReason("산소 부족");
            if (waveManager != null)
                waveManager.EndRun();
        }
        RefreshUI();
        // TODO: UI 갱신
    }

    void ApplyOxygenConfig()
    {
        var def = GameData.GetOxygen(oxygenId);
        if (def == null) return;
        if (def.startOxygen > 0f) startOxygen = def.startOxygen;
        // maxOxygen in the table is reserved for future modes; the active player starts full at startOxygen.
        if (def.decreaseInterval > 0f) oxygenDecreaseInterval = def.decreaseInterval;
        if (def.decayMin > 0f) decayMin = def.decayMin;
        if (def.decayMax > 0f) decayMax = def.decayMax;
        killReward = def.killReward;
    }

    void RefreshUI()
    {
        if (oxygenFill != null && maxOxygen > 0)
        {
            float ratio = Mathf.Clamp01(currentOxygen / maxOxygen);
            oxygenFill.fillAmount = ratio;
        }
        if (oxygenText != null)
        {
            oxygenText.text = $"남은 산소 ({currentOxygen:0.0} / {maxOxygen:0.0})";
        }
        if (oxygenTextTMP != null)
            oxygenTextTMP.text = $"남은 산소 ({currentOxygen:0.0} / {maxOxygen:0.0})";
    }

    public void RebindUI()
    {
        string panelName = BossBattleSession.IsBossBattle ? "bossPanel" : "runPanel";
        Transform panel = FindInActiveScene(panelName);

        oxygenFill = panel != null
            ? FindImageByNames(panel, OxygenFillNames)
            : FindImageByNames(OxygenFillNames);

        Transform textTransform = panel != null ? FindChild(panel, "oxygenText") : null;
        oxygenTextTMP = textTransform != null ? textTransform.GetComponent<TMP_Text>() : null;
        oxygenText = textTransform != null ? textTransform.GetComponent<Text>() : null;

        if (oxygenFill == null) return;

        oxygenFill.type = Image.Type.Filled;
        oxygenFill.fillMethod = Image.FillMethod.Horizontal;
        oxygenFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        RefreshUI();
    }

    Image FindImageByNames(string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            var go = GameObject.Find(names[i]);
            if (go == null) continue;
            var image = go.GetComponent<Image>();
            if (image != null) return image;
        }
        return null;
    }

    static Image FindImageByNames(Transform root, string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindChild(root, names[i]);
            if (found == null) continue;
            Image image = found.GetComponent<Image>();
            if (image != null) return image;
        }
        return null;
    }

    static Transform FindInActiveScene(string targetName)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChild(roots[i].transform, targetName);
            if (found != null)
                return found;
        }
        return null;
    }

    static Transform FindChild(Transform root, string targetName)
    {
        if (root == null)
            return null;
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
}
