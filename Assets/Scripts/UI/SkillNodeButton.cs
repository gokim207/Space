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

        EnsureRaycastTarget();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        var bought = SkillTreeManager.TryBuyById(skillId);
        if (tooltip == null)
            tooltip = FindObjectOfType<SkillTooltipManager>();
        if (tooltip != null)
            tooltip.Show(skillId, rt);
        if (bought)
        {
            var binder = FindObjectOfType<SkillTreeUIBinder>();
            if (binder != null) binder.RefreshAll();
        }
    }

    void EnsureRaycastTarget()
    {
        var graphic = GetComponent<UnityEngine.UI.Graphic>();
        if (graphic == null)
        {
            var img = gameObject.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            graphic = img;
        }
        graphic.raycastTarget = true;
    }
}
