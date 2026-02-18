using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public enum GameState { Run, Upgrade }
    public GameState CurrentState { get; private set; } = GameState.Run;

    public GameObject projectilePrefab;
    public Transform firePoint;
    public float fireInterval = 0.5f;
    private float fireTimer;

    void Start()
    {
        StartGame();
    }

    void Update()
    {
        if (CurrentState != GameState.Run)
            return;
        fireTimer += Time.deltaTime;
        if (fireTimer >= fireInterval)
        {
            fireTimer = 0f;
            AutoFire();
        }
    }

    public void StartGame()
    {
        CurrentState = GameState.Run;
        fireTimer = 0f;
        // TODO: 웨이브, 산소 등 초기화
        Debug.Log("게임 시작!");
    }

    public void EndRun()
    {
        CurrentState = GameState.Upgrade;
        // TODO: 업그레이드 씬 전환 등 처리
        Debug.Log("런 종료, 업그레이드로 전환");
    }

    void AutoFire()
    {
        if (projectilePrefab != null && firePoint != null)
        {
            Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        }
    }
}
