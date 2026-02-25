using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public Transform planetCenter;
    public float spawnInterval = 10f;
    private float timer;
    // spawnOffset is distance beyond the planet surface where enemies will appear
    public float spawnOffset = 3f;
    private float planetSurfaceRadius = 0f;
    public WaveManager waveManager;
    // spawn count increases over time (every spawnIncreaseInterval seconds, spawnCountPerTick++)
    public int spawnCountPerTick = 1;
    public float spawnIncreaseInterval = 5f;
    private float spawnIncreaseTimer = 0f;
    bool warnedWaveManagerMissing = false;

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
            e.maxHP = 1;
            e.moveSpeed = 1.2f;
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
        if (timer >= spawnInterval)
        {
            timer = 0f;
            for (int i = 0; i < spawnCountPerTick; i++)
            {
                SpawnEnemy();
            }
        }

        // increase spawn count every spawnIncreaseInterval seconds
        spawnIncreaseTimer += Time.deltaTime;
        if (spawnIncreaseTimer >= spawnIncreaseInterval)
        {
            spawnIncreaseTimer = 0f;
            spawnCountPerTick += 1;
        }
    }

    void SpawnEnemy()
    {
        if (enemyPrefab == null || planetCenter == null) return;
    float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad;
    float dist = planetSurfaceRadius + spawnOffset;
    Vector3 pos = planetCenter.position + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * dist;
        var go = Instantiate(enemyPrefab, pos, Quaternion.identity);
        go.SetActive(true);
        var e = go.GetComponent<Enemy>();
        if (e != null)
        {
            var playerT = GameObject.FindWithTag("Player")?.transform;
            if (playerT == null)
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null) playerT = pc.transform;
            }
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
            // Make spawned enemy visible and ensure it renders on top of planet
            var sr = e.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = true;
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
}
