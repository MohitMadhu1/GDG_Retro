using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

[DefaultExecutionOrder(-10000)]
public class XRBootAndBind : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (FindObjectOfType<XRBootAndBind>() != null) return;
        var go = new GameObject("XRBootAndBind");
        DontDestroyOnLoad(go);
        go.AddComponent<XRBootAndBind>();
        Debug.Log("[XRBoot] Bootstrap installed.");
    }

    void Start()
    {
        StartCoroutine(BootXRAndBindHands());
    }

    IEnumerator BootXRAndBindHands()
    {
        Debug.Log("[XRBoot] Starting XR (will try to initialize loader)...");
        var gs = XRGeneralSettings.Instance;
        if (gs == null)
        {
            Debug.LogError("[XRBoot] XRGeneralSettings.Instance is NULL. XR Plug-in Management may not be configured for this project.");
            yield break;
        }

        var man = gs.Manager;
        if (man == null)
        {
            Debug.LogError("[XRBoot] XRManagerSettings is NULL. Open Project Settings → XR Plug-in Management and enable OpenXR for THIS platform.");
            yield break;
        }

        // List configured loaders
        if (man.loaders == null || man.loaders.Count == 0)
        {
            Debug.LogError("[XRBoot] No loaders configured. In Project Settings → XR Plug-in Management, tick OpenXR on the active platform tab.");
            yield break;
        }
        else
        {
            var names = new System.Text.StringBuilder();
            foreach (var l in man.loaders) names.Append(l ? l.name : "null").Append(", ");
            Debug.Log($"[XRBoot] Configured loaders: {names}");
        }

        // Initialize loader (with timeout)
        float t0 = Time.realtimeSinceStartup;
        yield return man.InitializeLoader();
        float dt = Time.realtimeSinceStartup - t0;

        if (man.activeLoader == null)
        {
            Debug.LogError($"[XRBoot] InitializeLoader returned but activeLoader is NULL (elapsed {dt:F2}s). Check OpenXR is enabled on this platform and no validation errors exist.");
            yield break;
        }
        Debug.Log($"[XRBoot] Active loader: {man.activeLoader.name}");

        // Start subsystems
        man.StartSubsystems();
        Debug.Log("[XRBoot] Subsystems started.");

        // Check XR Hands feature result
        var hands = new List<XRHandSubsystem>();
        SubsystemManager.GetInstances(hands);
        Debug.Log($"[XRBoot] XRHandSubsystem instances after start: {hands.Count}");

#if UNITY_EDITOR
        if (hands.Count == 0)
            Debug.LogWarning("[XRBoot] No XRHandSubsystem. In Editor, ensure OpenXR (Standalone) has XR Hands / Hand Tracking enabled. Meta XR Simulator does NOT create XRHands.");
        else
            Debug.Log("[XRBoot] XRHandSubsystem bound (Editor).");
#else
        if (hands.Count == 0)
            Debug.LogError("[XRBoot] No XRHandSubsystem on device. Enable XR Hands + Meta Quest Hand Tracking features and turn Hand Tracking ON in headset.");
        else
            Debug.Log("[XRBoot] XRHandSubsystem bound (Device).");
#endif
    }
}
