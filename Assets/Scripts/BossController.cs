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

    float currentHP;
    Image hpBar;
    TMP_Text nameText;
    Color baseColor = Color.white;
    SpriteRenderer visual;
    bool defeated;
    int phase = 1;
    Collider2D[] hitColliders;

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
    }

    public void BeginBattle()
    {
        maxHP = 150000;
        currentHP = maxHP;
        defeated = false;
        phase = 1;
        BossBattleSession.SetCombatPaused(false);
        EnsureVisible();
        SetHitCollidersEnabled(true);
        BindUI();
        RefreshUI();
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
        if (defeated || damage <= 0)
            return;

        currentHP = Mathf.Max(0, currentHP - damage);
        RefreshUI();

        if (visual != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashHit());
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
    }

    void Defeat()
    {
        defeated = true;
        if (phase == 1)
            StartCoroutine(TransitionToSecondPhase());
        else
            StartCoroutine(FinishBossBattle());
    }

    IEnumerator TransitionToSecondPhase()
    {
        BossBattleSession.SetCombatPaused(true);
        SetHitCollidersEnabled(false);
        if (nameText != null)
            nameText.text = $"{bossName}  2페이즈 전환";

        yield return new WaitForSeconds(3f);

        phase = 2;
        currentHP = maxHP;
        defeated = false;
        BossBattleSession.EnterSecondPhase();
        BossBattleSession.SetCombatPaused(false);
        SetHitCollidersEnabled(true);
        RefreshUI();
    }

    IEnumerator FinishBossBattle()
    {
        BossBattleSession.SetCombatPaused(true);
        SetHitCollidersEnabled(false);
        if (nameText != null)
            nameText.text = $"{bossName} 처치";

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
            nameText.text = $"{bossName} P{phase}  {Mathf.CeilToInt(currentHP):N0} / {maxHP:N0}";
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
