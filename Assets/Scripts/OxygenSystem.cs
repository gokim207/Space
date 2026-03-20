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
    // UI
    public Image oxygenFill; // assign Image with Fill method
    public Text oxygenText; // optional numeric text
    private float maxOxygen;

    void Awake()
    {
        currentOxygen = startOxygen + SkillEffects.MaxOxygenBonus;
        timer = 0f;
        maxOxygen = startOxygen + SkillEffects.MaxOxygenBonus;
    }

    void Start()
    {
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
            float dec = Random.Range(0.8f, 1.5f) * SkillEffects.OxygenDecayMultiplier;
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
            waveManager.EndRun();
        }
        RefreshUI();
        // TODO: UI 갱신
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
