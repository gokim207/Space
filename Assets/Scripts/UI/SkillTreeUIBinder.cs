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
    public bool hideLocked = true;
    public float linkWidth = 6f;
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
        public Outline outline;
        public Color authoredColor;
        public Color authoredOutlineColor;
        public bool authoredOutlineEnabled;
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
        if (skillContent == null && skillPanel != null)
        {
            var node = skillPanel.GetComponentInChildren<SkillNodeButton>(true);
            if (node != null)
                skillContent = node.transform.parent as RectTransform;
        }
        if (linkContainer == null && skillPanel != null)
        {
            var t = skillPanel.Find("skillLinks");
            if (t != null) linkContainer = t as RectTransform;
            else
            {
                var go = new GameObject("skillLinks");
                if (skillContent != null)
                    go.transform.SetParent(skillContent, false);
                else
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
            if (i == 0)
            {
                string lower = line.ToLowerInvariant();
                if (lower.StartsWith("id,") || lower.StartsWith("skillid,"))
                    continue;
            }
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
            // 씬에서 수정한 오브젝트 이름이 CSV ID와 일치하면 이를 우선한다.
            // 복제 후 컴포넌트의 skillId를 미처 수정하지 않아도 잘못된 노드에 덮어쓰지 않는다.
            var id = defs.ContainsKey(n.gameObject.name)
                ? n.gameObject.name
                : (string.IsNullOrEmpty(n.skillId) ? n.gameObject.name : n.skillId);
            var mappedId = ResolveId(id);
            if (!string.IsNullOrEmpty(mappedId))
                id = mappedId;
            var rect = n.transform as RectTransform;
            var graphic = n.GetComponent<Graphic>();
            if (graphic == null) graphic = n.GetComponentInChildren<Graphic>(true);
            var btn = n.GetComponent<Button>();
            if (btn == null) btn = n.gameObject.AddComponent<Button>();
            var outline = n.GetComponent<Outline>();

            ui[id] = new UiNode
            {
                id = id,
                rect = rect,
                graphic = graphic,
                button = btn,
                outline = outline,
                authoredColor = graphic != null ? graphic.color : Color.white,
                authoredOutlineColor = outline != null ? outline.effectColor : Color.clear,
                authoredOutlineEnabled = outline != null && outline.enabled
            };
        }
    }

    string ResolveId(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        if (defs.ContainsKey(id)) return id;
        if (defs.ContainsKey(id + "1")) return id + "1";
        if (id.EndsWith("1"))
        {
            var trimmed = id.Substring(0, id.Length - 1);
            if (defs.ContainsKey(trimmed)) return trimmed;
        }
        return id;
    }

    void BuildLinks()
    {
        links.Clear();
        if (linkContainer == null) return;

        // 이전에 자동 생성한 선만 정리한다. 노드와 다른 UI 오브젝트는 건드리지 않는다.
        var oldLinks = new List<GameObject>();
        foreach (Transform child in linkContainer)
        {
            if (child.name.StartsWith("link_", System.StringComparison.Ordinal))
                oldLinks.Add(child.gameObject);
        }
        for (int i = 0; i < oldLinks.Count; i++)
        {
            oldLinks[i].SetActive(false);
            Destroy(oldLinks[i]);
        }

        foreach (var def in defs.Values)
        {
            for (int i = 0; i < def.reqs.Count; i++)
            {
                string prerequisiteId = def.reqs[i];
                if (!ui.TryGetValue(prerequisiteId, out var fromNode)) continue;
                if (!ui.TryGetValue(def.id, out var toNode)) continue;
                if (fromNode.rect == null || toNode.rect == null) continue;

                // 노드와 skillLinks는 모두 skillContent의 직접 자식이므로 같은 로컬 좌표계를 쓴다.
                // 월드/스크린 좌표 변환을 거치면 Stretch 앵커 때문에 선이 밀릴 수 있다.
                Vector3 fromLocal3 = fromNode.rect.localPosition;
                Vector3 toLocal3 = toNode.rect.localPosition;
                Vector2 fromLocal = new Vector2(fromLocal3.x, fromLocal3.y);
                Vector2 toLocal = new Vector2(toLocal3.x, toLocal3.y);
                Vector2 direction = toLocal - fromLocal;

                var lineObject = new GameObject(
                    $"link_{prerequisiteId}_to_{def.id}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                lineObject.transform.SetParent(linkContainer, false);

                var lineImage = lineObject.GetComponent<Image>();
                lineImage.color = linkUnlockedColor;
                lineImage.raycastTarget = false;

                var lineRect = lineObject.GetComponent<RectTransform>();
                lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);
                lineRect.localPosition = new Vector3(
                    (fromLocal.x + toLocal.x) * 0.5f,
                    (fromLocal.y + toLocal.y) * 0.5f,
                    0f);
                lineRect.sizeDelta = new Vector2(direction.magnitude, Mathf.Max(1f, linkWidth));
                lineRect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

                lineObject.transform.SetAsFirstSibling();
                links.Add(new Link
                {
                    a = prerequisiteId,
                    b = def.id,
                    img = lineImage
                });
            }
        }
    }

    public void RefreshAll()
    {
        if (links.Count == 0 && defs.Count > 0 && ui.Count > 0)
            BuildLinks();
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
                else if (node.rect != null)
                    node.rect.gameObject.SetActive(true);
            }
            else if (level >= max)
            {
                // 비활성화 색상으로 투명해지지 않도록 버튼 상태는 유지한다.
                // 구매 처리는 SkillTreeManager에서 MAX 여부를 다시 검사한다.
                node.button.interactable = true;
                node.rect.gameObject.SetActive(true);
            }
            else
            {
                node.button.interactable = true;
                node.rect.gameObject.SetActive(true);
            }

            // 사용자가 Inspector에서 설정한 원래 색상을 유지한다.
            if (node.graphic != null)
                node.graphic.color = node.authoredColor;

            if (node.outline != null)
            {
                if (unlocked && level >= max)
                {
                    node.outline.enabled = true;
                    node.outline.effectColor = maxColor;
                }
                else
                {
                    node.outline.enabled = node.authoredOutlineEnabled;
                    node.outline.effectColor = node.authoredOutlineColor;
                }
            }
        }

        foreach (var l in links)
        {
            bool show = IsUnlocked(l.a) && IsUnlocked(l.b);
            if (hideLocked)
                l.img.gameObject.SetActive(show);
            else
                l.img.gameObject.SetActive(true);
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
