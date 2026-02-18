using UnityEngine;

public class OxygenSystem : MonoBehaviour
{
    public int startOxygen = 20;
    public int currentOxygen { get; private set; }
    public float oxygenDecreaseInterval = 1f;
    private float timer;
    public WaveManager waveManager;

    void Start()
    {
        currentOxygen = startOxygen;
        timer = 0f;
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
        if (currentOxygen <= 0)
        {
            currentOxygen = 0;
            waveManager.EndRun();
        }
        // TODO: UI 갱신
    }
}
