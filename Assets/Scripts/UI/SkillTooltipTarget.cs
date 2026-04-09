using UnityEngine;
using UnityEngine.EventSystems;

public class SkillTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string skillId;
    private SkillTooltipManager mgr;
    private RectTransform rt;

    void Awake()
    {
        rt = transform as RectTransform;
        mgr = FindObjectOfType<SkillTooltipManager>();
        if (string.IsNullOrEmpty(skillId))
            skillId = gameObject.name;

        EnsureRaycastTarget();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (mgr == null) mgr = FindObjectOfType<SkillTooltipManager>();
        if (mgr == null) return;
        mgr.Show(skillId, rt);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (mgr == null) return;
        mgr.Hide();
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
