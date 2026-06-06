using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public Transform planetCenter;
    public float spawnInterval = 2f;
    private float timer;
    // spawnOffset is distance beyond the planet surface where enemies will appear
    public float spawnOffset = 3f;
    private float planetSurfaceRadius = 0f;
    public WaveManager waveManager;
    public float minDistanceFromPlayer = 1.5f;
    public float minSpawnAngleFromPlayer = 35f;
    public int spawnPositionMaxAttempts = 20;
    public int baseHP = 2;
    public int hpPerWave = 1;
    public float baseMoveSpeed = 1.0f;
    public float speedPerWave = 0.05f;
    public string spawnId = "default";
    public float referencePlanetRadius = 5f;
    bool warnedWaveManagerMissing = false;
    private float nextSpawnInterval = 2f;

    void Start()
    {
        timer = 0f;
        if (planetCenter == null)
        {
            var p = GameObject.Find("Planet");
            if (p != null) planetCenter = p.transform;
        }
        // compute planet surface radius if possible (from CircleCollider2D or SpriteRenderer bounds)
        if (planetCenter != null)
        {
            var cc = planetCenter.GetComponent<CircleCollider2D>();
            if (cc != null)
            {
                planetSurfaceRadius = Mathf.Abs(cc.radius) * planetCenter.lossyScale.x;
            }
            else
            {
                var sr = planetCenter.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    planetSurfaceRadius = Mathf.Max(sr.bounds.extents.x, sr.bounds.extents.y);
                }
            }
            if (planetSurfaceRadius <= 0f) planetSurfaceRadius = 1f; // fallback
        }
        if (planetCenter == null)
            Debug.LogWarning("EnemySpawner: planetCenter is null. Enemies will not spawn until planet exists or planetCenter is assigned.");
        // Auto-create a temporary enemy prefab if none assigned (so testing works without editor prefabs)
        if (enemyPrefab == null)
        {
            var temp = new GameObject("EnemyPrefab_Temp");
            var sr = temp.AddComponent<SpriteRenderer>();
            sr.sprite = CreateColorSprite(Color.red);
            sr.color = Color.red;
            var col = temp.AddComponent<BoxCollider2D>();
            var e = temp.AddComponent<Enemy>();
            e.maxHP = baseHP;
            e.moveSpeed = baseMoveSpeed;
            enemyPrefab = temp;
            temp.SetActive(false); // template
        }

        EnsureEnemyPrefabUsable();

        // Log existing enemies count (do not hide pre-placed enemies anymore)
        // Try auto-assign waveManager if missing
        if (waveManager == null)
        {
            waveManager = FindObjectOfType<WaveManager>();
            if (waveManager != null)
                warnedWaveManagerMissing = false;
            else
                Debug.LogWarning("EnemySpawner: WaveManager is null. Spawning will be paused until WaveManager exists.");
        }
    }

    void EnsureEnemyPrefabUsable()
    {
        if (enemyPrefab == null) return;
        if (enemyPrefab.GetComponent<Enemy>() == null)
        {
            var e = enemyPrefab.AddComponent<Enemy>();
            e.maxHP = baseHP;
            e.moveSpeed = baseMoveSpeed;
        }
        if (enemyPrefab.GetComponent<Collider2D>() == null)
        {
            enemyPrefab.AddComponent<BoxCollider2D>();
        }
        var rb = enemyPrefab.GetComponent<Rigidbody2D>();
        if (rb == null) rb = enemyPrefab.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        var sr = enemyPrefab.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            try { sr.sortingOrder = Mathf.Max(sr.sortingOrder, 10); sr.maskInteraction = SpriteMaskInteraction.None; } catch (System.Exception) { }
        }
    }

    Sprite CreateColorSprite(Color c)
    {
        Texture2D tex = new Texture2D(16, 16);
        Color[] cols = new Color[16 * 16];
        for (int i = 0; i < cols.Length; i++) cols[i] = c;
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }
    void Update()
    {
        if (waveManager == null)
        {
            if (!warnedWaveManagerMissing)
            {
                Debug.LogWarning("EnemySpawner: waveManager is null or not assigned. Spawning paused.");
                warnedWaveManagerMissing = true;
            }
            return;
        }
        if (waveManager.CurrentState != WaveManager.GameState.Run)
        {
            // only warn once to avoid spam
            if (!warnedWaveManagerMissing)
            {
                Debug.LogWarning("EnemySpawner: WaveManager not in Run state. Spawning paused.");
                warnedWaveManagerMissing = true;
            }
            return;
        }
        timer += Time.deltaTime;
        // spawn tick
        if (timer >= nextSpawnInterval)
        {
            timer = 0f;
            var spawnDef = ResolveSpawnDef();
            int count = 1;
            float minDist = spawnOffset;
            float maxDist = spawnOffset;
            string pattern = "random";
            if (spawnDef != null)
            {
                count = Mathf.Max(1, Random.Range(spawnDef.minAmount, spawnDef.maxAmount + 1));
                minDist = spawnDef.minDist;
                maxDist = spawnDef.maxDist;
                pattern = spawnDef.spawnPattern;
                nextSpawnInterval = Random.Range(spawnDef.minInterval, spawnDef.maxInterval);
            }
            else
            {
                nextSpawnInterval = spawnInterval;
            }
            SpawnBatch(count, minDist, maxDist, pattern);
        }
    }

    public void OnWaveStarted(int wave)
    {
        int count = Random.Range(5, 11);
        var waveDef = GameData.GetWave(wave);
        if (waveDef != null && waveDef.spawnCountMin > 0 && waveDef.spawnCountMax >= waveDef.spawnCountMin)
        {
            count = Random.Range(waveDef.spawnCountMin, waveDef.spawnCountMax + 1);
        }
        var spawnDef = ResolveSpawnDef();
        float minDist = spawnOffset;
        float maxDist = spawnOffset;
        string pattern = "random";
        if (spawnDef != null)
        {
            minDist = spawnDef.minDist;
            maxDist = spawnDef.maxDist;
            pattern = spawnDef.spawnPattern;
        }
        SpawnBatch(count, minDist, maxDist, pattern);
    }

    Transform GetPlayerTransform()
    {
        var playerT = GameObject.FindWithTag("Player")?.transform;
        if (playerT == null)
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null) playerT = pc.transform;
        }
        return playerT;
    }

    void SpawnEnemy(Vector3 pos)
    {
        if (enemyPrefab == null || planetCenter == null) return;
        var playerT = GetPlayerTransform();
        var go = Instantiate(enemyPrefab, pos, Quaternion.identity);
        go.SetActive(true);
        var e = go.GetComponent<Enemy>();
        if (e == null)
        {
            e = go.AddComponent<Enemy>();
            e.maxHP = baseHP;
            e.moveSpeed = baseMoveSpeed;
        }
        if (e != null)
        {
            playerT = GetPlayerTransform();
            e.target = playerT;
            e.waveManager = waveManager;
            if (e.target == null)
                Debug.LogWarning("Spawned enemy has no Player target assigned (no object tagged Player in scene)." );
            // Ensure moveSpeed is non-zero for testing
            if (e.moveSpeed <= 0f)
            {
                e.moveSpeed = 1f;
            }
            int wave = waveManager != null ? waveManager.currentWave : 1;
            var def = PickEnemyDef(wave);
            int scaledHp = Mathf.Max(1, baseHP + (wave - 1) * hpPerWave);
            float scaledSpeed = Mathf.Max(0.1f, baseMoveSpeed + (wave - 1) * speedPerWave);
            Color enemyColor = new Color(0.15f, 0.15f, 0.15f);
            string oreId = "stone";
            if (def != null)
            {
                scaledHp = Mathf.Max(1, def.baseHP + (wave - 1) * def.hpPerWave);
                scaledSpeed = Mathf.Max(0.1f, def.baseSpeed + (wave - 1) * def.speedPerWave);
                enemyColor = def.color;
                if (!string.IsNullOrEmpty(def.dropOreId)) oreId = def.dropOreId;
            }
            e.oreId = oreId;
            e.ApplyStats(scaledHp, scaledSpeed * GetWorldScale());
            e.contactRadius = Mathf.Max(e.contactRadius, EstimateContactRadius(e, playerT));
            // Make spawned enemy visible and ensure it renders on top of planet
            var sr = e.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = true;
                e.SetBaseColor(enemyColor);
                // Force visible: put on high order in layer and ignore sprite masks
                try
                {
                    sr.sortingOrder = Mathf.Max(sr.sortingOrder, 1000);
                    sr.maskInteraction = SpriteMaskInteraction.None;
                }
                catch (System.Exception)
                {
                    // Older Unity versions may not support maskInteraction property on SpriteRenderer.
                }
            }
            // Ensure transform is at z=0 and match player's scale if available
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            if (playerT != null)
                go.transform.localScale = playerT.localScale;
            else
                go.transform.localScale = Vector3.one;
        }
    }

    void SpawnBatch(int count, float minDist, float maxDist, string pattern)
    {
        if (enemyPrefab == null || planetCenter == null) return;
        if (count <= 0) return;

        float dist = planetSurfaceRadius + Mathf.Max(0.1f, Random.Range(minDist, maxDist));
        float baseAngle = ResolveSafeSpawnAngle(Random.Range(0f, 360f));
        float spreadDeg = 12f;

        for (int i = 0; i < count; i++)
        {
            float angDeg = 0f;
            if (pattern == "cluster")
            {
                angDeg = baseAngle + Random.Range(-spreadDeg, spreadDeg);
            }
            else if (pattern == "surround")
            {
                float step = 360f / count;
                angDeg = baseAngle + (step * i);
            }
            else
            {
                angDeg = Random.Range(0f, 360f);
            }

            angDeg = ResolveSafeSpawnAngle(angDeg);
            Vector3 pos = planetCenter.position + new Vector3(Mathf.Cos(angDeg * Mathf.Deg2Rad), Mathf.Sin(angDeg * Mathf.Deg2Rad), 0f) * dist;
            var playerT = GetPlayerTransform();
            float minWorldDistance = Mathf.Max(minDistanceFromPlayer, planetSurfaceRadius * 0.35f);
            if (playerT != null && Vector3.Distance(pos, playerT.position) < minWorldDistance)
            {
                Vector3 dir = (pos - playerT.position).normalized;
                if (dir.sqrMagnitude < 0.0001f)
                    dir = (pos - planetCenter.position).normalized;
                pos = planetCenter.position + dir * dist;
            }
            SpawnEnemy(pos);
        }
    }

    float ResolveSafeSpawnAngle(float preferredAngle)
    {
        var playerT = GetPlayerTransform();
        if (playerT == null || planetCenter == null) return preferredAngle;

        Vector2 playerDir = playerT.position - planetCenter.position;
        if (playerDir.sqrMagnitude < 0.0001f) return preferredAngle;

        float playerAngle = Mathf.Atan2(playerDir.y, playerDir.x) * Mathf.Rad2Deg;
        float angle = preferredAngle;
        for (int attempt = 0; attempt < Mathf.Max(1, spawnPositionMaxAttempts); attempt++)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(angle, playerAngle));
            if (delta >= minSpawnAngleFromPlayer)
                return angle;
            angle = Random.Range(0f, 360f);
        }

        float side = Random.value < 0.5f ? -1f : 1f;
        return playerAngle + side * minSpawnAngleFromPlayer;
    }

    GameData.EnemySpawnDef ResolveSpawnDef()
    {
        var wave = waveManager != null ? waveManager.currentWave : 1;
        var waveDef = GameData.GetWave(wave);
        if (waveDef != null && waveDef.isBossWave)
        {
            var bossWarn = GameData.GetEnemySpawn("boss_warning");
            if (bossWarn != null) return bossWarn;
        }
        var def = GameData.GetEnemySpawn(spawnId);
        if (def != null) return def;
        return GameData.GetEnemySpawn("default");
    }

    float GetWorldScale()
    {
        if (planetSurfaceRadius <= 0f) return 1f;
        return Mathf.Max(1f, planetSurfaceRadius / Mathf.Max(0.01f, referencePlanetRadius));
    }

    float EstimateContactRadius(Enemy enemy, Transform playerT)
    {
        float radius = 0.5f * GetWorldScale();
        var enemyRenderer = enemy.GetComponent<SpriteRenderer>();
        if (enemyRenderer != null)
            radius = Mathf.Max(radius, Mathf.Max(enemyRenderer.bounds.extents.x, enemyRenderer.bounds.extents.y));
        if (playerT != null)
        {
            var playerRenderer = playerT.GetComponent<SpriteRenderer>();
            if (playerRenderer != null)
                radius += Mathf.Max(playerRenderer.bounds.extents.x, playerRenderer.bounds.extents.y);
        }
        return Mathf.Max(0.5f, radius);
    }

    GameData.EnemyDef PickEnemyDef(int wave)
    {
        int totalWeight = 0;
        var list = new System.Collections.Generic.List<GameData.EnemyDef>();
        foreach (var def in GameData.GetAvailableEnemies(wave))
        {
            if (def.spawnWeight <= 0) continue;
            totalWeight += def.spawnWeight;
            list.Add(def);
        }
        if (list.Count == 0 || totalWeight <= 0) return null;
        int r = Random.Range(0, totalWeight);
        int acc = 0;
        foreach (var def in list)
        {
            acc += def.spawnWeight;
            if (r < acc) return def;
        }
        return list[0];
    }
}
