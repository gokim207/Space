using UnityEngine;
using UnityEngine.InputSystem;

public class DragCamera : MonoBehaviour
{
    public float dragSpeed = 0.01f;
    private bool dragging = false;
    private Vector3 lastMousePos;

    void Update()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
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
            Vector3 current = Mouse.current.position.ReadValue();
            Vector3 delta = current - lastMousePos;
            transform.position -= new Vector3(delta.x * dragSpeed, delta.y * dragSpeed, 0f);
            lastMousePos = current;
        }
    }
}
