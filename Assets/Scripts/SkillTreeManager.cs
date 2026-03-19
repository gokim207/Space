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
    public float dragSpeed = 1.0f;

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

        AddNode("atk", "공격력 증가1", "무기의 공격력을 증가시킵니다.\n(+1 고정)", 5, new[] { 1, 10, 100, 1000, 10000 }, new Vector2(0f, 0f));
        AddNode("value", "가치 증가1", "모든 광물 가치 증가.\n(레벨마다 2배)", 3, new[] { 1000, 50000, 100000 }, new Vector2(0f, 140f));
        AddNode("forge", "재련 쿨감 1", "대장간 재련 쿨타임 감소.\n(-0.2s)", 3, new[] { 100, 1000, 10000 }, new Vector2(-160f, 0f));
        AddNode("firerate", "발사 속도 증가1", "무기 공격 속도 증가.\n(-5%)", 5, new[] { 5, 15, 35, 50, 100 }, new Vector2(0f, -140f));
        AddNode("oxygenkill", "적 처치 산소 획득1", "소행성 처치 시 산소 획득.\n(+3)", 3, new[] { 10, 300, 1000 }, new Vector2(160f, 0f));
        AddNode("oxygenmax", "최대 산소 증가1", "최대 산소 증가.\n(+10)", 5, new[] { 100, 200, 300, 400, 500 }, new Vector2(160f, -140f));
        AddNode("oxygendecay", "산소 감소 1", "매초 산소 감소량 완화.\n(-5%)", 3, new[] { 50, 100, 500 }, new Vector2(320f, 0f));

        Link("atk", "value");
        Link("atk", "forge");
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

        BuildTooltip(parent);
    }

    void BuildTooltip(Transform parent)
    {
        tooltip = new GameObject("SkillTooltip");
        tooltip.transform.SetParent(parent, false);
        var img = tooltip.AddComponent<Image>();
        img.color = new Color(0.9f, 0.9f, 0.9f, 1f);
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
        foreach (var n in nodes.Values)
        {
            n.level = 0;
        }
        ApplySkillEffects();
    }

    void ApplySkillEffects()
    {
        SkillEffects.SetDamageLevel(nodes["atk"].level);
        SkillEffects.SetValueLevel(nodes["value"].level);
        SkillEffects.SetFireRateLevel(nodes["firerate"].level);
        SkillEffects.SetForgeCooldownLevel(nodes["forge"].level);
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
                n.image.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                n.button.interactable = true;
                n.image.gameObject.SetActive(true);
            }
        }
    }

    void ShowTooltip(Node n)
    {
        hoverNode = n;
        tooltipText.text = $"{n.title}\n{n.desc}\n레벨 {n.level}/{n.maxLevel}\n비용: {NextCost(n)}";
        var pos = n.image.rectTransform.anchoredPosition;
        var rt = tooltip.GetComponent<RectTransform>();
        rt.anchoredPosition = pos + new Vector2(0f, 90f);
        tooltip.SetActive(true);
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

    void Update()
    {
        if (Mouse.current == null || container == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame && hoverNode == null)
        {
            dragging = true;
            lastMousePos = Mouse.current.position.ReadValue();
        }
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            dragging = false;
        }
        if (dragging)
        {
            Vector2 current = Mouse.current.position.ReadValue();
            Vector2 delta = current - lastMousePos;
            container.anchoredPosition += delta * dragSpeed * Time.unscaledDeltaTime;
            lastMousePos = current;

            if (hoverNode != null)
            {
                var pos = hoverNode.image.rectTransform.anchoredPosition;
                var rt = tooltip.GetComponent<RectTransform>();
                rt.anchoredPosition = pos + new Vector2(0f, 90f);
            }
        }
    }
}
