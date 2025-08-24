using UnityEngine;
using UnityEngine.UI;
public class AlwaysOnTopUI : MonoBehaviour
{
    [Tooltip("Sorting Layer to render on (must exist in Project Settings).")]
    public string sortingLayerName = "UIOverlay";

    [Tooltip("Order within the sorting layer (higher = drawn later/on top).")]
    public int sortingOrder = 100;

    void Awake()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = sortingLayerName;
        canvas.sortingOrder     = sortingOrder;

        // Make sure your MR Camera can see this layer:
        Camera cam = Camera.main;
        if (cam != null && !cam.cullingMask.HasLayer(LayerMask.NameToLayer(sortingLayerName)))
            cam.cullingMask |= 1 << LayerMask.NameToLayer(sortingLayerName);
    }
}

// Helper extension for cullingMask check
static class LayerMaskExtensions
{
    public static bool HasLayer(this int mask, int layer) => (mask & (1 << layer)) != 0;
}
