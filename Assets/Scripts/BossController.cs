using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BossController : MonoBehaviour
{
    public int maxHP = 150000;
    public string bossName = "보스";
    public float introDuration = 1f;
    public float idleFramesPerSecond = 4f;

    float currentHP;
    Image hpBar;
    TMP_Text nameText;
    Color baseColor = Color.white;
    SpriteRenderer visual;
    bool defeated;
    int phase = 1;
    Collider2D[] hitColliders;
    Sprite[] idleFrames;
    float idleFrameTimer;
    int idleFrameIndex;
    Coroutine hitFlashRoutine;
    BossPatternController patternController;
    bool patternAnimationLocked;
    bool targetable = true;

    public float CurrentHP => currentHP;
    public float HealthRatio => maxHP > 0 ? Mathf.Clamp01((float)currentHP / maxHP) : 0f;

    void Awake()
    {
        hitColliders = GetComponentsInChildren<Collider2D>(true);
        visual = GetComponent<SpriteRenderer>();
        if (visual == null)
            visual = GetComponentInChildren<SpriteRenderer>(true);
        if (visual != null)
            baseColor = visual.color;
        LoadIdleFrames();
    }

    public void BeginBattle()
    {
        maxHP = 150000;
        currentHP = maxHP;
        defeated = false;
        phase = 1;
        BossBattleSession.SetCombatPaused(true);
        EnsureVisible();
        SetHitCollidersEnabled(false);
        BindUI();
        RefreshUI();
        StartCoroutine(PlayIntro());
    }

    void Update()
    {
        if (patternAnimationLocked || visual == null || idleFrames == null || idleFrames.Length == 0)
            return;

        idleFrameTimer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(0.01f, idleFramesPerSecond);
        if (idleFrameTimer < frameDuration)
            return;

        idleFrameTimer %= frameDuration;
        idleFrameIndex = (idleFrameIndex + 1) % idleFrames.Length;
        visual.sprite = idleFrames[idleFrameIndex];
    }

    IEnumerator PlayIntro()
    {
        float duration = Mathf.Max(0.01f, introDuration);
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = renderers[i].color;
            color.a = 0f;
            renderers[i].color = color;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color color = renderers[i].color;
                color.a = alpha;
                renderers[i].color = color;
            }
            yield return null;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color color = renderers[i].color;
            color.a = 1f;
            renderers[i].color = color;
        }

        if (visual != null)
            baseColor = visual.color;
        SetHitCollidersEnabled(true);
        BossBattleSession.SetCombatPaused(false);
        patternController = GetComponent<BossPatternController>();
        if (patternController == null)
            patternController = gameObject.AddComponent<BossPatternController>();
        patternController.Begin(this);
    }

    void LoadIdleFrames()
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>("character/boss");
        Sprite boss0 = null;
        Sprite boss1 = null;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i].name == "boss_0")
                boss0 = sprites[i];
            else if (sprites[i].name == "boss_1")
                boss1 = sprites[i];
        }

        if (boss0 != null && boss1 != null)
        {
            idleFrames = new[] { boss0, boss1 };
            idleFrameIndex = 0;
            idleFrameTimer = 0f;
            if (visual != null)
                visual.sprite = idleFrames[0];
        }
    }

    void EnsureVisible()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            renderer.enabled = true;
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 2);

            Color color = renderer.color;
            color.a = 1f;
            renderer.color = color;
        }

        if (visual != null)
            baseColor = visual.color;
    }

    public void TakeDamage(float damage)
    {
        if (!targetable || defeated || damage <= 0)
            return;

        currentHP = Mathf.Max(0, currentHP - damage);
        RefreshUI();

        if (visual != null)
        {
            if (hitFlashRoutine != null)
                StopCoroutine(hitFlashRoutine);
            hitFlashRoutine = StartCoroutine(FlashHit());
        }

        if (currentHP <= 0)
            Defeat();
    }

    IEnumerator FlashHit()
    {
        visual.color = Color.red;
        yield return new WaitForSeconds(0.08f);
        if (visual != null)
            visual.color = baseColor;
        hitFlashRoutine = null;
    }

    void Defeat()
    {
        defeated = true;
        if (patternController != null)
            patternController.StopPatterns();
        if (phase == 1)
            StartCoroutine(TransitionToSecondPhase());
        else
            StartCoroutine(FinishBossBattle());
    }

    IEnumerator TransitionToSecondPhase()
    {
        BossBattleSession.SetCombatPaused(true);
        SetHitCollidersEnabled(false);
        patternAnimationLocked = true;
        phase = 2;
        currentHP = 0f;
        RefreshUI();

        SpriteRenderer planetRenderer = null;
        GameObject planetObject = GameObject.Find("Planet");
        if (planetObject != null)
            planetRenderer = planetObject.GetComponent<SpriteRenderer>();

        for (int repeat = 0; repeat < 5; repeat++)
        {
            for (int frameIndex = 45; frameIndex <= 48; frameIndex++)
            {
                Sprite frame = FindBossFrame(frameIndex);
                if (visual != null && frame != null)
                    visual.sprite = frame;
                yield return new WaitForSeconds(0.08f);
            }

            if (planetRenderer != null)
            {
                const float darkenAmount = 50f / 255f;
                Color color = planetRenderer.color;
                color.r = Mathf.Max(0f, color.r - darkenAmount);
                color.g = Mathf.Max(0f, color.g - darkenAmount);
                color.b = Mathf.Max(0f, color.b - darkenAmount);
                planetRenderer.color = color;
            }

            HealByMaxRatio(0.20f);
        }

        defeated = false;
        patternAnimationLocked = false;
        BossBattleSession.EnterSecondPhase();
        BossBattleSession.SetCombatPaused(false);
        SetHitCollidersEnabled(true);
        RefreshUI();
        if (patternController != null)
            patternController.Begin(this);
    }

    IEnumerator FinishBossBattle()
    {
        BossBattleSession.SetCombatPaused(true);
        SetHitCollidersEnabled(false);
        yield return new WaitForSeconds(3f);

        Time.timeScale = 1f;
        BossBattleSession.EnterNormalRun();
        SceneManager.LoadScene("TitleScene");
    }

    void SetHitCollidersEnabled(bool enabled)
    {
        if (hitColliders == null)
            return;

        for (int i = 0; i < hitColliders.Length; i++)
        {
            if (hitColliders[i] != null)
                hitColliders[i].enabled = enabled;
        }
    }

    public int Phase => phase;
    public SpriteRenderer Visual => visual;
    public bool IsTargetable => targetable && !defeated;

    public void SetTargetable(bool value)
    {
        targetable = value;
        SetHitCollidersEnabled(value);
    }

    public void SetPatternAnimationLocked(bool locked)
    {
        patternAnimationLocked = locked;
        if (!locked && idleFrames != null && idleFrames.Length > 0 && visual != null)
        {
            idleFrameIndex = 0;
            idleFrameTimer = 0f;
            visual.sprite = idleFrames[0];
        }
    }

    public void HealByMaxRatio(float ratio)
    {
        if (ratio <= 0f || maxHP <= 0)
            return;

        currentHP = Mathf.Min(maxHP, currentHP + maxHP * ratio);
        RefreshUI();
    }

    public Sprite FindBossFrame(int index)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>("character/boss");
        string targetName = $"boss_{index}";
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null && sprites[i].name == targetName)
                return sprites[i];
        }
        return null;
    }

    void BindUI()
    {
        GameObject panel = FindInActiveScene("bossPanel");
        if (panel == null)
            return;

        Transform hpTransform = FindChild(panel.transform, "bossHpBar");
        hpBar = hpTransform != null ? hpTransform.GetComponent<Image>() : null;

        Transform nameTransform = FindChild(panel.transform, "name");
        nameText = nameTransform != null ? nameTransform.GetComponent<TMP_Text>() : null;
    }

    void RefreshUI()
    {
        if (hpBar != null)
        {
            hpBar.type = Image.Type.Filled;
            hpBar.fillMethod = Image.FillMethod.Horizontal;
            hpBar.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpBar.fillAmount = HealthRatio;
        }

        if (nameText != null)
        {
            nameText.text =
                $"{bossName} {phase}P  {Mathf.CeilToInt(currentHP):N0} / {maxHP:N0}";
        }
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

    static GameObject FindInActiveScene(string targetName)
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
}
