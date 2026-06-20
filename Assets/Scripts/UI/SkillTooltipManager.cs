using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillTooltipManager : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform detailPanel;
    public TMP_Text titleText;
    public TMP_Text descText;
    public TMP_Text levelText;
    public TMP_Text currentText;
    public TMP_Text priceText;
    public Button priceButton;
    public Text titleTextLegacy;
    public Text descTextLegacy;
    public Text levelTextLegacy;
    public Text currentTextLegacy;
    public Text priceTextLegacy;

    [Header("Placement")]
    public Vector2 offset = new Vector2(0f, 12f);
    public float extraTopPadding = 8f;

    class SkillInfo
    {
        public string id;
        public string title;
        public string desc;
        public int maxLevel;
        public int[] costs;
    }

    Dictionary<string, SkillInfo> skills = new Dictionary<string, SkillInfo>();

    void Awake()
    {
        AutoBindIfMissing();
        LoadSkillsFromCsv();
        Hide();
    }

    public void AutoBindIfMissing()
    {
        if (detailPanel == null)
            detailPanel = FindRect("detail");
        if (detailPanel != null)
        {
            if (titleText == null) titleText = FindTmp(detailPanel, "title");
            if (descText == null) descText = FindTmp(detailPanel, "desc");
            if (levelText == null) levelText = FindTmp(detailPanel, "level");
            if (currentText == null) currentText = FindTmpDeep(detailPanel, "current");
            if (priceText == null) priceText = FindTmpDeep(detailPanel, "moneyText");
            if (priceButton == null) priceButton = FindButton(detailPanel, "btnMoney");

            if (titleTextLegacy == null) titleTextLegacy = FindLegacy(detailPanel, "title");
            if (descTextLegacy == null) descTextLegacy = FindLegacy(detailPanel, "desc");
            if (levelTextLegacy == null) levelTextLegacy = FindLegacy(detailPanel, "level");
            if (currentTextLegacy == null) currentTextLegacy = FindLegacyDeep(detailPanel, "current");
            if (priceTextLegacy == null) priceTextLegacy = FindLegacyDeep(detailPanel, "moneyText");

            // Prevent tooltip from stealing hover raycasts (causes flicker on edges).
            DisableRaycasts(detailPanel);
        }
    }

    public void Show(string skillId, RectTransform anchor)
    {
        if (detailPanel == null)
            AutoBindIfMissing();
        if (detailPanel == null) return;
        if (string.IsNullOrEmpty(skillId)) return;
        skillId = ResolveId(skillId);
        if (!skills.TryGetValue(skillId, out var info)) return;

        int level = GetSkillLevel(skillId);
        int max = Mathf.Max(1, info.maxLevel);
        int nextCost = GetNextCost(info, level);

        SetText(titleText, titleTextLegacy, info.title);
        SetText(descText, descTextLegacy, info.desc);
        SetText(levelText, levelTextLegacy, $"레벨 : {level} / {max}");
        SetText(currentText, currentTextLegacy, BuildCurrentEffectText(skillId, level));
        SetText(priceText, priceTextLegacy, level >= max ? "MAX" : $"$ {nextCost}");
        if (priceButton != null) priceButton.interactable = false;

        PositionNear(anchor);
        detailPanel.gameObject.SetActive(true);
        detailPanel.SetAsLastSibling();
    }

    void DisableRaycasts(RectTransform root)
    {
        if (root == null) return;
        var graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
        var cg = root.GetComponent<CanvasGroup>();
        if (cg == null) cg = root.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
    }

    public void Hide()
    {
        if (detailPanel != null)
            detailPanel.gameObject.SetActive(false);
    }

    void PositionNear(RectTransform anchor)
    {
        if (detailPanel == null || anchor == null) return;
        var parent = detailPanel.parent as RectTransform;
        if (parent == null) return;

        var canvas = detailPanel.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        // Place tooltip above the hovered node (top-center) in screen space.
        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 topCenterWorld = (corners[1] + corners[2]) * 0.5f;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, topCenterWorld);
        float panelHalfHeight = detailPanel.rect.height * 0.5f;
        screenPoint += new Vector2(0f, panelHalfHeight + extraTopPadding);
        screenPoint += offset;

        Vector3 worldPoint;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screenPoint, cam, out worldPoint);
        detailPanel.position = worldPoint;
    }

    int GetSkillLevel(string id)
    {
        id = ResolveId(id);
        return SkillTreeManager.GetSkillLevel(id);
    }

    string ResolveId(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        if (skills.ContainsKey(id)) return id;
        if (skills.ContainsKey(id + "1")) return id + "1";
        if (id.EndsWith("1"))
        {
            var trimmed = id.Substring(0, id.Length - 1);
            if (skills.ContainsKey(trimmed)) return trimmed;
        }
        return id;
    }

    int GetNextCost(SkillInfo info, int level)
    {
        if (info.costs == null || info.costs.Length == 0) return 0;
        if (level < 0) level = 0;
        if (level >= info.costs.Length) return info.costs[info.costs.Length - 1];
        return info.costs[level];
    }

    void LoadSkillsFromCsv()
    {
        skills.Clear();
        var csv = Resources.Load<TextAsset>("data/skillTree");
        if (csv == null) return;

        var lines = csv.text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            if (i == 0)
            {
                string lower = line.ToLowerInvariant();
                if (lower.StartsWith("id,") || lower.StartsWith("skillid,"))
                    continue;
            }

            var cols = ParseCsvLine(line);
            if (cols.Count < 5) continue;

            var id = cols[0].Trim();
            var title = cols[1].Trim();
            var desc = cols[2].Replace("\\n", "\n").Trim();
            int maxLevel = SafeInt(cols[3], 1);
            int[] costs = ParseIntList(cols[4]);

            skills[id] = new SkillInfo
            {
                id = id,
                title = title,
                desc = desc,
                maxLevel = maxLevel,
                costs = costs
            };
        }
    }

    List<string> ParseCsvLine(string line)
    {
        var list = new List<string>();
        bool inQuotes = false;
        var cur = "";
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (c == ',' && !inQuotes)
            {
                list.Add(cur);
                cur = "";
            }
            else
            {
                cur += c;
            }
        }
        list.Add(cur);
        return list;
    }

    int[] ParseIntList(string s)
    {
        if (string.IsNullOrEmpty(s)) return new int[0];
        var parts = s.Split('|');
        var list = new List<int>();
        for (int i = 0; i < parts.Length; i++)
        {
            int v = SafeInt(parts[i], 0);
            list.Add(v);
        }
        return list.ToArray();
    }

    int SafeInt(string s, int def)
    {
        if (int.TryParse(s.Trim(), out var v)) return v;
        return def;
    }

    string BuildCurrentEffectText(string skillId, int level)
    {
        var effects = GameData.GetSkillEffects(skillId);
        if (effects == null || effects.Count == 0)
            return "현재 효과 : -";

        if (effects.Count == 1 && effects[0].calcType == "bool")
            return level > 0 ? "현재 효과 : 해금" : "현재 효과 : 미해금";

        var values = new List<string>();
        for (int i = 0; i < effects.Count; i++)
        {
            string value = FormatCurrentEffect(effects[i], level);
            if (!string.IsNullOrEmpty(value))
                values.Add(value);
        }
        return values.Count > 0
            ? $"현재 효과 : {string.Join(" / ", values)}"
            : "현재 효과 : -";
    }

    string FormatCurrentEffect(GameData.SkillEffectDef effect, int level)
    {
        if (effect == null) return "";
        if (effect.calcType == "bool")
            return level > 0 ? "해금" : "미해금";

        float value = EvaluateEffect(effect, level);
        switch (effect.targetStat)
        {
            case "damageMult":
            case "damageBonus":
                float damagePercent = effect.calcType == "mul"
                    ? (value - 1f) * 100f
                    : value * 100f;
                return $"{FormatSigned(damagePercent)}%";
            case "oxygenOnKillMissing":
                return $"+{FormatNumber(value * 100f)}%";
            case "fireIntervalMult":
                return $"{FormatSigned((value - 1f) * 100f)}%";
            case "oxygenDecayMult":
                return $"{FormatSigned((value - 1f) * 100f)}%";
            case "valueMult":
                return $"+{FormatNumber((value - 1f) * 100f)}%";
            case "forgeCooldownReduce":
                return $"-{FormatNumber(value)}초";
            case "forgeStabilityLevel":
                return $"+{FormatNumber(value * 5f)}%";
            case "maxOxygen":
                return $"+{FormatNumber(value)}";
            case "forgeBonusChance":
                return $"{FormatNumber(value * 100f)}% 확률";
            case "forgeBonusMultiplier":
                return $"{FormatNumber(value)}배";
            case "reviveOxygenPercent":
                return $"산소 {FormatNumber(value * 100f)}%로 부활";
            case "shieldCount":
                return $"{FormatNumber(value)}회 방어";
            default:
                return FormatNumber(value);
        }
    }

    float EvaluateEffect(GameData.SkillEffectDef effect, int level)
    {
        float value = effect.baseVal;
        switch (effect.calcType)
        {
            case "add":
                value = effect.baseVal + effect.perLevel * level;
                break;
            case "pow":
                value = effect.baseVal * Mathf.Pow(effect.perLevel, level);
                break;
            case "mul":
                float multiplier = Mathf.Pow(effect.perLevel, level);
                value = Mathf.Approximately(effect.baseVal, 0f)
                    ? multiplier
                    : effect.baseVal * multiplier;
                break;
        }
        return Mathf.Clamp(value, effect.minVal, effect.maxVal);
    }

    string FormatNumber(float value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    string FormatSigned(float value)
    {
        if (Mathf.Abs(value) < 0.0001f) return "0";
        return value.ToString("+0.##;-0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    RectTransform FindRect(string name)
    {
        var t = transform.Find(name);
        if (t == null) return null;
        return t.GetComponent<RectTransform>();
    }

    TMP_Text FindTmp(Transform root, string name)
    {
        if (root == null) return null;
        var t = root.Find(name);
        if (t == null) return null;
        var tmp = t.GetComponent<TMP_Text>();
        if (tmp != null) return tmp;
        return t.GetComponentInChildren<TMP_Text>(true);
    }

    Text FindLegacy(Transform root, string name)
    {
        if (root == null) return null;
        var t = root.Find(name);
        if (t == null) return null;
        var txt = t.GetComponent<Text>();
        if (txt != null) return txt;
        return t.GetComponentInChildren<Text>(true);
    }

    TMP_Text FindTmpDeep(Transform root, string name)
    {
        if (root == null) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name != name) continue;
            var tmp = all[i].GetComponent<TMP_Text>();
            if (tmp != null) return tmp;
        }
        return null;
    }

    Text FindLegacyDeep(Transform root, string name)
    {
        if (root == null) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name != name) continue;
            var txt = all[i].GetComponent<Text>();
            if (txt != null) return txt;
        }
        return null;
    }

    void SetText(TMP_Text tmp, Text legacy, string value)
    {
        if (tmp != null) tmp.text = value;
        if (legacy != null) legacy.text = value;
    }

    Button FindButton(Transform root, string name)
    {
        if (root == null) return null;
        var t = root.Find(name);
        if (t == null) return null;
        var btn = t.GetComponent<Button>();
        if (btn != null) return btn;
        return t.GetComponentInChildren<Button>(true);
    }
}
