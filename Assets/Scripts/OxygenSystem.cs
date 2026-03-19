using UnityEngine;
using UnityEngine.UI;

public class OxygenSystem : MonoBehaviour
{
    public int startOxygen = 20;
    public int currentOxygen { get; private set; }
    public int MaxOxygen => maxOxygen;
    public float oxygenDecreaseInterval = 1f;
    private float timer;
    public WaveManager waveManager;
    // UI
    public Image oxygenFill; // assign Image with Fill method
    public Text oxygenText; // optional numeric text
    private int maxOxygen;

    void Start()
    {
        currentOxygen = startOxygen;
        timer = 0f;
        maxOxygen = startOxygen;
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
            ChangeOxygen(-1);
        }
    }

    public void ChangeOxygen(int amount)
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
            oxygenFill.fillAmount = (float)currentOxygen / (float)maxOxygen;
        }
        if (oxygenText != null)
        {
            oxygenText.text = $"O2: {currentOxygen}/{maxOxygen}";
        }
    }
}
