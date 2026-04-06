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
}
