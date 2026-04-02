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
    public int baseHP = 2;
    public int hpPerWave = 1;
    public float baseMoveSpeed = 1.0f;
    public float speedPerWave = 0.05f;
    public string spawnId = "default";
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
                Debug.Log($"EnemySpawner: planetSurfaceRadius from CircleCollider2D = {planetSurfaceRadius}");
            }
            else
            {
                var sr = planetCenter.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    planetSurfaceRadius = Mathf.Max(sr.bounds.extents.x, sr.bounds.extents.y);
                    Debug.Log($"EnemySpawner: planetSurfaceRadius from SpriteRenderer bounds = {planetSurfaceRadius}");
                }
            }
            if (planetSurfaceRadius <= 0f) planetSurfaceRadius = 1f; // fallback
        }
        if (planetCenter != null)
            Debug.Log($"EnemySpawner: planetCenter found at {planetCenter.position}");
        else
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
            Debug.Log("임시 Enemy prefab을 생성했습니다 (EnemyPrefab_Temp)");
        }

        // Log existing enemies count (do not hide pre-placed enemies anymore)
        var existing = FindObjectsOfType<Enemy>();
        Debug.Log($"EnemySpawner: found {existing.Length} pre-placed Enemy components in scene.");

        // Try auto-assign waveManager if missing
        if (waveManager == null)
        {
            waveManager = FindObjectOfType<WaveManager>();
            if (waveManager != null)
                Debug.Log("EnemySpawner: auto-assigned WaveManager.");
            else
                Debug.LogWarning("EnemySpawner: WaveManager is null. Spawning will be paused until WaveManager exists.");
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
                Debug.Log("Spawned enemy moveSpeed was 0, reset to 1f for testing.");
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
            e.ApplyStats(scaledHp, scaledSpeed);
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
        float baseAngle = Random.Range(0f, 360f);
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

            Vector3 pos = planetCenter.position + new Vector3(Mathf.Cos(angDeg * Mathf.Deg2Rad), Mathf.Sin(angDeg * Mathf.Deg2Rad), 0f) * dist;
            var playerT = GetPlayerTransform();
            if (playerT != null && Vector3.Distance(pos, playerT.position) < minDistanceFromPlayer)
            {
                Vector3 dir = (planetCenter.position - playerT.position).normalized;
                pos = planetCenter.position + dir * dist;
            }
            SpawnEnemy(pos);
        }
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
