using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkillTreeUIBinder : MonoBehaviour
{
    [Header("References")]
    public RectTransform skillPanel;
    public RectTransform skillContent;
    public RectTransform linkContainer;

    [Header("Visuals")]
    public bool hideLocked = false;
    public Color unlockedColor = Color.white;
    public Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    public Color maxColor = new Color(1f, 0.9f, 0.2f, 1f);
    public Color linkUnlockedColor = new Color(1f, 1f, 1f, 0.85f);
    public Color linkLockedColor = new Color(1f, 1f, 1f, 0.25f);

    class NodeDef
    {
        public string id;
        public int maxLevel;
        public List<string> reqs = new List<string>();
    }

    class UiNode
    {
        public string id;
        public RectTransform rect;
        public Graphic graphic;
        public Button button;
    }

    class Link
    {
        public string a;
        public string b;
        public Image img;
    }

    Dictionary<string, NodeDef> defs = new Dictionary<string, NodeDef>();
    Dictionary<string, UiNode> ui = new Dictionary<string, UiNode>();
    List<Link> links = new List<Link>();

    void Awake()
    {
        AutoBind();
        LoadDefs();
        MapUiNodes();
        BuildLinks();
        RefreshAll();
    }

    public void AutoBind()
    {
        if (skillPanel == null)
            skillPanel = GameObject.Find("skillPanel")?.GetComponent<RectTransform>();
        if (skillContent == null && skillPanel != null)
        {
            var t = skillPanel.Find("skillContent");
            if (t != null) skillContent = t as RectTransform;
        }
        if (linkContainer == null && skillPanel != null)
        {
            var t = skillPanel.Find("skillLinks");
            if (t != null) linkContainer = t as RectTransform;
            else
            {
                var go = new GameObject("skillLinks");
                go.transform.SetParent(skillPanel, false);
                linkContainer = go.AddComponent<RectTransform>();
                linkContainer.anchorMin = Vector2.zero;
                linkContainer.anchorMax = Vector2.one;
                linkContainer.offsetMin = Vector2.zero;
                linkContainer.offsetMax = Vector2.zero;
                linkContainer.SetAsFirstSibling();
            }
        }
    }

    void LoadDefs()
    {
        defs.Clear();
        var csv = Resources.Load<TextAsset>("data/skillTree");
        if (csv == null) return;
        var lines = csv.text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            if (i == 0 && line.ToLower().StartsWith("id,")) continue;
            var cols = ParseCsvLine(line);
            if (cols.Count < 7) continue;

            string id = cols[0].Trim();
            int maxLevel = SafeInt(cols[3], 1);
            string reqs = cols[6].Trim();

            var def = new NodeDef { id = id, maxLevel = maxLevel };
            if (!string.IsNullOrWhiteSpace(reqs) && !reqs.Equals("none"))
            {
                foreach (var r in reqs.Split('|'))
                {
                    var t = r.Trim();
                    if (t.Length > 0) def.reqs.Add(t);
                }
            }
            defs[id] = def;
        }
    }

    void MapUiNodes()
    {
        ui.Clear();
        if (skillContent == null) return;
        var nodes = skillContent.GetComponentsInChildren<SkillNodeButton>(true);
        foreach (var n in nodes)
        {
            var id = string.IsNullOrEmpty(n.skillId) ? n.gameObject.name : n.skillId;
            var rect = n.transform as RectTransform;
            var graphic = n.GetComponent<Graphic>();
            if (graphic == null) graphic = n.GetComponentInChildren<Graphic>(true);
            var btn = n.GetComponent<Button>();
            if (btn == null) btn = n.gameObject.AddComponent<Button>();

            ui[id] = new UiNode
            {
                id = id,
                rect = rect,
                graphic = graphic,
                button = btn
            };
        }
    }

    void BuildLinks()
    {
        links.Clear();
        if (linkContainer == null) return;
        foreach (Transform child in linkContainer) Destroy(child.gameObject);

        foreach (var def in defs.Values)
        {
            foreach (var req in def.reqs)
            {
                if (!ui.ContainsKey(def.id) || !ui.ContainsKey(req)) continue;
                var a = ui[req].rect;
                var b = ui[def.id].rect;
                var go = new GameObject($"link_{req}_to_{def.id}");
                go.transform.SetParent(linkContainer, false);
                var img = go.AddComponent<Image>();
                img.color = linkLockedColor;
                var rt = go.GetComponent<RectTransform>();
                rt.pivot = new Vector2(0.5f, 0.5f);

                Vector2 pA = a.anchoredPosition;
                Vector2 pB = b.anchoredPosition;
                Vector2 dir = pB - pA;
                float len = dir.magnitude;
                rt.sizeDelta = new Vector2(len, 6f);
                rt.anchoredPosition = (pA + pB) * 0.5f;
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                rt.rotation = Quaternion.Euler(0f, 0f, ang);
                links.Add(new Link { a = req, b = def.id, img = img });
            }
        }
    }

    public void RefreshAll()
    {
        foreach (var kv in ui)
        {
            var id = kv.Key;
            var node = kv.Value;
            bool unlocked = IsUnlocked(id);
            int level = SkillTreeManager.GetSkillLevel(id);
            int max = defs.ContainsKey(id) ? defs[id].maxLevel : 1;

            if (!unlocked)
            {
                node.button.interactable = false;
                if (hideLocked)
                    node.rect.gameObject.SetActive(false);
                else
                    node.graphic.color = lockedColor;
            }
            else if (level >= max)
            {
                node.button.interactable = false;
                node.rect.gameObject.SetActive(true);
                node.graphic.color = maxColor;
            }
            else
            {
                node.button.interactable = true;
                node.rect.gameObject.SetActive(true);
                node.graphic.color = unlockedColor;
            }
        }

        foreach (var l in links)
        {
            bool show = IsUnlocked(l.a) && IsUnlocked(l.b);
            if (hideLocked)
                l.img.gameObject.SetActive(show);
            else
            {
                l.img.gameObject.SetActive(true);
                l.img.color = show ? linkUnlockedColor : linkLockedColor;
            }
        }
    }

    bool IsUnlocked(string id)
    {
        if (!defs.ContainsKey(id)) return true;
        var reqs = defs[id].reqs;
        if (reqs.Count == 0) return true;
        foreach (var r in reqs)
        {
            if (SkillTreeManager.GetSkillLevel(r) <= 0) return false;
        }
        return true;
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

    int SafeInt(string s, int def)
    {
        if (int.TryParse(s.Trim(), out var v)) return v;
        return def;
    }
}
