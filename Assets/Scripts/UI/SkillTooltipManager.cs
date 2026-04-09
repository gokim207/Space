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
    public TMP_Text priceText;
    public Button priceButton;
    public Text titleTextLegacy;
    public Text descTextLegacy;
    public Text levelTextLegacy;
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
            if (priceText == null) priceText = FindTmpDeep(detailPanel, "moneyText");
            if (priceButton == null) priceButton = FindButton(detailPanel, "btnMoney");

            if (titleTextLegacy == null) titleTextLegacy = FindLegacy(detailPanel, "title");
            if (descTextLegacy == null) descTextLegacy = FindLegacy(detailPanel, "desc");
            if (levelTextLegacy == null) levelTextLegacy = FindLegacy(detailPanel, "level");
            if (priceTextLegacy == null) priceTextLegacy = FindLegacyDeep(detailPanel, "moneyText");
        }
    }

    public void Show(string skillId, RectTransform anchor)
    {
        if (detailPanel == null)
            AutoBindIfMissing();
        if (detailPanel == null) return;
        if (string.IsNullOrEmpty(skillId)) return;
        if (!skills.TryGetValue(skillId, out var info)) return;

        int level = GetSkillLevel(skillId);
        int max = Mathf.Max(1, info.maxLevel);
        int nextCost = GetNextCost(info, level);

        SetText(titleText, titleTextLegacy, info.title);
        SetText(descText, descTextLegacy, info.desc);
        SetText(levelText, levelTextLegacy, $"레벨 : {level} / {max}");
        SetText(priceText, priceTextLegacy, level >= max ? "MAX" : $"$ {nextCost}");
        if (priceButton != null) priceButton.interactable = false;

        PositionNear(anchor);
        detailPanel.gameObject.SetActive(true);
        detailPanel.SetAsLastSibling();
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
        return SkillTreeManager.GetSkillLevel(id);
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
            if (i == 0 && line.ToLower().StartsWith("id,")) continue;

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
