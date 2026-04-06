using UnityEngine;
using UnityEngine.EventSystems;

public class DragPan : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Header("Drag Targets")]
    public RectTransform content; // panel to move (e.g., skillContent)
    public RectTransform bounds;  // fallback view bounds (e.g., skillPanel)
    public RectTransform viewport; // preferred view bounds (e.g., skill viewport)
    [Range(0.1f, 2f)]
    public float dragSpeed = 0.6f;

    private Vector2 lastPos;

    public void OnBeginDrag(PointerEventData eventData)
    {
        lastPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (content == null || bounds == null) return;
        Vector2 delta = eventData.position - lastPos;
        lastPos = eventData.position;

        content.anchoredPosition += delta * dragSpeed;
        ClampContent();
    }

    void ClampContent()
    {
        if (content == null || bounds == null) return;

        var view = viewport != null ? viewport : bounds;
        // Assumes content and view are under the same parent space (typical UI layout).
        Vector3[] contentCorners = new Vector3[4];
        Vector3[] viewCorners = new Vector3[4];
        content.GetWorldCorners(contentCorners);
        view.GetWorldCorners(viewCorners);

        // Convert to view local space.
        for (int i = 0; i < 4; i++)
        {
            contentCorners[i] = view.InverseTransformPoint(contentCorners[i]);
            viewCorners[i] = view.InverseTransformPoint(viewCorners[i]);
        }

        float contentMinX = contentCorners[0].x;
        float contentMaxX = contentCorners[2].x;
        float contentMinY = contentCorners[0].y;
        float contentMaxY = contentCorners[2].y;

        float viewMinX = viewCorners[0].x;
        float viewMaxX = viewCorners[2].x;
        float viewMinY = viewCorners[0].y;
        float viewMaxY = viewCorners[2].y;

        Vector2 offset = Vector2.zero;

        if (contentMaxX < viewMaxX) offset.x = viewMaxX - contentMaxX;
        if (contentMinX > viewMinX) offset.x = viewMinX - contentMinX;

        if (contentMaxY < viewMaxY) offset.y = viewMaxY - contentMaxY;
        if (contentMinY > viewMinY) offset.y = viewMinY - contentMinY;

        if (offset != Vector2.zero)
            content.anchoredPosition += offset;
    }
}
