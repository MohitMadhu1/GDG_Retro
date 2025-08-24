using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using System.Collections.Generic;

public class XRDiag : MonoBehaviour
{
    void Awake()  { Debug.Log("[XRDiag] Awake"); }
    void OnEnable(){ Debug.Log("[XRDiag] OnEnable"); }
    void Start()
    {
        Debug.Log("[XRDiag] Start");
        var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
        Debug.Log($"[XRDiag] Active XR Loader: {(loader ? loader.name : "none")}");

        var hands = new List<XRHandSubsystem>();
        SubsystemManager.GetInstances(hands);
        Debug.Log($"[XRDiag] XRHandSubsystem instances: {hands.Count}");
    }
}
