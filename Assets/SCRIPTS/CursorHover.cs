using UnityEngine;
using UnityEngine.EventSystems;

public class CursorHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // You can assign a specific hand cursor texture in the Inspector
    public Texture2D customCursor;

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Change to the custom texture (or pass null for the system default hand)
        Cursor.SetCursor(customCursor, Vector2.zero, CursorMode.Auto);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Reset to the default system arrow
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void OnDisable()
    {
        // Reset cursor when script is disabled
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void OnDestroy()
    {
        // Reset cursor when script/GameObject is destroyed
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}