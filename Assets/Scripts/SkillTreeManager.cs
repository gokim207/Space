using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class SkillTreeManager : MonoBehaviour
{
    class Node
    {
        public string id;
        public string effectKey;
        public string title;
        public string desc;
        public List<string> values = new List<string>();
        public List<string> reqs = new List<string>();
        public int maxLevel;
        public int[] costs;
        public Vector2 pos;
        public List<string> links = new List<string>();
        public int level;
        public bool unlocked;
        public Button button;
        public Image image;
    }

    private Dictionary<string, Node> nodes = new Dictionary<string, Node>();
    private RectTransform container;
    private GameObject tooltip;
    private Text tooltipText;
    private Node hoverNode;
    private bool dragging = false;
    private Vector2 lastMousePos;
    public float dragSpeed = 0.5f;
    private bool dragMoved = false;
    private const float dragThreshold = 6f;
    private readonly List<LinkLine> links = new List<LinkLine>();

    class LinkLine
    {
        public string a;
        public string b;
        public Image img;
    }

    public void Init(Transform parent)
    {
        BuildNodes();
        BuildUI(parent);
        ApplyLevelsFromPrefs();
        RefreshUnlocks();
        RefreshVisuals();
    }

    public void InitDataOnly()
    {
        BuildNodes();
        ApplyLevelsFromPrefs();
        RefreshUnlocks();
        RefreshVisuals();
    }

    void BuildNodes()
    {
        nodes.Clear();
        LoadNodesFromCsv();
    }

    void AddNode(string id, string title, string desc, int maxLevel, int[] costs, Vector2 pos)
    {
        nodes[id] = new Node
        {
            id = id,
            effectKey = BuildEffectKey(id),
            title = title,
            desc = desc,
            maxLevel = maxLevel,
            costs = costs,
            pos = pos,
            level = 0,
            unlocked = false
        };
    }

    bool LoadNodesFromCsv()
    {
        var csv = Resources.Load<TextAsset>("data/skillTree");
        if (csv == null) return false;

        var lines = csv.text.Split('\n');
        var pendingLinks = new List<(string from, string to)>();
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            if (i == 0 && line.ToLower().StartsWith("id,")) continue;

            var cols = ParseCsvLine(line);
            if (cols.Count < 9) continue;

            string id = cols[0].Trim();
            string title = cols[1].Trim();
            string desc = cols[2].Replace("\\n", "\n").Trim();
            int maxLevel = SafeInt(cols[3], 1);
            int[] costs = ParseIntList(cols[4]);
            string valuesRaw = cols[5].Trim();
            string reqs = cols[6].Trim();
            float posX = SafeFloat(cols[7], 0f);
            float posY = SafeFloat(cols[8], 0f);

            AddNode(id, title, desc, maxLevel, costs, new Vector2(posX, posY));
            if (nodes.TryGetValue(id, out var node))
            {
                node.values = ParseStringList(valuesRaw);
                node.reqs = ParseStringList(reqs);
            }

            if (!string.IsNullOrWhiteSpace(reqs) && !reqs.Equals("none"))
            {
                foreach (var link in reqs.Split('|'))
                {
                    var t = link.Trim();
                    if (t.Length > 0) pendingLinks.Add((id, t));
                }
            }
        }

        var done = new HashSet<string>();
        foreach (var p in pendingLinks)
        {
            if (!nodes.ContainsKey(p.from) || !nodes.ContainsKey(p.to)) continue;
            string key = string.CompareOrdinal(p.from, p.to) < 0 ? p.from + "-" + p.to : p.to + "-" + p.from;
            if (done.Contains(key)) continue;
            done.Add(key);
            Link(p.from, p.to);
        }

        return nodes.Count > 0;
    }

    void Link(string a, string b)
    {
        nodes[a].links.Add(b);
        nodes[b].links.Add(a);
    }

    void BuildUI(Transform parent)
    {
        container = new GameObject("SkillTree").AddComponent<RectTransform>();
        container.SetParent(parent, false);
        container.anchorMin = new Vector2(0.5f, 0.5f);
        container.anchorMax = new Vector2(0.5f, 0.5f);
        container.anchoredPosition = new Vector2(0f, 0f);
        container.sizeDelta = new Vector2(600f, 400f);

        foreach (var kv in nodes)
        {
            var n = kv.Value;
            var nodeGo = new GameObject("Node_" + n.id);
            nodeGo.transform.SetParent(container, false);
            var img = nodeGo.AddComponent<Image>();
            img.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            var btn = nodeGo.AddComponent<Button>();
            var rt = nodeGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(90f, 60f);
            rt.anchoredPosition = n.pos;

            var label = new GameObject("Label").AddComponent<Text>();
            label.transform.SetParent(nodeGo.transform, false);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = n.title;
            label.fontSize = 12;
            label.color = Color.black;
            label.alignment = TextAnchor.MiddleCenter;
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var trigger = nodeGo.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => ShowTooltip(n));
            AddTrigger(trigger, EventTriggerType.PointerExit, HideTooltip);
            btn.onClick.AddListener(() => TryBuy(n));

            n.button = btn;
            n.image = img;
        }

        DrawLinks();
        BuildTooltip(parent);
    }

    void BuildTooltip(Transform parent)
    {
        tooltip = new GameObject("SkillTooltip");
        tooltip.transform.SetParent(container, false);
        var img = tooltip.AddComponent<Image>();
        img.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        var cg = tooltip.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        var rt = tooltip.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(260f, 140f);

        tooltipText = new GameObject("Text").AddComponent<Text>();
        tooltipText.transform.SetParent(tooltip.transform, false);
        tooltipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tooltipText.fontSize = 14;
        tooltipText.color = Color.black;
        tooltipText.alignment = TextAnchor.UpperLeft;
        var trt = tooltipText.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.2f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(10f, 10f);
        trt.offsetMax = new Vector2(-10f, -10f);

        // purchase happens by clicking the node itself (no buy button)

        tooltip.SetActive(false);
    }

    void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    void ApplyLevelsFromPrefs()
    {
        int slot = GameFlowManager.CurrentSlot;
        foreach (var n in nodes.Values)
        {
            if (slot >= 1)
                n.level = PlayerPrefs.GetInt($"slot_{slot}_skill_{n.id}", 0);
            else
                n.level = 0;
        }
        ApplySkillEffects();
    }

    void ApplySkillEffects()
    {
        SkillEffects.ApplyAllFromTable(GetLevelByEffectKey);
    }

    void RefreshUnlocks()
    {
        foreach (var n in nodes.Values) n.unlocked = false;
        foreach (var n in nodes.Values)
        {
            if (n.reqs.Count == 0)
            {
                n.unlocked = true;
                continue;
            }
            bool ok = true;
            foreach (var r in n.reqs)
            {
                if (!nodes.ContainsKey(r) || nodes[r].level <= 0)
                {
                    ok = false;
                    break;
                }
            }
            n.unlocked = ok;
        }
    }

    void RefreshVisuals()
    {
        foreach (var n in nodes.Values)
        {
            if (n.image == null || n.button == null) continue;
            if (!n.unlocked)
            {
                n.image.color = new Color(0.35f, 0.35f, 0.35f, 1f);
                n.button.interactable = false;
                n.image.gameObject.SetActive(false);
            }
            else if (n.level >= n.maxLevel)
            {
                n.image.color = new Color(1f, 0.9f, 0.2f, 1f);
                n.button.interactable = false;
                n.image.gameObject.SetActive(true);
            }
            else
            {
                n.image.color = new Color(1f, 0.35f, 0.35f, 1f);
                n.button.interactable = true;
                n.image.gameObject.SetActive(true);
            }
        }
        RefreshLinks();
    }

    void ShowTooltip(Node n)
    {
        if (tooltip == null || tooltipText == null || n == null) return;
        hoverNode = n;
        string valueText = NextValue(n);
        string extra = string.IsNullOrEmpty(valueText) ? "" : $"\n효과: {valueText}";
        tooltipText.text = $"{n.title}\n{n.desc}{extra}\n레벨 {n.level}/{n.maxLevel}\n비용: {NextCost(n)}$";
        var pos = n.image.rectTransform.anchoredPosition;
        var rt = tooltip.GetComponent<RectTransform>();
        rt.anchoredPosition = pos + new Vector2(0f, 90f);
        tooltip.SetActive(true);
        tooltip.transform.SetAsLastSibling();
    }

    int GetLevelByEffectKey(string key)
    {
        int level = 0;
        foreach (var n in nodes.Values)
        {
            if (n.effectKey == key)
                level = Mathf.Max(level, n.level);
        }
        return level;
    }

    string NextValue(Node n)
    {
        if (n.values == null || n.values.Count == 0) return "";
        int idx = Mathf.Clamp(n.level, 0, n.values.Count - 1);
        return n.values[idx];
    }

    static string BuildEffectKey(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        int i = id.Length - 1;
        while (i >= 0 && char.IsDigit(id[i])) i--;
        return id.Substring(0, i + 1);
    }

    static List<string> ParseStringList(string s)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(s) || s.Equals("none")) return list;
        foreach (var p in s.Split('|'))
        {
            var t = p.Trim();
            if (t.Length > 0) list.Add(t);
        }
        return list;
    }

    static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (line == null) return result;
        bool inQuotes = false;
        var cur = new System.Text.StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    cur.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(cur.ToString());
                cur.Length = 0;
            }
            else
            {
                cur.Append(c);
            }
        }
        result.Add(cur.ToString());
        return result;
    }

    static int[] ParseIntList(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new int[] { 0 };
        var parts = s.Split('|');
        var list = new List<int>();
        foreach (var p in parts)
        {
            if (int.TryParse(p.Trim(), out var v)) list.Add(v);
        }
        if (list.Count == 0) list.Add(0);
        return list.ToArray();
    }

    static int SafeInt(string s, int def)
    {
        if (int.TryParse(s.Trim(), out var v)) return v;
        return def;
    }

    static float SafeFloat(string s, float def)
    {
        if (float.TryParse(s.Trim(), out var v)) return v;
        return def;
    }

    public static List<string> GetSkillIdsFromCsv()
    {
        var csv = Resources.Load<TextAsset>("data/skillTree");
        var ids = new List<string>();
        if (csv == null) return ids;
        var lines = csv.text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            if (i == 0 && line.ToLower().StartsWith("id,")) continue;
            var cols = ParseCsvLine(line);
            if (cols.Count > 0)
            {
                var id = cols[0].Trim();
                if (id.Length > 0) ids.Add(id);
            }
        }
        return ids;
    }

    void HideTooltip()
    {
        tooltip.SetActive(false);
        hoverNode = null;
    }

    int NextCost(Node n)
    {
        if (n.level >= n.maxLevel) return 0;
        if (n.level < n.costs.Length) return n.costs[n.level];
        return n.costs[n.costs.Length - 1];
    }

    void TryBuy(Node n)
    {
        if (dragMoved) return;
        if (!n.unlocked) return;
        if (n.level >= n.maxLevel) return;
        int cost = NextCost(n);
        var flow = GameFlowManager.Instance;
        if (flow == null || !flow.SpendMoney(cost)) return;

        n.level += 1;
        ApplySkillEffects();
        RefreshUnlocks();
        RefreshVisuals();
        ShowTooltip(n);
    }

    public static void SaveSkills(int slot)
    {
        if (slot < 1) return;
        var mgr = FindManager();
        if (mgr == null) return;
        foreach (var n in mgr.nodes.Values)
        {
            PlayerPrefs.SetInt($"slot_{slot}_skill_{n.id}", n.level);
        }
    }

    public static void LoadSkills(int slot)
    {
        if (slot < 1) return;
        var mgr = FindManager();
        if (mgr == null) return;
        foreach (var n in mgr.nodes.Values)
        {
            n.level = PlayerPrefs.GetInt($"slot_{slot}_skill_{n.id}", 0);
        }
        mgr.ApplySkillEffects();
        mgr.RefreshUnlocks();
        mgr.RefreshVisuals();
    }

    public static void ResetAllSkills()
    {
        var mgr = FindManager();
        if (mgr == null) return;
        foreach (var n in mgr.nodes.Values)
        {
            n.level = 0;
        }
        mgr.ApplySkillEffects();
        mgr.RefreshUnlocks();
        mgr.RefreshVisuals();
    }

    public static int GetSkillLevel(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        var mgr = FindManager();
        if (mgr != null && mgr.nodes.TryGetValue(id, out var node))
            return node.level;

        int slot = GameFlowManager.CurrentSlot;
        if (slot < 1) return 0;
        return PlayerPrefs.GetInt($"slot_{slot}_skill_{id}", 0);
    }

    static SkillTreeManager FindManager()
    {
        var mgr = FindObjectOfType<SkillTreeManager>();
        if (mgr != null) return mgr;
        var all = Resources.FindObjectsOfTypeAll<SkillTreeManager>();
        return all != null && all.Length > 0 ? all[0] : null;
    }

    public static bool TryBuyById(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var mgr = FindManager();
        if (mgr == null)
        {
            Debug.LogWarning("SkillTreeManager not found. Cannot buy skill.");
            return false;
        }
        if (!mgr.nodes.TryGetValue(id, out var node)) return false;
        int before = node.level;
        mgr.TryBuy(node);
        return node.level > before;
    }

    void Update()
    {
        if (Mouse.current == null || container == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            lastMousePos = Mouse.current.position.ReadValue();
            dragMoved = false;
        }
        if (!Mouse.current.leftButton.isPressed)
        {
            dragging = false;
            dragMoved = false;
            return;
        }
        if (dragging)
        {
            Vector2 current = Mouse.current.position.ReadValue();
            Vector2 delta = current - lastMousePos;
            if (delta.sqrMagnitude >= dragThreshold * dragThreshold)
                dragMoved = true;
            container.anchoredPosition += delta * dragSpeed;
            lastMousePos = current;

            if (hoverNode != null)
            {
                var pos = hoverNode.image.rectTransform.anchoredPosition;
                var rt = tooltip.GetComponent<RectTransform>();
                rt.anchoredPosition = pos + new Vector2(0f, 90f);
            }
        }
    }

    void DrawLinks()
    {
        var drawn = new HashSet<string>();
        foreach (var n in nodes.Values)
        {
            foreach (var linkId in n.links)
            {
                string key = n.id.CompareTo(linkId) < 0 ? n.id + "-" + linkId : linkId + "-" + n.id;
                if (drawn.Contains(key)) continue;
                drawn.Add(key);
                var other = nodes[linkId];
                CreateLine(n.id, other.id, n.pos, other.pos);
            }
        }
    }

    void CreateLine(string aId, string bId, Vector2 a, Vector2 b)
    {
        var go = new GameObject("Link");
        go.transform.SetParent(container, false);
        var img = go.AddComponent<Image>();
        img.color = Color.black;
        var rt = go.GetComponent<RectTransform>();
        Vector2 mid = (a + b) * 0.5f;
        Vector2 dir = b - a;
        float len = dir.magnitude;
        rt.sizeDelta = new Vector2(len, 6f);
        rt.anchoredPosition = mid;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.rotation = Quaternion.Euler(0f, 0f, ang);
        go.transform.SetAsFirstSibling();
        links.Add(new LinkLine { a = aId, b = bId, img = img });
    }

    void RefreshLinks()
    {
        foreach (var l in links)
        {
            bool show = nodes[l.a].unlocked && nodes[l.b].unlocked;
            l.img.gameObject.SetActive(show);
        }
    }
}
