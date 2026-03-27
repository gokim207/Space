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
        public string title;
        public string desc;
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

    void BuildNodes()
    {
        nodes.Clear();

        AddNode("atk", "공격력 증가1", "무기의 공격력을 증가시킵니다.\n(+1 고정)", 5, new[] { 1, 5, 25, 125, 625 }, new Vector2(0f, 0f));
        AddNode("value", "가치 증가1", "모든 광물 가치 증가.\n(레벨마다 1.5배)", 3, new[] { 100, 500, 1000 }, new Vector2(0f, 140f));
        AddNode("copper", "구리 광석", "3웨이브 이후부터 나옵니다.", 1, new[] { 100 }, new Vector2(0f, 280f));
        AddNode("forge", "재련 쿨감 1", "대장간 재련 쿨타임 감소.\n(-0.2s)", 5, new[] { 10, 50, 125, 250, 500 }, new Vector2(-160f, 0f));
        AddNode("anvil", "모루 안정화", "재련 배율 안정화.\n(0.5x -5% / 2x +5%)", 3, new[] { 50, 100, 150 }, new Vector2(-320f, 0f));
        AddNode("firerate", "발사 속도 증가1", "무기 공격 속도 증가.\n(-3%)", 5, new[] { 5, 15, 35, 50, 100 }, new Vector2(0f, -140f));
        AddNode("oxygenkill", "적 처치 산소 획득1", "소행성 처치 시 산소 획득.\n(+3)", 3, new[] { 10, 300, 1000 }, new Vector2(160f, 0f));
        AddNode("oxygenmax", "최대 산소 증가1", "최대 산소 증가.\n(+10)", 5, new[] { 100, 200, 300, 400, 500 }, new Vector2(160f, -140f));
        AddNode("oxygendecay", "산소 감소 1", "매초 산소 감소량 완화.\n(-3%)", 3, new[] { 50, 100, 500 }, new Vector2(320f, 0f));

        Link("atk", "value");
        Link("value", "copper");
        Link("atk", "forge");
        Link("forge", "anvil");
        Link("atk", "firerate");
        Link("atk", "oxygenkill");
        Link("oxygenkill", "oxygenmax");
        Link("oxygenkill", "oxygendecay");
    }

    void AddNode(string id, string title, string desc, int maxLevel, int[] costs, Vector2 pos)
    {
        nodes[id] = new Node
        {
            id = id,
            title = title,
            desc = desc,
            maxLevel = maxLevel,
            costs = costs,
            pos = pos,
            level = 0,
            unlocked = false
        };
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
        SkillEffects.SetDamageLevel(nodes["atk"].level);
        SkillEffects.SetValueLevel(nodes["value"].level);
        SkillEffects.SetCopperLevel(nodes["copper"].level);
        SkillEffects.SetFireRateLevel(nodes["firerate"].level);
        SkillEffects.SetForgeCooldownLevel(nodes["forge"].level);
        SkillEffects.SetForgeStabilityLevel(nodes["anvil"].level);
        SkillEffects.SetOxygenOnKillLevel(nodes["oxygenkill"].level);
        SkillEffects.SetMaxOxygenLevel(nodes["oxygenmax"].level);
        SkillEffects.SetOxygenDecayLevel(nodes["oxygendecay"].level);
    }

    void RefreshUnlocks()
    {
        foreach (var n in nodes.Values) n.unlocked = false;
        nodes["atk"].unlocked = true;
        foreach (var n in nodes.Values)
        {
            if (n.level > 0)
            {
                n.unlocked = true;
                foreach (var link in n.links)
                    nodes[link].unlocked = true;
            }
        }
    }

    void RefreshVisuals()
    {
        foreach (var n in nodes.Values)
        {
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
        hoverNode = n;
        tooltipText.text = $"{n.title}\n{n.desc}\n레벨 {n.level}/{n.maxLevel}\n비용: {NextCost(n)}$";
        var pos = n.image.rectTransform.anchoredPosition;
        var rt = tooltip.GetComponent<RectTransform>();
        rt.anchoredPosition = pos + new Vector2(0f, 90f);
        tooltip.SetActive(true);
        tooltip.transform.SetAsLastSibling();
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

        int slot = GameFlowManager.CurrentSlot;
        if (slot >= 1)
        {
            PlayerPrefs.SetInt($"slot_{slot}_skill_{n.id}", n.level);
            PlayerPrefs.Save();
        }
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

    static SkillTreeManager FindManager()
    {
        var mgr = FindObjectOfType<SkillTreeManager>();
        if (mgr != null) return mgr;
        var all = Resources.FindObjectsOfTypeAll<SkillTreeManager>();
        return all != null && all.Length > 0 ? all[0] : null;
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
