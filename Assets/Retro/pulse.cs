using UnityEngine;

[DisallowMultipleComponent]
public class GridPulseController : MonoBehaviour
{
    // ── Assigned by your RoomBuilder ─────────────────────────────────────────
    public Renderer floorRenderer;
    public Renderer ceilingRenderer;
    public Material sharedPulseMaterial;

    // ── Hard‑coded look (from your screenshot) ──────────────────────────────
    const int   kPulseMode   = 0;      // 0 = Radial
    const float kPulseAmp    = 20f;
    const float kPulseSpeed  = 5f;
    const float kPulsePeriod = 6f;
    const float kPulseWidth  = 2f;

    static readonly Vector2 kCenterXZ = Vector2.zero;

    const float kGridTiling = 1.25f;
    const float kLineWidth  = 0.053f;

    static readonly Color kBaseColor = new Color(0f, 0f, 0f, 1f);   // pure black
    static readonly Color kLineColor = new Color(1f, 0f, 1f, 1f);   // magenta
    const float kGlow = 0.4f;

    // ────────────────────────────────────────────────────────────────────────
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (!sharedPulseMaterial) return;

        ApplyTo(floorRenderer);
        ApplyTo(ceilingRenderer);
    }

    void ApplyTo(Renderer r)
    {
        if (!r) return;

        r.GetPropertyBlock(_mpb);

        // grid look
        _mpb.SetColor("_BaseColor", kBaseColor);
        _mpb.SetColor("_LineColor", kLineColor);
        _mpb.SetFloat("_Glow", kGlow);

        _mpb.SetFloat("_GridTiling", kGridTiling);
        _mpb.SetFloat("_LineWidth",  kLineWidth);

        // pulse
        _mpb.SetFloat("_PulseMode",   kPulseMode);
        _mpb.SetFloat("_PulseAmp",    kPulseAmp);
        _mpb.SetFloat("_PulseSpeed",  kPulseSpeed);
        _mpb.SetFloat("_PulsePeriod", kPulsePeriod);
        _mpb.SetFloat("_PulseWidth",  kPulseWidth);
        _mpb.SetVector("_PulseCenter", new Vector4(kCenterXZ.x, 0f, kCenterXZ.y, 0f));

        r.SetPropertyBlock(_mpb);
    }
}
