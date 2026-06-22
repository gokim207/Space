using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public Transform planetCenter;
    private float timer;
    public float spawnCheckInterval = 1f;
    public float spawnMinOffset = 15f;
    public float spawnMaxOffset = 25f;
    [Range(0f, 1f)] public float spawnThreeChance = 0.3f;
    [Range(0f, 1f)] public float spawnFiveChance = 0.1f;
    [Range(0f, 1f)] public float spawnTenChance = 0.05f;
    [Range(0f, 1f)] public float respawnOnDeathChance = 0.05f;
    private float planetSurfaceRadius = 0f;
    public WaveManager waveManager;
    public float minDistanceFromPlayer = 1.5f;
    public float minSpawnBlocksFromPlayer = 5f;
    public float minSpawnAngleFromPlayer = 35f;
    public int spawnPositionMaxAttempts = 20;
    public int baseHP = 2;
    public int hpPerWave = 1;
    public float baseMoveSpeed = 1.0f;
    public float speedPerWave = 0.05f;
    public float enemyScaleMultiplier = 1.5f;
    public bool preservePrefabScale = true;
    public float referencePlanetRadius = 5f;
    bool warnedWaveManagerMissing = false;
    bool initialized;

    void Start()
    {
        timer = 0f;
        PrepareForBossSpawns();
    }

    public void PrepareForBossSpawns()
    {
        if (initialized)
            return;

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

        initialized = enemyPrefab != null && planetCenter != null;
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
        if (timer >= spawnCheckInterval)
        {
            timer -= spawnCheckInterval;

            // Each chance is checked independently. Multiple groups can spawn on the same second.
            if (Random.value < spawnThreeChance)
                SpawnRandomBatch(3);
            if (Random.value < spawnFiveChance)
                SpawnRandomBatch(5);
            if (Random.value < spawnTenChance)
                SpawnRandomBatch(10);
        }
    }

    public void SpawnInitialEnemies()
    {
        SpawnRandomBatch(Random.Range(5, 11));
    }

    public void OnWaveEnded()
    {
        SpawnRandomBatch(5);
        if (Random.value < 0.5f)
            SpawnRandomBatch(10);
    }

    public void OnEnemyKilled()
    {
        if (Random.value < respawnOnDeathChance)
            SpawnRandomBatch(Random.Range(1, 3));
    }

    void SpawnRandomBatch(int count)
    {
        SpawnBatch(count, spawnMinOffset, spawnMaxOffset, "random");
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
        SpawnEnemy(pos, null);
    }

    Enemy SpawnEnemy(Vector3 pos, string forcedOreId)
    {
        if (enemyPrefab == null || planetCenter == null) return null;
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
            var def = string.IsNullOrEmpty(forcedOreId)
                ? PickEnemyDef(wave)
                : GameData.GetEnemyByOreId(forcedOreId);
            int scaledHp = CalculateWaveHp(baseHP, wave);
            float scaledSpeed = Mathf.Max(0.1f, baseMoveSpeed + (wave - 1) * speedPerWave);
            Color enemyColor = new Color(0.15f, 0.15f, 0.15f);
            string oreId = "stone";
            if (def != null)
            {
                scaledHp = CalculateWaveHp(def.baseHP, wave);
                scaledSpeed = Mathf.Max(0.1f, def.baseSpeed + (wave - 1) * def.speedPerWave);
                enemyColor = def.color;
                if (!string.IsNullOrEmpty(def.dropOreId)) oreId = def.dropOreId;
            }
            if (!string.IsNullOrEmpty(forcedOreId))
                oreId = forcedOreId;
            e.oreId = oreId;
            e.ApplyStats(scaledHp, scaledSpeed * GetWorldScale());
            e.contactRadius = Mathf.Max(e.contactRadius, EstimateContactRadius(e, playerT));
            // Make spawned enemy visible and ensure it renders on top of planet
            var sr = e.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = true;
                Sprite oreSprite = LoadEnemySprite(oreId);
                if (oreSprite != null)
                {
                    sr.sprite = oreSprite;
                    e.SetBaseColor(Color.white);
                }
                else
                {
                    e.SetBaseColor(enemyColor);
                }
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
            // 씬/프리팹에서 맞춘 크기를 기본으로 유지한다. 임시 프리팹만 테스트용으로 플레이어 기준 스케일을 적용한다.
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            bool isTempPrefab = enemyPrefab != null && enemyPrefab.name.Contains("_Temp");
            if (!preservePrefabScale || isTempPrefab)
            {
                Vector3 baseScale = playerT != null ? playerT.localScale : Vector3.one;
                go.transform.localScale = baseScale * Mathf.Max(0.01f, enemyScaleMultiplier);
            }
        }
        return e;
    }

    public Enemy SpawnBossEnemy(string oreId)
    {
        if (enemyPrefab == null || planetCenter == null)
            return null;

        float distance = planetSurfaceRadius + Mathf.Max(0.1f, Random.Range(spawnMinOffset, spawnMaxOffset));
        float angle = ResolveSafeSpawnAngle(Random.Range(0f, 360f), distance);
        return SpawnEnemy(BuildSafeSpawnPosition(angle, distance), oreId);
    }

    static int CalculateWaveHp(int baseHp, int wave)
    {
        float scaledHp = baseHp * (1f + (Mathf.Max(1, wave) - 1) * 0.07f);
        return Mathf.Max(1, Mathf.RoundToInt(scaledHp));
    }

    Sprite LoadEnemySprite(string oreId)
    {
        if (string.IsNullOrWhiteSpace(oreId))
            oreId = "stone";

        oreId = oreId.Trim().ToLowerInvariant();
        if (oreId == "stone")
        {
            return LoadSpriteFromIcon("stone_1", "stone_1_0");
        }

        if (oreId == "copper")
        {
            return LoadSpriteFromIcon("copper_node", "copper_node_0");
        }

        if (oreId == "iron")
        {
            return LoadSpriteFromIcon("silver_node", "silver_node_0");
        }

        if (oreId == "gold")
        {
            return LoadSpriteFromIcon("gold_node", "gold_node_0");
        }

        if (oreId == "diamond" || oreId == "diamon")
        {
            return LoadSpriteFromIcon("mystrile_node", "mystrile_node_0") ??
                   LoadSpriteFromIcon("diamond", "diamond");
        }

        string nodeIcon = $"{oreId}_node";
        return LoadSpriteFromIcon(nodeIcon, nodeIcon) ??
               LoadSpriteFromIcon(oreId, oreId);
    }

    Sprite LoadSpriteFromIcon(string resourceName, string preferredSpriteName)
    {
        Sprite direct = Resources.Load<Sprite>($"icon/{resourceName}");
        if (direct != null)
            return direct;

        Sprite[] sprites = Resources.LoadAll<Sprite>($"icon/{resourceName}");
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null &&
                string.Equals(sprites[i].name, preferredSpriteName, System.StringComparison.OrdinalIgnoreCase))
                return sprites[i];
        }

        return sprites.Length > 0 ? sprites[0] : null;
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
            Vector3 pos = BuildSafeSpawnPosition(angDeg, dist);
            SpawnEnemy(pos);
        }
    }

    Vector3 BuildSafeSpawnPosition(float preferredAngle, float dist)
    {
        var playerT = GetPlayerTransform();
        float minWorldDistance = GetMinSpawnDistanceFromPlayer();
        float angle = preferredAngle;

        for (int attempt = 0; attempt < Mathf.Max(1, spawnPositionMaxAttempts); attempt++)
        {
            angle = ResolveSafeSpawnAngle(angle, dist);
            Vector3 pos = planetCenter.position + AngleToDir(angle) * dist;
            if (playerT == null || Vector3.Distance(pos, playerT.position) >= minWorldDistance)
                return pos;

            angle = Random.Range(0f, 360f);
        }

        // Final fallback: place on the opposite side of the planet from the player.
        if (playerT != null && planetCenter != null)
        {
            Vector3 away = (planetCenter.position - playerT.position).normalized;
            if (away.sqrMagnitude > 0.0001f)
                return planetCenter.position + away * dist;
        }

        return planetCenter.position + AngleToDir(preferredAngle) * dist;
    }

    Vector3 AngleToDir(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
    }

    float GetMinSpawnDistanceFromPlayer()
    {
        return Mathf.Max(0f, minDistanceFromPlayer, minSpawnBlocksFromPlayer);
    }

    float ResolveSafeSpawnAngle(float preferredAngle)
    {
        float spawnRadius = planetSurfaceRadius + Mathf.Max(0.1f, spawnMinOffset);
        return ResolveSafeSpawnAngle(preferredAngle, spawnRadius);
    }

    float ResolveSafeSpawnAngle(float preferredAngle, float spawnRadius)
    {
        var playerT = GetPlayerTransform();
        if (playerT == null || planetCenter == null) return preferredAngle;

        Vector2 playerDir = playerT.position - planetCenter.position;
        if (playerDir.sqrMagnitude < 0.0001f) return preferredAngle;

        float playerAngle = Mathf.Atan2(playerDir.y, playerDir.x) * Mathf.Rad2Deg;
        float minAngleByDistance = 0f;
        float clearance = GetMinSpawnDistanceFromPlayer();
        if (spawnRadius > 0.001f && clearance > 0f)
        {
            float chordRatio = Mathf.Clamp(clearance / (2f * spawnRadius), 0f, 1f);
            minAngleByDistance = Mathf.Asin(chordRatio) * 2f * Mathf.Rad2Deg;
        }
        float requiredAngle = Mathf.Max(minSpawnAngleFromPlayer, minAngleByDistance);
        float angle = preferredAngle;
        for (int attempt = 0; attempt < Mathf.Max(1, spawnPositionMaxAttempts); attempt++)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(angle, playerAngle));
            if (delta >= requiredAngle)
                return angle;
            angle = Random.Range(0f, 360f);
        }

        float side = Random.value < 0.5f ? -1f : 1f;
        return playerAngle + side * requiredAngle;
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
