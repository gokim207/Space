using UnityEngine;
using UnityEngine.UI;

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
    private float maxOxygen;

    void Awake()
    {
        ApplyOxygenConfig();
        currentOxygen = startOxygen + SkillEffects.MaxOxygenBonus;
        timer = 0f;
        maxOxygen = (maxOxygen > 0f ? maxOxygen : startOxygen) + SkillEffects.MaxOxygenBonus;
    }

    void Start()
    {
        if (waveManager == null)
            waveManager = FindObjectOfType<WaveManager>();
        RefreshUI();
    }

    void Update()
    {
        if (waveManager == null || waveManager.CurrentState != WaveManager.GameState.Run)
            return;

        timer += Time.deltaTime;
        if (timer >= oxygenDecreaseInterval)
        {
            timer = 0f;
            float dec = Random.Range(decayMin, decayMax) * SkillEffects.OxygenDecayMultiplier;
            ChangeOxygen(-dec);
        }
    }

    public void ChangeOxygen(float amount)
    {
        currentOxygen += amount;
        if (currentOxygen > maxOxygen) currentOxygen = maxOxygen;
        if (currentOxygen <= 0)
        {
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
        if (def.maxOxygen > 0f) maxOxygen = def.maxOxygen;
        if (def.decreaseInterval > 0f) oxygenDecreaseInterval = def.decreaseInterval;
        if (def.decayMin > 0f) decayMin = def.decayMin;
        if (def.decayMax > 0f) decayMax = def.decayMax;
        killReward = def.killReward;
    }

    void RefreshUI()
    {
        if (oxygenFill != null && maxOxygen > 0)
        {
            oxygenFill.fillAmount = currentOxygen / maxOxygen;
        }
        if (oxygenText != null)
        {
            oxygenText.text = $"O2: {currentOxygen:0.0}/{maxOxygen:0.0}";
        }
    }
}
