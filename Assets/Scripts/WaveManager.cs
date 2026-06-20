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
    private float baseProjectileSpeed = 10f;
    private float baseProjectileLifeTime = 2f;
    private float baseProjectileDamage = 1f;
    private int baseProjectilePierceCount = 0;
    private int baseProjectileCount = 1;
    public float multiProjectileSpreadAngle = 12f;
    private float baseMultiProjectileSpreadAngle = 12f;
    private float fireTimer;
    public float fireRange = 30f; // targeting range
    private float baseFireRange = 30f;
    public float referenceOrbitRadius = 5f;
    public float runtimeProjectileVisualScale = 2.25f;
    public bool preserveProjectileTemplateScale = true;
    public float uiProjectileSizeMultiplier = 1.5f;
    public Vector2 firePointLocalOffset = new Vector2(0.25f, 0.05f);
    public bool requireTargetInView = true;
    public float viewportMargin = 0.02f; // allow slight margin outside [0,1]
    private bool usingRuntimeProjectilePrefab = false;
    private RunWeaponDisplay runWeaponDisplay;
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
    readonly Dictionary<string, float> oreDropRemainders = new Dictionary<string, float>();
    readonly Dictionary<string, int> waveStartOreCounts = new Dictionary<string, int>();
    readonly Dictionary<string, float> waveBonusRemainders = new Dictionary<string, float>();
    private bool endSequenceStarted = false;
    // UI
    public Text waveText;
    public Text waveTimerText;
    public Text oreText;
    public string weaponId = "starter_gun";
    public string projectileId = "default";

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
        WeaponTraitRuntime.UpdateCombat(Time.deltaTime);
        if (BossBattleSession.IsCombatPaused)
        {
            fireTimer = 0f;
            return;
        }
        float effectiveFireInterval = Mathf.Max(
            0.05f,
            baseFireInterval *
            SkillEffects.FireIntervalMultiplier *
            WeaponTraitRuntime.GetDynamicFireIntervalMultiplier(weaponId, oxygenSystem));
        fireTimer += Time.deltaTime;
        if (fireTimer >= effectiveFireInterval)
        {
            fireTimer = 0f;
            AutoFire();
        }

        // 보스전은 일반 웨이브 타이머와 적 증원 규칙을 사용하지 않는다.
        if (!BossBattleSession.IsBossBattle)
        {
            waveTimer += Time.deltaTime;
            if (waveTimer >= waveDuration)
            {
                waveTimer = 0f;
                AdvanceWave();
            }
            RefreshWaveUI();
        }
    }

    public void StartGame()
    {
        CurrentState = GameState.Run;
        WeaponTraitRuntime.ResetRun();
        baseMultiProjectileSpreadAngle = multiProjectileSpreadAngle;
        fireTimer = 0f;
        ApplyWeaponConfig();
        currentWave = 1;
        waveTimer = 0f;
        oresCollectedThisRun = 0;
        oresCollectedStone = 0;
        oresCollectedCopper = 0;
        oresCollectedById.Clear();
        oreDropRemainders.Clear();
        waveStartOreCounts.Clear();
        waveBonusRemainders.Clear();
        if (BossBattleSession.IsBossBattle)
        {
            if (spawner == null)
                spawner = FindObjectOfType<EnemySpawner>();
            if (spawner != null)
                spawner.enabled = false;
        }
        else if (spawner != null) spawner.waveManager = this;
        else
        {
            // try to auto-find spawner
            spawner = FindObjectOfType<EnemySpawner>();
            if (spawner != null)
            {
                spawner.waveManager = this;
            }
            else
            {
                var go = new GameObject("EnemySpawner_Auto");
                spawner = go.AddComponent<EnemySpawner>();
                spawner.waveManager = this;
                // try assign planetCenter if there's a Planet object
                var p = GameObject.Find("Planet");
                if (p != null) spawner.planetCenter = p.transform;
            }
        }
        if (oxygenSystem == null) oxygenSystem = FindObjectOfType<OxygenSystem>();
        if (oxygenSystem == null)
        {
            var go = new GameObject("OxygenSystem_Auto");
            oxygenSystem = go.AddComponent<OxygenSystem>();
            oxygenSystem.waveManager = this;
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
                // Keep the shot origin around the character body instead of above the head.
                fpgo.transform.localPosition = new Vector3(firePointLocalOffset.x, firePointLocalOffset.y, 0f);
                fpgo.transform.localRotation = Quaternion.identity;
            }
            else
            {
                fpgo.transform.position = Camera.main != null ? Camera.main.transform.position + Vector3.up * 2f : Vector3.up * 5f;
            }
            firePoint = fpgo.transform;
        }

        runWeaponDisplay = RunWeaponDisplay.EnsureExists(this);

        // Runtime: prefer a scene bullet template, then data/resource art, then a debug square.
        if (projectilePrefab == null)
            projectilePrefab = FindSceneProjectileTemplate();

        if (projectilePrefab == null)
            projectilePrefab = CreateProjectilePrefabFromResources();

        if (projectilePrefab == null)
            projectilePrefab = CreateDebugProjectilePrefab();
        EnsureProjectilePrefabUsable();
        ApplyProjectileStats();
        if (!BossBattleSession.IsBossBattle && spawner != null)
            spawner.SpawnInitialEnemies();
    }



    GameObject FindSceneProjectileTemplate()
    {
        var activeScene = SceneManager.GetActiveScene();
        var roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var found = FindChildByName(roots[i].transform, "bullet");
            if (found == null)
                found = FindChildByName(roots[i].transform, "Bullet");
            if (found == null) continue;

            var go = found.gameObject;
            var scenePrefab = CreateProjectilePrefabFromSceneTemplate(go);
            go.SetActive(false);
            return scenePrefab != null ? scenePrefab : go;
        }
        return null;
    }

    Transform FindChildByName(Transform root, string targetName)
    {
        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var result = FindChildByName(root.GetChild(i), targetName);
            if (result != null)
                return result;
        }
        return null;
    }


    GameObject CreateProjectilePrefabFromSceneTemplate(GameObject template)
    {
        if (template == null) return null;

        var sourceSpriteRenderer = template.GetComponent<SpriteRenderer>();
        var sourceImage = template.GetComponent<Image>();
        Sprite sprite = sourceSpriteRenderer != null ? sourceSpriteRenderer.sprite : sourceImage != null ? sourceImage.sprite : null;
        if (sprite == null) return null;

        var temp = new GameObject("ProjectilePrefab_SceneBullet");
        temp.transform.localScale = GetProjectileTemplateScale(template, sprite, sourceSpriteRenderer, sourceImage);
        var sr = temp.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = sourceSpriteRenderer != null ? sourceSpriteRenderer.color : sourceImage != null ? sourceImage.color : Color.white;
        try
        {
            sr.sortingOrder = Mathf.Max(sourceSpriteRenderer != null ? sourceSpriteRenderer.sortingOrder : 0, 1000);
            sr.maskInteraction = SpriteMaskInteraction.None;
        }
        catch (System.Exception) { }

        var col = temp.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        var rb = temp.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        var proj = temp.AddComponent<Projectile>();
        proj.speed = baseProjectileSpeed;
        proj.lifeTime = baseProjectileLifeTime;
        temp.SetActive(false);
        usingRuntimeProjectilePrefab = true;
        return temp;
    }

    Vector3 GetProjectileTemplateScale(GameObject template, Sprite sprite, SpriteRenderer sourceSpriteRenderer, Image sourceImage)
    {
        if (sourceSpriteRenderer != null)
            return template.transform.localScale;

        if (sourceImage != null)
        {
            var rect = sourceImage.rectTransform.rect;
            Vector2 uiSize = rect.size;
            if (uiSize.x <= 0.01f || uiSize.y <= 0.01f)
                uiSize = sourceImage.rectTransform.sizeDelta;

            Vector2 spriteSize = sprite.bounds.size;
            if (uiSize.x > 0.01f && uiSize.y > 0.01f && spriteSize.x > 0.01f && spriteSize.y > 0.01f)
            {
                float scaleX = uiSize.x / spriteSize.x;
                float scaleY = uiSize.y / spriteSize.y;
                return new Vector3(scaleX, scaleY, 1f) * Mathf.Max(0.01f, uiProjectileSizeMultiplier);
            }
        }

        return template.transform.localScale;
    }

    GameObject CreateProjectilePrefabFromResources()
    {
        var pdef = GameData.GetProjectile(projectileId);
        string prefabId = pdef != null ? pdef.prefabId : "";

        var prefab = LoadProjectileResource<GameObject>(prefabId);
        if (prefab != null)
        {
            usingRuntimeProjectilePrefab = false;
            return prefab;
        }

        var sprite = LoadProjectileResource<Sprite>(prefabId);
        if (sprite == null)
            sprite = Resources.Load<Sprite>("bullets/Default");
        if (sprite == null)
            sprite = Resources.Load<Sprite>("bullets/default");
        if (sprite == null)
        {
            var sprites = Resources.LoadAll<Sprite>("bullets");
            if (sprites != null && sprites.Length > 0)
                sprite = sprites[0];
        }

        if (sprite == null)
            return null;

        var temp = new GameObject("ProjectilePrefab_Resource");
        var sr = temp.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
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
        proj.speed = baseProjectileSpeed;
        proj.lifeTime = baseProjectileLifeTime;
        temp.SetActive(false);
        usingRuntimeProjectilePrefab = true;
        return temp;
    }

    T LoadProjectileResource<T>(string id) where T : Object
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        id = id.Trim();
        var resource = Resources.Load<T>($"bullets/{id}");
        if (resource != null) return resource;
        return Resources.Load<T>(id);
    }

    GameObject CreateDebugProjectilePrefab()
    {
        var temp = new GameObject("ProjectilePrefab_Temp");
        var sr = temp.AddComponent<SpriteRenderer>();
        sr.sprite = CreateColorSprite(Color.yellow);
        sr.color = Color.yellow;
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
        usingRuntimeProjectilePrefab = true;
        return temp;
    }

    void EnsureProjectilePrefabUsable()
    {
        if (projectilePrefab == null) return;
        var sr = projectilePrefab.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = projectilePrefab.AddComponent<SpriteRenderer>();
            sr.sprite = CreateColorSprite(Color.yellow);
            sr.color = Color.yellow;
        }
        var col = projectilePrefab.GetComponent<Collider2D>();
        if (col == null)
        {
            var circle = projectilePrefab.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
        }
        else
        {
            col.isTrigger = true;
        }
        var rb = projectilePrefab.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = projectilePrefab.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        if (projectilePrefab.GetComponent<Projectile>() == null)
        {
            var proj = projectilePrefab.AddComponent<Projectile>();
            proj.speed = 10f;
            proj.lifeTime = 2f;
            proj.damage = 1;
        }
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
        if (spawner != null) spawner.enabled = false;
        var flow = FindObjectOfType<GameFlowManager>();
        if (flow != null)
            flow.ShowEnd();
    }

    void AdvanceWave()
    {
        ApplyWaveEndOreBonus();
        currentWave++;
        if (spawner != null) spawner.OnWaveEnded();
    }

    public void OnEnemyKilled(int ore, float oxygen, string oreId, Projectile sourceProjectile = null)
    {
        if (spawner != null)
            spawner.OnEnemyKilled();

        string sourceWeaponId = sourceProjectile != null && !string.IsNullOrEmpty(sourceProjectile.weaponId)
            ? sourceProjectile.weaponId
            : weaponId;
        float exactOre = ore * WeaponTraitRuntime.GetOreDropMultiplier(sourceWeaponId);
        oreDropRemainders.TryGetValue(oreId ?? "", out float remainder);
        exactOre += remainder;
        int awardedOre = Mathf.Max(0, Mathf.FloorToInt(exactOre));
        oreDropRemainders[oreId ?? ""] = exactOre - awardedOre;
        AddCollectedOre(oreId, awardedOre);
        if (oxygenSystem != null)
        {
            float totalOxygen = oxygen + SkillEffects.OxygenOnKillBonus;
            float missingOxygen = Mathf.Max(0f, oxygenSystem.MaxOxygen - oxygenSystem.currentOxygen);
            float missingRecoveryRatio = SkillEffects.OxygenOnKillMissingRatio +
                                         WeaponTraitRuntime.GetOxygenOnKillMissingRatio(sourceWeaponId);
            totalOxygen += missingOxygen * missingRecoveryRatio;
            if (oxygenSystem != null && oxygenSystem.killReward > 0f && SkillEffects.OxygenOnKillBonus > 0f)
                totalOxygen += oxygenSystem.killReward;
            if (totalOxygen > 0f)
                oxygenSystem.ChangeOxygen(totalOxygen);
        }
        // Enemy killed: update ores and oxygen (log suppressed per request)
        RefreshOreUI();
    }

    void AddCollectedOre(string oreId, int amount)
    {
        if (amount <= 0)
            return;

        oresCollectedThisRun += amount;
        if (!string.IsNullOrEmpty(oreId))
        {
            if (!oresCollectedById.ContainsKey(oreId)) oresCollectedById[oreId] = 0;
            oresCollectedById[oreId] += amount;
        }
        if (oreId == "copper")
            oresCollectedCopper += amount;
        else if (oreId == "stone")
            oresCollectedStone += amount;
    }

    void ApplyWaveEndOreBonus()
    {
        float multiplier = WeaponTraitRuntime.GetWaveOreRewardMultiplier(weaponId);
        if (multiplier <= 1f)
        {
            SnapshotWaveOreCounts();
            return;
        }

        var ids = new List<string>(oresCollectedById.Keys);
        for (int i = 0; i < ids.Count; i++)
        {
            string oreId = ids[i];
            int current = oresCollectedById[oreId];
            waveStartOreCounts.TryGetValue(oreId, out int start);
            int earnedThisWave = Mathf.Max(0, current - start);
            waveBonusRemainders.TryGetValue(oreId, out float remainder);
            float exactBonus = earnedThisWave * (multiplier - 1f) + remainder;
            int bonus = Mathf.Max(0, Mathf.FloorToInt(exactBonus));
            waveBonusRemainders[oreId] = exactBonus - bonus;
            AddCollectedOre(oreId, bonus);
        }
        SnapshotWaveOreCounts();
        RefreshOreUI();
    }

    void SnapshotWaveOreCounts()
    {
        waveStartOreCounts.Clear();
        foreach (var pair in oresCollectedById)
            waveStartOreCounts[pair.Key] = pair.Value;
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
        var flow = FindObjectOfType<GameFlowManager>();
        if (flow != null) flow.ShowEnd();
    }

    void RefreshWaveUI()
    {
        if (waveText != null) waveText.text = $"{currentWave} Wave";
        if (waveTimerText != null) waveTimerText.text = FormatMinuteSecond(RemainingWaveSeconds);
    }

    string FormatMinuteSecond(int totalSeconds)
    {
        totalSeconds = Mathf.Max(0, totalSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes} : {seconds}";
    }

    void RefreshOreUI()
    {
        if (oreText != null) oreText.text = $"Ore: {oresCollectedThisRun}";
    }

    void AutoFire()
    {
        if (projectilePrefab == null || firePoint == null) return;
        if (runWeaponDisplay == null)
            runWeaponDisplay = RunWeaponDisplay.EnsureExists(this);

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

        if (runWeaponDisplay != null)
        {
            runWeaponDisplay.SetAimRotation(firePoint.rotation);
            runWeaponDisplay.RefreshMuzzlePosition();
        }

        var shotModifiers = WeaponTraitRuntime.OnWeaponFired(weaponId);
        if (shotModifiers.destroyImmediately)
            return;

        int projectileCount = Mathf.Max(1, baseProjectileCount);
        float centerOffset = (projectileCount - 1) * 0.5f;
        for (int i = 0; i < projectileCount; i++)
        {
            float angleOffset = (i - centerOffset) * multiProjectileSpreadAngle;
            SpawnProjectile(angleOffset, shotModifiers);
        }
    }

    void SpawnProjectile(float angleOffset, WeaponTraitRuntime.ShotModifiers shotModifiers)
    {
        Quaternion shotRotation = firePoint.rotation * Quaternion.Euler(0f, 0f, angleOffset);
        var pgo = Instantiate(projectilePrefab, firePoint.position, shotRotation) as GameObject;
        if (pgo != null)
        {
            pgo.SetActive(true);
            // Ensure projectile's rotation matches firePoint so its local +X moves toward target
            pgo.transform.rotation = shotRotation;
            // Force visible settings
            pgo.transform.position = new Vector3(pgo.transform.position.x, pgo.transform.position.y, 0f);
            var proj = pgo.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.damage = Mathf.Max(0.01f,
                    baseProjectileDamage *
                    SkillEffects.DamageMultiplier *
                    proj.damageMultiplier *
                    shotModifiers.damageMultiplier);
                proj.pierceCount = Mathf.Max(0, baseProjectilePierceCount);
                proj.speed = baseProjectileSpeed * GetWorldScale();
                proj.lifeTime = Mathf.Max(baseProjectileLifeTime, fireRange / Mathf.Max(1f, proj.speed) + 0.25f);
                proj.weaponId = weaponId;
                proj.maxRange = fireRange;
                proj.SetMoveDirection(shotRotation * Vector3.right);
            }
            bool isTempProjectile = projectilePrefab.name.Contains("_Temp");
            if ((usingRuntimeProjectilePrefab && !preserveProjectileTemplateScale) || isTempProjectile)
                pgo.transform.localScale = Vector3.one * Mathf.Max(1f, GetWorldScale() * runtimeProjectileVisualScale);
            else
                pgo.transform.localScale = projectilePrefab.transform.localScale;
            var psr = pgo.GetComponent<SpriteRenderer>();
            if (psr != null)
            {
                psr.enabled = true;
                try { psr.sortingOrder = Mathf.Max(psr.sortingOrder, 1000); psr.maskInteraction = SpriteMaskInteraction.None; } catch (System.Exception) { }
            }
        }
    }

    void ApplyWeaponConfig()
    {
        weaponId = WeaponPanelManager.GetEquippedWeaponId();
        var w = GameData.GetWeapon(weaponId);
        if (w != null)
        {
            float upgradedFireInterval = WeaponPanelManager.GetEffectiveFireInterval(w);
            if (upgradedFireInterval > 0f) fireInterval = upgradedFireInterval;
            if (w.detectRange > 0f) baseFireRange = w.detectRange;
            if (w.bulletSpeed > 0f) baseProjectileSpeed = w.bulletSpeed;
            baseProjectileDamage = WeaponPanelManager.GetEffectiveDamage(w);
            baseProjectilePierceCount = WeaponPanelManager.GetEffectivePierceCount(w);
            baseProjectileCount = WeaponPanelManager.GetEffectiveProjectileCount(w);
            multiProjectileSpreadAngle = WeaponPanelManager.GetEffectiveSpreadAngle(w, baseMultiProjectileSpreadAngle);
        }
        baseFireInterval = fireInterval;
        fireRange = GetScaledRange(baseFireRange);
    }

    void ApplyProjectileStats()
    {
        var w = GameData.GetWeapon(weaponId);
        if (projectilePrefab == null) return;
        var proj = projectilePrefab.GetComponent<Projectile>();
        if (proj == null) return;
        if (w != null)
        {
            if (w.bulletSpeed > 0f) baseProjectileSpeed = w.bulletSpeed;
            baseProjectileDamage = WeaponPanelManager.GetEffectiveDamage(w);
            baseProjectilePierceCount = WeaponPanelManager.GetEffectivePierceCount(w);
            baseProjectileCount = WeaponPanelManager.GetEffectiveProjectileCount(w);
            multiProjectileSpreadAngle = WeaponPanelManager.GetEffectiveSpreadAngle(w, baseMultiProjectileSpreadAngle);
        }
        var pdef = GameData.GetProjectile(projectileId);
        if (pdef != null)
        {
            if ((w == null || w.bulletSpeed <= 0f) && pdef.speed > 0f) baseProjectileSpeed = pdef.speed;
            if (pdef.lifeTime > 0f) baseProjectileLifeTime = pdef.lifeTime;
            if (pdef.damageMult > 0f) proj.damageMultiplier = pdef.damageMult;
        }
        proj.damage = baseProjectileDamage;
        proj.pierceCount = baseProjectilePierceCount;
        proj.speed = baseProjectileSpeed * GetWorldScale();
        proj.lifeTime = Mathf.Max(baseProjectileLifeTime, fireRange / Mathf.Max(1f, proj.speed) + 0.25f);
    }

    float GetScaledRange(float designRange)
    {
        return Mathf.Max(designRange * GetWorldScale(), designRange);
    }

    float GetWorldScale()
    {
        var player = firePoint != null && firePoint.parent != null
            ? firePoint.parent
            : FindObjectOfType<PlayerController>()?.transform;
        var planet = GameObject.Find("Planet")?.transform;
        if (player != null && planet != null)
        {
            float orbitRadius = Vector3.Distance(player.position, planet.position);
            if (orbitRadius > 0.01f)
                return Mathf.Max(1f, orbitRadius / Mathf.Max(0.01f, referenceOrbitRadius));
        }
        return 1f;
    }

    Transform FindNearestEnemy(Vector3 from, float range)
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        Transform best = null;
        float bestDist = Mathf.Max(range, fireRange);

        if (BossBattleSession.IsBossBattle)
        {
            var boss = FindFirstObjectByType<BossController>();
            if (boss != null && boss.gameObject.activeInHierarchy && boss.CurrentHP > 0)
                return boss.transform;
        }

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

public class RunWeaponDisplay : MonoBehaviour
{
    private WaveManager waveManager;
    private PlayerController player;
    private SpriteRenderer weaponRenderer;
    private string displayedWeaponId;

    public static RunWeaponDisplay EnsureExists(WaveManager manager)
    {
        if (manager == null || SceneManager.GetActiveScene().name != "RunScene")
            return null;

        SpriteRenderer sceneWeaponRenderer = FindSceneWeaponRenderer();
        if (sceneWeaponRenderer == null)
        {
            Debug.LogWarning("RunWeaponDisplay: Player 하위의 WeaponVisual SpriteRenderer를 찾지 못했습니다.");
            return null;
        }

        var display = sceneWeaponRenderer.GetComponent<RunWeaponDisplay>();
        if (display == null)
            display = sceneWeaponRenderer.gameObject.AddComponent<RunWeaponDisplay>();

        display.Configure(manager, sceneWeaponRenderer);
        return display;
    }

    public void Configure(WaveManager manager, SpriteRenderer sceneWeaponRenderer)
    {
        waveManager = manager;
        player = FindFirstObjectByType<PlayerController>();
        weaponRenderer = sceneWeaponRenderer;

        RefreshWeaponSprite(true);
        if (waveManager.firePoint != null)
            SetAimRotation(waveManager.firePoint.rotation);
        RefreshMuzzlePosition();
    }

    void LateUpdate()
    {
        if (waveManager == null)
            return;

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        RefreshWeaponSprite(false);
        RefreshMuzzlePosition();
    }

    public void RefreshMuzzlePosition()
    {
        if (waveManager == null || waveManager.firePoint == null || player == null)
            return;

        if (weaponRenderer != null && weaponRenderer.sprite != null)
        {
            Bounds spriteBounds = weaponRenderer.sprite.bounds;
            Vector3 localMuzzle = new Vector3(
                spriteBounds.max.x,
                spriteBounds.center.y,
                0f);
            Vector3 worldMuzzle = weaponRenderer.transform.TransformPoint(localMuzzle);
            worldMuzzle.z = player.transform.position.z;
            waveManager.firePoint.position = worldMuzzle;
            return;
        }

    }

    void RefreshWeaponSprite(bool force)
    {
        if (weaponRenderer == null)
            return;

        string weaponId = WeaponPanelManager.GetEquippedWeaponId();
        if (!force && displayedWeaponId == weaponId)
            return;

        var weapon = GameData.GetWeapon(weaponId);
        string iconKey = weapon != null && !string.IsNullOrWhiteSpace(weapon.iconKey)
            ? weapon.iconKey.Trim()
            : $"icon_{weaponId}";
        Sprite sprite = LoadWeaponSprite(iconKey);

        if (sprite == null)
        {
            Debug.LogWarning($"RunWeaponDisplay: Resources/weapon/{iconKey} 무기 이미지를 찾지 못했습니다.");
            weaponRenderer.enabled = false;
            displayedWeaponId = weaponId;
            return;
        }

        weaponRenderer.sprite = sprite;
        weaponRenderer.enabled = true;
        displayedWeaponId = weaponId;
    }

    public void SetAimRotation(Quaternion shotRotation)
    {
        if (weaponRenderer == null)
            return;

        weaponRenderer.flipX = false;
        weaponRenderer.flipY = false;
        weaponRenderer.transform.rotation = shotRotation;
    }

    static SpriteRenderer FindSceneWeaponRenderer()
    {
        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        if (playerController == null)
            return null;

        SpriteRenderer[] renderers = playerController.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.gameObject == playerController.gameObject)
                continue;

            if (renderer.name == "WeaponVisual")
                return renderer;
        }

        return null;
    }

    static Sprite LoadWeaponSprite(string iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey))
            return null;

        Sprite sprite = Resources.Load<Sprite>($"weapon/{iconKey}");
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>($"weapon/{iconKey}");
        if (sprites == null || sprites.Length == 0)
            return null;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null &&
                (sprites[i].name == iconKey || sprites[i].name == $"{iconKey}_0"))
                return sprites[i];
        }

        return sprites[0];
    }

}
