using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WaveManager : MonoBehaviour
{
    public enum GameState { Run, Upgrade }
    public GameState CurrentState { get; private set; } = GameState.Run;

    public GameObject projectilePrefab;
    public Transform firePoint;
    public float fireInterval = 1.5f;
    private float baseFireInterval = 1f;
    private float fireTimer;
    public float fireRange = 30f; // targeting range
    public bool requireTargetInView = true;
    public float viewportMargin = 0.02f; // allow slight margin outside [0,1]
    // Wave system
    public int currentWave = 1;
    public float waveDuration = 40f;
    private float waveTimer = 0f;
    public EnemySpawner spawner;
    public OxygenSystem oxygenSystem;
    public int oresCollectedThisRun = 0;
    public int oresCollectedStone = 0;
    public int oresCollectedCopper = 0;
    public Dictionary<string, int> oresCollectedById = new Dictionary<string, int>();
    private bool endSequenceStarted = false;
    // UI
    public Text waveText;
    public Text waveTimerText;
    public Text oreText;
    public string weaponId = "starter_gun";

    public int RemainingWaveSeconds
    {
        get { return Mathf.Max(0, Mathf.CeilToInt(waveDuration - waveTimer)); }
    }

    void Start()
    {
        StartGame();
    }

    void Update()
    {
        if (CurrentState != GameState.Run)
            return;
        float effectiveFireInterval = baseFireInterval * SkillEffects.FireIntervalMultiplier;
        fireTimer += Time.deltaTime;
        if (fireTimer >= effectiveFireInterval)
        {
            fireTimer = 0f;
            AutoFire();
        }

        // Wave timer
        waveTimer += Time.deltaTime;
        if (waveTimer >= waveDuration)
        {
            waveTimer = 0f;
            AdvanceWave();
        }
        // Log remaining time every 10 seconds
        int secondsLeft = Mathf.CeilToInt(waveDuration - waveTimer);
        if (secondsLeft % 10 == 0 && secondsLeft != 0 && Mathf.Abs((waveDuration - waveTimer) - secondsLeft) < 0.02f)
        {
            Debug.Log($"Wave {currentWave}: {secondsLeft}s 남음");
        }
        RefreshWaveUI();
    }

    public void StartGame()
    {
        CurrentState = GameState.Run;
        fireTimer = 0f;
        ApplyWeaponConfig();
        currentWave = 1;
        waveTimer = 0f;
        ApplyWaveConfig(currentWave);
        oresCollectedThisRun = 0;
        oresCollectedStone = 0;
        oresCollectedCopper = 0;
        oresCollectedById.Clear();
        if (spawner != null) spawner.waveManager = this;
        else
        {
            // try to auto-find spawner
            spawner = FindObjectOfType<EnemySpawner>();
            if (spawner != null)
            {
                spawner.waveManager = this;
                Debug.Log("WaveManager: found EnemySpawner in scene and assigned WaveManager.");
            }
            else
            {
                Debug.LogWarning("WaveManager: No EnemySpawner found in scene. Creating one at runtime.");
                var go = new GameObject("EnemySpawner_Auto");
                spawner = go.AddComponent<EnemySpawner>();
                spawner.waveManager = this;
                // try assign planetCenter if there's a Planet object
                var p = GameObject.Find("Planet");
                if (p != null) spawner.planetCenter = p.transform;
                Debug.Log("WaveManager: EnemySpawner_Auto created and wired.");
            }
        }
        if (oxygenSystem == null) oxygenSystem = FindObjectOfType<OxygenSystem>();
        if (oxygenSystem == null)
        {
            var go = new GameObject("OxygenSystem_Auto");
            oxygenSystem = go.AddComponent<OxygenSystem>();
            oxygenSystem.waveManager = this;
            Debug.Log("WaveManager: OxygenSystem_Auto created and wired.");
        }
        // Runtime: create a simple firePoint if none. Prefer attaching to Player so firing originates from player.
        if (firePoint == null)
        {
            Transform playerT = null;
            var pgo = GameObject.FindWithTag("Player");
            if (pgo != null) playerT = pgo.transform;
            if (playerT == null)
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null) playerT = pc.transform;
            }

            var fpgo = new GameObject("FirePoint_Temp");
            if (playerT != null)
            {
                fpgo.transform.SetParent(playerT, false);
                // place slightly ahead of player along its up vector
                fpgo.transform.localPosition = Vector3.up * 0.6f;
                fpgo.transform.localRotation = Quaternion.identity;
            }
            else
            {
                fpgo.transform.position = Camera.main != null ? Camera.main.transform.position + Vector3.up * 2f : Vector3.up * 5f;
            }
            firePoint = fpgo.transform;
            Debug.Log("임시 FirePoint를 생성했습니다 (FirePoint_Temp)");
        }

        // Runtime: create a simple projectile prefab if none assigned
        if (projectilePrefab == null)
        {
            var temp = new GameObject("ProjectilePrefab_Temp");
            var sr = temp.AddComponent<SpriteRenderer>();
            sr.sprite = CreateColorSprite(Color.yellow);
            sr.color = Color.yellow;
            // make sure temp projectile renders on top and not masked
            try
            {
                sr.sortingOrder = Mathf.Max(sr.sortingOrder, 1000);
                sr.maskInteraction = SpriteMaskInteraction.None;
            }
            catch (System.Exception) { }
            var col = temp.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            var rb = temp.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            var proj = temp.AddComponent<Projectile>();
            proj.speed = 4f;
            temp.SetActive(false);
            projectilePrefab = temp;
            Debug.Log("임시 Projectile prefab을 생성했습니다 (ProjectilePrefab_Temp)");
        }
        ApplyProjectileStats();
        // TODO: 웨이브, 산소 등 초기화
        Debug.Log("게임 시작!");
        Debug.Log($"Wave {currentWave} 시작");
        if (spawner != null) spawner.OnWaveStarted(currentWave);
    }

    Sprite CreateColorSprite(Color c)
    {
        Texture2D tex = new Texture2D(8, 8);
        Color[] cols = new Color[8 * 8];
        for (int i = 0; i < cols.Length; i++) cols[i] = c;
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    public void EndRun()
    {
        CurrentState = GameState.Upgrade;
        // TODO: 업그레이드 씬 전환 등 처리
        Debug.Log("런 종료, 업그레이드로 전환");
        if (spawner != null) spawner.enabled = false;
        var flow = FindObjectOfType<GameFlowManager>();
        if (flow != null)
            flow.ShowEnd();
    }

    void AdvanceWave()
    {
        currentWave++;
        ApplyWaveConfig(currentWave);
        Debug.Log($"웨이브 진행: {currentWave}");
        if (spawner != null) spawner.OnWaveStarted(currentWave);
        var waveDef = GameData.GetWave(currentWave);
        if (waveDef != null && waveDef.isBossWave)
        {
            // spawn boss
            SpawnBoss();
        }
    }

    void SpawnBoss()
    {
        Debug.Log("보스 등장!");
        // For now, pause spawner
        if (spawner != null) spawner.enabled = false;
        // Actual boss prefab logic can be added later
    }

    public void OnEnemyKilled(int ore, float oxygen, string oreId)
    {
        oresCollectedThisRun += ore;
        if (!string.IsNullOrEmpty(oreId))
        {
            if (!oresCollectedById.ContainsKey(oreId)) oresCollectedById[oreId] = 0;
            oresCollectedById[oreId] += ore;
        }
        if (oreId == "copper")
            oresCollectedCopper += ore;
        else if (oreId == "stone")
            oresCollectedStone += ore;
        if (oxygenSystem != null)
        {
            float totalOxygen = oxygen + SkillEffects.OxygenOnKillBonus;
            if (totalOxygen > 0f)
                oxygenSystem.ChangeOxygen(totalOxygen);
        }
        // Enemy killed: update ores and oxygen (log suppressed per request)
        RefreshOreUI();
    }

    // Called when player's HP reaches zero to trigger end-run sequence
    public void TriggerEndRunSequence(PlayerController player)
    {
        if (endSequenceStarted) return;
        endSequenceStarted = true;
        CurrentState = GameState.Upgrade; // stop auto-fire immediately
        if (spawner != null) spawner.enabled = false;
        var flow = FindObjectOfType<GameFlowManager>();
        if (flow != null)
        {
            flow.SetEndReason("소행성 충돌");
            flow.ShowEnd();
        }
        StartCoroutine(EndRunSequenceCoroutine(player));
    }

    System.Collections.IEnumerator EndRunSequenceCoroutine(PlayerController player)
    {
        Debug.Log("End run sequence started: freezing game...");
        // stop spawner
        if (spawner != null) spawner.enabled = false;
        // stop existing enemies (disable their Update movement by disabling script)
        var enemies = FindObjectsOfType<Enemy>();
        foreach (var e in enemies)
        {
            e.enabled = false;
            var rb = e.GetComponent<Rigidbody2D>();
            if (rb != null) rb.simulated = false;
        }
        // remove projectiles
        var projs = FindObjectsOfType<Projectile>();
        foreach (var p in projs)
        {
            Destroy(p.gameObject);
        }
        // hide player
        if (player != null)
        {
            var sr = player.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
            player.enabled = false;
        }
        // short pause so player sees impact, then finish
        yield return new WaitForSecondsRealtime(0.5f);
        Debug.Log("End run sequence finished.");
        var flow = FindObjectOfType<GameFlowManager>();
        if (flow != null) flow.ShowEnd();
    }

    void RefreshWaveUI()
    {
        if (waveText != null) waveText.text = $"Wave : {currentWave}";
        if (waveTimerText != null) waveTimerText.text = $"{Mathf.CeilToInt(waveDuration - waveTimer)}s";
    }

    void ApplyWaveConfig(int wave)
    {
        var cfg = GameData.GetWave(wave);
        if (cfg != null && cfg.duration > 0f)
            waveDuration = cfg.duration;
    }

    void RefreshOreUI()
    {
        if (oreText != null) oreText.text = $"Ore: {oresCollectedThisRun}";
    }

    void AutoFire()
    {
        if (projectilePrefab == null || firePoint == null) return;
        // Find nearest enemy within range
        Transform target = FindNearestEnemy(firePoint.position, fireRange);
        if (target != null)
        {
            Vector3 dir = (target.position - firePoint.position).normalized;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            firePoint.rotation = Quaternion.Euler(0f, 0f, ang);
        }
        else
        {
            // No enemies: fire relative to player's up direction (so if player is at top -> shoot down, etc.)
            var player = firePoint.parent != null ? firePoint.parent : null;
            if (player != null)
            {
                Vector3 up = player.up;
                float ang = Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg;
                // We want upward local to map to shooting outward from planet: rotate by 90 degrees to point along +X of projectile
                firePoint.rotation = Quaternion.Euler(0f, 0f, ang);
            }
        }
        var pgo = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation) as GameObject;
        if (pgo != null)
        {
            // Ensure projectile's rotation matches firePoint so its local +X moves toward target
            pgo.transform.rotation = firePoint.rotation;
            // Force visible settings
            pgo.transform.position = new Vector3(pgo.transform.position.x, pgo.transform.position.y, 0f);
            var proj = pgo.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.damage = Mathf.Max(1, proj.damage + SkillEffects.DamageBonus);
            }
            pgo.transform.localScale = Vector3.one;
            var psr = pgo.GetComponent<SpriteRenderer>();
            if (psr != null)
            {
                psr.enabled = true;
                try { psr.sortingOrder = Mathf.Max(psr.sortingOrder, 1000); psr.maskInteraction = SpriteMaskInteraction.None; } catch (System.Exception) { }
            }
            pgo.SetActive(true);
        }
    }

    void ApplyWeaponConfig()
    {
        var w = GameData.GetWeapon(weaponId);
        if (w == null) return;
        if (w.fireInterval > 0f) fireInterval = w.fireInterval;
        baseFireInterval = fireInterval;
        if (w.detectRange > 0f) fireRange = w.detectRange;
    }

    void ApplyProjectileStats()
    {
        var w = GameData.GetWeapon(weaponId);
        if (w == null) return;
        if (projectilePrefab == null) return;
        var proj = projectilePrefab.GetComponent<Projectile>();
        if (proj == null) return;
        if (w.bulletSpeed > 0f) proj.speed = w.bulletSpeed;
        if (w.damage > 0) proj.damage = w.damage;
    }

    Transform FindNearestEnemy(Vector3 from, float range)
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        Transform best = null;
        float bestDist = range;

        Camera cam = Camera.main;
        // First pass: prefer enemies inside camera viewport if requested
        if (requireTargetInView && cam != null)
        {
            foreach (var e in enemies)
            {
                if (e == null) continue;
                Vector3 vp = cam.WorldToViewportPoint(e.transform.position);
                bool inView = vp.z > 0 && vp.x >= -viewportMargin && vp.x <= 1f + viewportMargin && vp.y >= -viewportMargin && vp.y <= 1f + viewportMargin;
                if (!inView) continue;
                float d = Vector3.Distance(from, e.transform.position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = e.transform;
                }
            }
            if (best != null) return best;
            // else fallthrough to normal nearest by distance
        }

        // Fallback: nearest enemy by distance within range
        foreach (var e in enemies)
        {
            if (e == null) continue;
            float d = Vector3.Distance(from, e.transform.position);
            if (d <= bestDist)
            {
                bestDist = d;
                best = e.transform;
            }
        }
        return best;
    }
}
