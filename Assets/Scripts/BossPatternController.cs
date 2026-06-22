using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BossPatternController : MonoBehaviour
{
    const float SwingWarningSeconds = 0.7f;
    const float SwingTrackingSeconds = 0.2f;
    const float SwingScaleMultiplier = 1.75f;
    const float OxygenDamageMultiplier = 1.5f;
    const float HazardWarningBlinkSeconds = 0.15f;
    const int HazardWarningBlinkCount = 2;

    BossController boss;
    OxygenSystem oxygen;
    EnemySpawner spawner;
    PlayerController player;
    Transform planet;
    Transform patternsRoot;
    RectTransform swingRoot;
    Canvas swingCanvas;
    Canvas bulletCanvas;
    GameObject warningArea;
    GameObject attackEffect;
    GameObject hitArea;
    GameObject bossBulletTemplate;
    GameObject leftCircle;
    GameObject rightCircle;
    GameObject leftWarning;
    GameObject rightWarning;
    TMP_Text debuffType;
    TMP_Text debuffTime;
    Coroutine patternLoop;
    bool hazardActive;
    bool debuffActive;
    Vector2 swingRootOffsetFromBoss;
    bool hasSwingOffset;
    bool swingScaleApplied;
    readonly List<Sprite> attackFrames = new List<Sprite>();
    readonly List<GameObject> activeBossBullets = new List<GameObject>();

    public void Begin(BossController owner)
    {
        boss = owner;
        BindSceneObjects();
        LoadAttackFrames();
        StopPatterns();
        patternLoop = StartCoroutine(PatternLoop());
    }

    public void StopPatterns()
    {
        StopAllCoroutines();
        patternLoop = null;
        hazardActive = false;
        debuffActive = false;
        BossBattleSession.ClearDebuff();
        SetActive(warningArea, false);
        SetActive(attackEffect, false);
        SetActive(leftCircle, false);
        SetActive(rightCircle, false);
        SetActive(leftWarning, false);
        SetActive(rightWarning, false);
        ClearBossBullets();
        SetDebuffUI(false);
        if (boss != null)
        {
            boss.SetPatternAnimationLocked(false);
            boss.SetTargetable(true);
        }
    }

    IEnumerator PatternLoop()
    {
        yield return new WaitForSeconds(GetPatternCooldown());
        while (boss != null && BossBattleSession.IsBossBattle && boss.CurrentHP > 0f)
        {
            int maxPattern = boss.Phase >= 2 ? 6 : 5;
            int pattern = Random.Range(0, maxPattern);
            if (pattern == 3 && hazardActive)
                continue;
            if (pattern == 5 && debuffActive)
                continue;

            switch (pattern)
            {
                case 0:
                    yield return TeleportPattern();
                    break;
                case 1:
                    yield return SummonPattern();
                    break;
                case 2:
                    yield return BulletHellPattern();
                    break;
                case 3:
                    yield return HazardPattern();
                    break;
                case 4:
                    yield return SwingPattern();
                    break;
                case 5:
                    yield return DebuffPattern();
                    break;
            }

            yield return new WaitForSeconds(GetPatternCooldown());
        }
    }

    float GetPatternCooldown()
    {
        return boss != null && boss.Phase >= 2 ? 0.75f : 1.5f;
    }

    IEnumerator TeleportPattern()
    {
        if (boss == null)
            yield break;

        boss.SetTargetable(false);
        yield return FadeBoss(1f, 0f, 0.25f);

        if (boss.Phase >= 2)
        {
            List<Enemy> summoned = new List<Enemy>();
            for (int i = 0; i < 5; i++)
            {
                Enemy iron = spawner != null ? spawner.SpawnBossEnemy("iron") : null;
                Enemy diamond = spawner != null ? spawner.SpawnBossEnemy("diamond") : null;
                if (iron != null) summoned.Add(iron);
                if (diamond != null) summoned.Add(diamond);
                yield return new WaitForSeconds(0.15f);
            }

            float healTimer = 0f;
            while (HasLivingEnemies(summoned))
            {
                healTimer += Time.deltaTime;
                if (healTimer >= 3f)
                {
                    healTimer -= 3f;
                    boss.HealByMaxRatio(0.05f);
                }
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(0.35f);
        }

        MoveBossToOppositeSide();
        yield return FadeBoss(0f, 1f, 0.25f);
        boss.SetTargetable(true);
    }

    IEnumerator SummonPattern()
    {
        string[] ores = { "stone", "copper", "iron", "gold", "diamond" };
        int eachCount = boss != null && boss.Phase >= 2 ? 5 : 2;
        float delay = boss != null && boss.Phase >= 2 ? 0.15f : 0.3f;

        for (int oreIndex = 0; oreIndex < ores.Length; oreIndex++)
        {
            for (int i = 0; i < eachCount; i++)
            {
                if (spawner != null)
                    spawner.SpawnBossEnemy(ores[oreIndex]);
                yield return new WaitForSeconds(delay);
            }
        }
    }

    IEnumerator BulletHellPattern()
    {
        if (boss == null || player == null || bossBulletTemplate == null || bulletCanvas == null)
            yield break;

        Vector2 origin = boss.transform.position;
        Vector2 aimDirection = (Vector2)player.transform.position - origin;
        if (aimDirection.sqrMagnitude <= 0.0001f)
            aimDirection = Vector2.down;

        // The player is sampled once. Every volley keeps this initial direction.
        float fixedAimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        for (int volley = 0; volley < 20; volley++)
        {
            float volleyOffset;
            if (boss.Phase >= 2)
            {
                int step = volley / 2 + 1;
                volleyOffset = (volley % 2 == 0 ? 1f : -1f) * step * 5f;
            }
            else
            {
                volleyOffset = volley * 5f;
            }

            for (int directionIndex = 0; directionIndex < 5; directionIndex++)
            {
                float angle = fixedAimAngle + (directionIndex - 2) * 18f + volleyOffset;
                SpawnBossBullet(origin, angle);
            }
            yield return new WaitForSeconds(0.16f);
        }
    }

    IEnumerator HazardPattern()
    {
        bool useLeft = Random.value < 0.5f;
        GameObject selected = useLeft ? leftCircle : rightCircle;
        GameObject selectedWarning = useLeft ? leftWarning : rightWarning;
        if (selected == null)
            yield break;

        hazardActive = true;
        selected.SetActive(true);
        Graphic mainGraphic = selected.GetComponent<Graphic>();
        SpriteRenderer mainRenderer = selected.GetComponent<SpriteRenderer>();
        if (mainGraphic != null) mainGraphic.enabled = false;
        if (mainRenderer != null) mainRenderer.enabled = false;

        // Two 0.3-second blinks: 0.15 on + 0.15 off, repeated twice.
        for (int i = 0; i < HazardWarningBlinkCount; i++)
        {
            SetActive(selectedWarning, true);
            yield return new WaitForSeconds(HazardWarningBlinkSeconds);
            SetActive(selectedWarning, false);
            yield return new WaitForSeconds(HazardWarningBlinkSeconds);
        }

        if (mainGraphic != null) mainGraphic.enabled = true;
        if (mainRenderer != null) mainRenderer.enabled = true;

        float duration = boss != null && boss.Phase >= 2 ? 5f : 10f;
        float oxygenRatio = boss != null && boss.Phase >= 2 ? 0.03f : 0.01f;
        StartCoroutine(RunHazardLifetime(selected, selectedWarning, duration, oxygenRatio));
    }

    IEnumerator RunHazardLifetime(
        GameObject selected,
        GameObject selectedWarning,
        float duration,
        float oxygenRatio)
    {
        float elapsed = 0f;
        float damageTimer = 0f;
        ApplyHazardDamage(selected, oxygenRatio);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            damageTimer += Time.deltaTime;
            if (damageTimer >= 0.5f)
            {
                damageTimer -= 0.5f;
                ApplyHazardDamage(selected, oxygenRatio);
            }
            yield return null;
        }

        selected.SetActive(false);
        SetActive(selectedWarning, false);
        hazardActive = false;
    }

    void ApplyHazardDamage(GameObject area, float oxygenRatio)
    {
        if (oxygen != null && IsPlayerInside(area))
            oxygen.ChangeOxygen(
                -oxygen.MaxOxygen * oxygenRatio * OxygenDamageMultiplier);
    }

    IEnumerator SwingPattern()
    {
        if (boss == null || player == null || attackEffect == null)
            yield break;

        boss.SetPatternAnimationLocked(true);
        SetActive(warningArea, false);
        SetActive(attackEffect, false);

        Vector3 lockedTargetPosition = player.transform.position;
        yield return PlaySwingWindupAndLockTarget(
            new[] { 42, 43, 44 },
            SwingWarningSeconds,
            position => lockedTargetPosition = position);

        for (int strike = 0; strike < 2; strike++)
        {
            PositionSwingEffectAtWorldPoint(lockedTargetPosition);
            yield return PlaySwingStrike();
            if (strike == 0)
                yield return new WaitForSeconds(0.12f);
        }

        SetActive(attackEffect, false);
        boss.SetPatternAnimationLocked(false);
    }

    IEnumerator PlaySwingWindupAndLockTarget(
        int[] frameIndices,
        float duration,
        System.Action<Vector3> setLockedPosition)
    {
        if (boss == null || player == null || frameIndices == null || frameIndices.Length == 0)
            yield break;

        float elapsed = 0f;
        int previousFrame = -1;
        Vector3 lockedPosition = player.transform.position;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (elapsed <= SwingTrackingSeconds)
            {
                lockedPosition = player.transform.position;
                PositionSwingEffectAtWorldPoint(lockedPosition);
            }

            int frameIndex = Mathf.Min(
                frameIndices.Length - 1,
                Mathf.FloorToInt(elapsed / duration * frameIndices.Length));
            if (frameIndex != previousFrame)
            {
                Sprite frame = boss.FindBossFrame(frameIndices[frameIndex]);
                if (boss.Visual != null && frame != null)
                    boss.Visual.sprite = frame;
                previousFrame = frameIndex;
            }
            yield return null;
        }

        setLockedPosition?.Invoke(lockedPosition);
    }

    IEnumerator PlaySwingStrike()
    {
        if (attackEffect == null || boss == null)
            yield break;

        SetActive(attackEffect, true);
        bool damaged = false;

        for (int i = 0; i < 5; i++)
        {
            Sprite bossFrame = boss.FindBossFrame(Mathf.Min(45 + i, 48));
            if (boss.Visual != null && bossFrame != null)
                boss.Visual.sprite = bossFrame;
            SetAttackEffectFrame(i);
            if (!damaged && IsPlayerInside(hitArea != null ? hitArea : attackEffect))
            {
                float ratio = boss.Phase >= 2 ? 0.30f : 0.20f;
                oxygen?.ChangeOxygen(
                    -oxygen.MaxOxygen * ratio * OxygenDamageMultiplier);
                damaged = true;
            }
            yield return new WaitForSeconds(0.12f);
        }

        SetActive(attackEffect, false);
    }

    IEnumerator DebuffPattern()
    {
        if (boss == null || boss.Phase < 2)
            yield break;

        debuffActive = true;
        bool damageDebuff = Random.value < 0.5f;
        float ratio = Random.Range(0.20f, 0.4001f);
        float duration = 10f;

        if (damageDebuff)
            BossBattleSession.SetDamageDebuff(ratio);
        else
            BossBattleSession.SetFireRateDebuff(ratio);

        SetDebuffUI(true);
        if (debuffType != null)
            debuffType.text = $"{(damageDebuff ? "공격력" : "공격 속도")} {ratio * 100f:0}% 감소";

        StartCoroutine(RunDebuffLifetime(duration));
        yield break;
    }

    IEnumerator RunDebuffLifetime(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (debuffTime != null)
                debuffTime.text = $"{Mathf.Max(0f, duration - elapsed):0.0}s";
            yield return null;
        }

        BossBattleSession.ClearDebuff();
        SetDebuffUI(false);
        debuffActive = false;
    }

    IEnumerator PlayBossFrames(int[] indices, float totalDuration)
    {
        if (boss == null || indices == null || indices.Length == 0)
            yield break;

        float frameDuration = totalDuration / indices.Length;
        for (int i = 0; i < indices.Length; i++)
        {
            Sprite frame = boss.FindBossFrame(indices[i]);
            if (boss.Visual != null && frame != null)
                boss.Visual.sprite = frame;
            yield return new WaitForSeconds(frameDuration);
        }
    }

    IEnumerator FadeBoss(float from, float to, float duration)
    {
        SpriteRenderer[] renderers = boss != null
            ? boss.GetComponentsInChildren<SpriteRenderer>(true)
            : new SpriteRenderer[0];
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            for (int i = 0; i < renderers.Length; i++)
            {
                Color color = renderers[i].color;
                color.a = alpha;
                renderers[i].color = color;
            }
            yield return null;
        }
    }

    void SpawnBossBullet(Vector2 worldOrigin, float angleDegrees)
    {
        GameObject bullet = Instantiate(bossBulletTemplate, bossBulletTemplate.transform.parent);
        bullet.name = "BossBullet";
        bullet.SetActive(true);
        activeBossBullets.Add(bullet);
        RectTransform rect = bullet.GetComponent<RectTransform>();
        if (rect == null)
        {
            activeBossBullets.Remove(bullet);
            Destroy(bullet);
            return;
        }
        rect.anchoredPosition = WorldToCanvasPoint(worldOrigin, bulletCanvas);
        StartCoroutine(MoveBossBullet(bullet, worldOrigin, angleDegrees));
    }

    IEnumerator MoveBossBullet(GameObject bullet, Vector2 worldOrigin, float angleDegrees)
    {
        RectTransform rect = bullet.GetComponent<RectTransform>();
        Vector2 direction = new Vector2(
            Mathf.Cos(angleDegrees * Mathf.Deg2Rad),
            Mathf.Sin(angleDegrees * Mathf.Deg2Rad));
        Vector2 worldPosition = worldOrigin;
        float worldSpeed = GetBulletWorldSpeed();
        float elapsed = 0f;
        bool hit = false;
        while (bullet != null && elapsed < 5f)
        {
            elapsed += Time.deltaTime;
            worldPosition += direction * worldSpeed * Time.deltaTime;
            Canvas canvas = bullet.GetComponentInParent<Canvas>();
            rect.anchoredPosition = WorldToCanvasPoint(worldPosition, canvas);

            Vector2 bulletScreenPoint = Camera.main != null
                ? Camera.main.WorldToScreenPoint(worldPosition)
                : worldPosition;
            Vector2 playerScreenPoint = Camera.main != null
                ? Camera.main.WorldToScreenPoint(player.transform.position)
                : player.transform.position;
            if (!hit && Vector2.Distance(bulletScreenPoint, playerScreenPoint) <= 40f)
            {
                float ratio = boss != null && boss.Phase >= 2 ? 0.10f : 0.05f;
                if (oxygen != null)
                {
                    oxygen.ChangeOxygen(
                        -oxygen.currentOxygen * ratio * OxygenDamageMultiplier);
                }
                hit = true;
                activeBossBullets.Remove(bullet);
                Destroy(bullet);
                yield break;
            }
            yield return null;
        }
        activeBossBullets.Remove(bullet);
        if (bullet != null)
            Destroy(bullet);
    }

    void ClearBossBullets()
    {
        for (int i = activeBossBullets.Count - 1; i >= 0; i--)
        {
            if (activeBossBullets[i] != null)
                Destroy(activeBossBullets[i]);
        }
        activeBossBullets.Clear();
    }

    float GetBulletWorldSpeed()
    {
        Camera camera = Camera.main;
        if (camera != null && camera.orthographic && Screen.height > 0)
            return 500f * (camera.orthographicSize * 2f / Screen.height);
        return 100f;
    }

    void MoveBossToOppositeSide()
    {
        if (boss == null)
            return;

        float centerX = planet != null ? planet.position.x : 0f;
        float direction = boss.transform.position.x <= centerX ? 1f : -1f;
        float targetX = boss.transform.position.x + direction * 100f;

        Camera camera = Camera.main;
        if (camera != null)
        {
            float minX = camera.ViewportToWorldPoint(new Vector3(0.08f, 0.5f, Mathf.Abs(camera.transform.position.z))).x;
            float maxX = camera.ViewportToWorldPoint(new Vector3(0.92f, 0.5f, Mathf.Abs(camera.transform.position.z))).x;
            targetX = Mathf.Clamp(targetX, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));
        }

        Vector3 position = boss.transform.position;
        position.x = targetX;
        boss.transform.position = position;
    }

    bool HasLivingEnemies(List<Enemy> enemies)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null)
                return true;
        }
        return false;
    }

    bool IsPlayerInside(GameObject areaObject)
    {
        if (areaObject == null || player == null)
            return false;

        RectTransform area = areaObject.GetComponent<RectTransform>();
        if (area != null)
        {
            Canvas canvas = area.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 screenPoint = Camera.main != null
                ? Camera.main.WorldToScreenPoint(player.transform.position)
                : player.transform.position;
            return RectTransformUtility.RectangleContainsScreenPoint(area, screenPoint, uiCamera);
        }

        Collider2D collider = areaObject.GetComponent<Collider2D>();
        return collider != null && collider.OverlapPoint(player.transform.position);
    }

    Vector2 WorldToCanvasPoint(Vector3 worldPosition, Canvas canvas)
    {
        if (canvas == null)
            return Vector2.zero;

        RectTransform canvasRect = canvas.transform as RectTransform;
        Vector2 screenPoint = Camera.main != null
            ? (Vector2)Camera.main.WorldToScreenPoint(worldPosition)
            : (Vector2)worldPosition;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 local);
        return local;
    }

    void RefreshSwingPosition()
    {
        if (boss == null || swingRoot == null || swingCanvas == null)
            return;

        Vector2 bossPoint = WorldToCanvasPoint(boss.transform.position, swingCanvas);
        swingRoot.anchoredPosition = bossPoint + (hasSwingOffset ? swingRootOffsetFromBoss : Vector2.zero);
    }

    void PositionSwingEffectAtWorldPoint(Vector3 worldPosition)
    {
        if (swingRoot == null || swingCanvas == null)
            return;

        Vector2 targetPoint = WorldToCanvasPoint(worldPosition, swingCanvas);
        RectTransform effectRect = attackEffect != null
            ? attackEffect.GetComponent<RectTransform>()
            : null;
        Vector2 effectLocalOffset = effectRect != null
            ? effectRect.anchoredPosition
            : Vector2.zero;
        swingRoot.anchoredPosition = targetPoint - effectLocalOffset;
    }

    void SetAttackEffectFrame(int index)
    {
        if (attackEffect == null || attackFrames.Count == 0)
            return;
        Image image = attackEffect.GetComponent<Image>();
        if (image != null)
            image.sprite = attackFrames[Mathf.Clamp(index, 0, attackFrames.Count - 1)];
    }

    void LoadAttackFrames()
    {
        attackFrames.Clear();
        for (int i = 1; i <= 5; i++)
        {
            string path = $"animate/attack/SP301_{i:00}";
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Sprite[] sprites = Resources.LoadAll<Sprite>(path);
                if (sprites.Length > 0)
                    sprite = sprites[0];
            }
            if (sprite != null)
                attackFrames.Add(sprite);
        }
    }

    void BindSceneObjects()
    {
        oxygen = FindFirstObjectByType<OxygenSystem>();
        spawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        if (spawner != null)
            spawner.PrepareForBossSpawns();
        player = FindFirstObjectByType<PlayerController>();
        GameObject planetObject = FindInScene("Planet");
        planet = planetObject != null ? planetObject.transform : null;

        GameObject patterns = FindInScene("BossPatterns");
        patternsRoot = patterns != null ? patterns.transform : null;

        Transform swing = FindChild(patternsRoot, "SwingAttack");
        swingRoot = swing as RectTransform;
        warningArea = FindChildObject(swing, "WarningArea");
        attackEffect = FindChildObject(swing, "AttackEffect");
        hitArea = FindChildObjectLoose(swing, "HitArea");
        swingCanvas = swingRoot != null ? swingRoot.GetComponentInParent<Canvas>() : null;
        ApplySwingScale();

        Transform bullet = FindChild(patternsRoot, "BossBulletTemplate");
        bossBulletTemplate = bullet != null ? bullet.gameObject : null;
        bulletCanvas = bullet != null ? bullet.GetComponentInParent<Canvas>() : null;

        leftCircle = FindChildObject(patternsRoot, "leftCircle");
        rightCircle = FindChildObject(patternsRoot, "rightCircle");
        leftWarning = FindChildObject(patternsRoot, "leftwarning");
        rightWarning = FindChildObject(patternsRoot, "rightwarning");
        if (leftWarning == null && leftCircle != null)
            leftWarning = FindChildObject(leftCircle.transform, "warning");
        if (rightWarning == null && rightCircle != null)
            rightWarning = FindChildObject(rightCircle.transform, "warning");

        if (boss != null && swingRoot != null && swingCanvas != null)
        {
            swingRootOffsetFromBoss =
                swingRoot.anchoredPosition - WorldToCanvasPoint(boss.transform.position, swingCanvas);
            hasSwingOffset = true;
        }
        else
        {
            hasSwingOffset = false;
        }

        GameObject panel = FindInScene("bossPanel");
        debuffType = FindTmp(panel, "debuffType");
        debuffTime = FindTmp(panel, "debuffTime");

        SetActive(warningArea, false);
        SetActive(attackEffect, false);
        SetActive(bossBulletTemplate, false);
        SetActive(leftCircle, false);
        SetActive(rightCircle, false);
        SetActive(leftWarning, false);
        SetActive(rightWarning, false);
        SetDebuffUI(false);
    }

    void ApplySwingScale()
    {
        if (swingScaleApplied)
            return;

        RectTransform effectRect = attackEffect != null
            ? attackEffect.GetComponent<RectTransform>()
            : null;
        if (effectRect != null)
            effectRect.sizeDelta *= SwingScaleMultiplier;

        RectTransform hitRect = hitArea != null
            ? hitArea.GetComponent<RectTransform>()
            : null;
        if (hitRect != null)
        {
            hitRect.sizeDelta *= SwingScaleMultiplier;
        }
        swingScaleApplied = true;
    }

    void SetDebuffUI(bool active)
    {
        if (debuffType != null) debuffType.gameObject.SetActive(active);
        if (debuffTime != null) debuffTime.gameObject.SetActive(active);
    }

    static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    static TMP_Text FindTmp(GameObject root, string targetName)
    {
        if (root == null)
            return null;
        Transform found = FindChild(root.transform, targetName);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    static GameObject FindInScene(string targetName)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChild(roots[i].transform, targetName);
            if (found != null)
                return found.gameObject;
        }
        return null;
    }

    static Transform FindChild(Transform root, string targetName)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }
        return null;
    }

    static GameObject FindChildObject(Transform root, string targetName)
    {
        Transform found = FindChild(root, targetName);
        return found != null ? found.gameObject : null;
    }

    static GameObject FindChildObjectLoose(Transform root, string targetName)
    {
        Transform found = FindChildLoose(root, targetName);
        return found != null ? found.gameObject : null;
    }

    static Transform FindChildLoose(Transform root, string targetName)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name.Trim(), targetName, System.StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildLoose(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }
        return null;
    }
}
