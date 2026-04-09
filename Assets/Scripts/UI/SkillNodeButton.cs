using UnityEngine;
using UnityEngine.EventSystems;

public class SkillNodeButton : MonoBehaviour, IPointerClickHandler
{
    public string skillId;
    private RectTransform rt;
    private SkillTooltipManager tooltip;

    void Awake()
    {
        rt = transform as RectTransform;
        tooltip = FindObjectOfType<SkillTooltipManager>();
        if (string.IsNullOrEmpty(skillId))
            skillId = gameObject.name;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        SkillTreeManager.TryBuyById(skillId);
        if (tooltip != null)
            tooltip.Show(skillId, rt);
    }
}
