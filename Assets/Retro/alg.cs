using UnityEngine;
using UnityEngine.Rendering;

public class Alg : MonoBehaviour
{
    void Awake()
    {
        // Stop any global DR scaling (URP/SRP path)
        ScalableBufferManager.ResizeBuffers(1f, 1f);

        // Make sure all existing cameras don't use DR
        foreach (var cam in Camera.allCameras)
            cam.allowDynamicResolution = false;

        // Also force it off for cameras created later
        Camera.onPreCull += ForceOffDR;

        Debug.Log($"[DR] Dynamic Resolution forced OFF. Cameras: {Camera.allCamerasCount}");
    }

    void OnDestroy()
    {
        Camera.onPreCull -= ForceOffDR;
    }

    static void ForceOffDR(Camera cam)
    {
        if (cam) cam.allowDynamicResolution = false;
    }
}
