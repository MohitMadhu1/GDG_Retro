// RoomBuilder_TronShinyMatrix.cs
// Retro arena + Glyph-on-Wall Game (GAZE TO REVEAL) + Scrolling Code Walls
// Notes:
// - Poke to start (Space key dev shortcut).
// - Glyph: spawns on wall, look at it (default 3s) to reveal → win flow.
// - Uses absolute unscaled time for gaze & lifetimes (Quest-safe).

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using UnityEngine.XR.Management;
using System.Collections.Generic;
using UnityEngine.Scripting;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Hands;
#if UNITY_XR_INTERACTION_TOOLKIT
using UnityEngine.XR.Interaction.Toolkit;
#endif
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// ─────────────────────────────────────────────────────────────────────────────
// Room Builder
// ─────────────────────────────────────────────────────────────────────────────
[DisallowMultipleComponent]
public class RoomBuilder_TronShinyMatrix : MonoBehaviour
{
    // XR Hands
    XRHandSubsystem _handSubsystem;
    static readonly List<XRHandSubsystem> __handsTmp = new List<XRHandSubsystem>(1);

    bool EnsureHandsBound()
    {
        if (_handSubsystem != null) return true;
        __handsTmp.Clear();
        SubsystemManager.GetInstances(__handsTmp);
        if (__handsTmp.Count > 0) { _handSubsystem = __handsTmp[0]; return true; }
        return false;
    }

    // ────────── Room Config ──────────
    [Header("Room Size (meters)")]
    public Vector3 roomSize = new Vector3(12f, 3.2f, 12f);
    [Min(0.001f)] public float wallThickness = 0.02f;
    Vector3 Half => roomSize * 0.5f;

    [Header("Materials (assign or auto)")]
    public Material gridTileMat;        // floor/ceiling
    public Material wallBaseMat;        // dark base
    public Material wallShinyEdgeMat;   // legacy overlay
    public Material pillarMat;          // glass/emissive
    public Material accentMat;          // decor emissive
    [Tooltip("Material for green glyphs (Unlit with emission recommended)")]
    public Material glyphMat;

    // NEW: wall style selector
    public enum WallStyle { BaseOnly, ShinyEdgesOverlay, ScrollingCode }

    [Header("Wall Visual Style")]
    public WallStyle wallStyle = WallStyle.ScrollingCode;
    [Tooltip("Material using Evolvium/ScrollTextCodeURP_Blue (or Safe)")]
    public Material wallCodeMat;
    [Tooltip("Leave (0,0) to use material tiling")]
    public Vector2 wallCodeTilingOverride = Vector2.zero;

    [Header("Start Screen (Canvas + TMP only)")]
    public Canvas startCanvas;
    public TextMeshProUGUI startText;
    public bool pokeRightToStart = true;
    [TextArea(2,6)]
    public string startMessage =
        "<size=170%><b>NEON ARENA</b></size>\n\n<size=110%><b>Poke to start</b></size>\n<size=85%>Index finger straight, other fingers curled.</size>\n<size=70%><alpha=#88>Dev shortcut: press <b>Space</b></alpha></size>";

    [Header("Win Screen (Canvas + TMP)")]
    public Canvas winCanvas;
    public TextMeshProUGUI winText;
    [TextArea(2,6)]
    public string winMessage =
        "<size=100%><b>You destroyed the secret shape!</b></size>\n\n<size=80%>It contained a secret key that says</size>\n<size=140%><b>GOBLIN MACHINE!!!</b></size>";

    bool _started = false;
    bool _wasPointing = false;
    float _pulseT = 0f;

    [Header("Build Options")]
    public bool spawnAroundCamera = true;
    public float floorY = 0f;
    public bool buildCeilingGrid = true;

    // ───────── Teaser (post-poke) ─────────
    [Header("Teaser (after poke, before game)")]
    public bool useTeaser = true;
    [Min(1f)] public float teaserSeconds = 10f;
    [TextArea(2, 6)]
    public string teaserMessage =
        "<size=100%><b>There is a <color=#66e0ff>glitch spot</color> in the room.</b></size>\n" +
        "Point towards it for <b>3 seconds</b> to find the secret key.\n" +
        "<size=80%>Be careful — the drones are watching.</size>\n\n" +
        "<size=65%>Game starts in <b>{0}</b> seconds...</size>";

    bool _inTeaser = false;
    float _teaserEndT = 0f;


    [Header("Audio")]
    public AudioClip backgroundMusic;
    public bool loopMusic = true;
    public float musicVolume = 0.7f;

    AudioSource musicSource;



    [Header("Content")]
    public bool addPillars = true;
    [Tooltip("Enable Matrix-like UV curtains in front of walls")]
    public bool addDataRain = false;
    public bool addSecretKey = true; // preserved (centerpiece)
    public bool addReflectionProbe = true;
    public bool addGlobalVolume = true;

    [Header("Decorations")]
    public bool addCornerCaps = true;
    public bool addCeilingStuds = true;

    [Header("Pillars")]
    public float pillarRadius = 0.22f;
    public float pillarClearMargin = 0.10f;

    [Header("Renderer Flags")]
    public bool disableShadows = true;

    // ───────── Glyph Game (replaces floaters) ─────────
    [Header("Glyph Game (Wall Spawner)")]
    public bool enableGlyphGame = true;
    public float glyphSpawnInterval = 2.75f;
    public float glyphLifetime = 5.0f;
    public float glyphGazeSeconds = 3.0f;
    public float glyphMaxAngle = 10.0f;
    public Vector2 glyphScaleRange = new Vector2(0.35f, 0.6f);
    [Tooltip("Inset from wall edges (x: horizontal, y: vertical)")]
    public Vector2 glyphWallInsetXY = new Vector2(0.25f, 0.25f);
    public bool oneGlyphAtATime = true;

    // ────────── Internals ──────────
    const string RootName = "RoomRoot";
    Transform root;

    // keep references to the 4 base walls for glyph placement
    Transform wallN, wallS, wallE, wallW;

    // Glyph spawner
    private GlyphWallSpawner glyphSpawner;


    // ---- DEBUG ----
    float _dbgNextLog = 0f;
    string _dbgLastReason = "";
    bool _dbgLoggedPokeEdge = false;

    // ────────── Utility ──────────
    void ShowStartUI()
    {
        if (startCanvas) startCanvas.enabled = true;
        if (startText)
        {
            startText.enabled = true;
            startText.alignment = TextAlignmentOptions.Center;
            startText.text = startMessage;
        }
    }
    void ClearStartTextOnly() { if (startText) startText.text = ""; }

    void LogXRState()
    {
        var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
        Debug.Log($"[RB] Active XR Loader: {(loader ? loader.name : "none")}");
        var hands = new List<XRHandSubsystem>(); SubsystemManager.GetInstances(hands);
        Debug.Log($"[RB] XRHandSubsystem instances: {hands.Count}");
    }

    void HideWinUI() { if (winText) winText.enabled = false; if (winCanvas) winCanvas.enabled = false; }
    void ShowWinUI()
    {
        if (winText)
        {
            winText.text = string.IsNullOrEmpty(winMessage)
                ? "You destroyed the secret shape!\nGOBLIN MACHINE!!!"
                : winMessage;
            winText.enabled = true;
        }
        if (winCanvas) winCanvas.enabled = true;
    }


    void StartTeaser()
    {
        if (startCanvas) startCanvas.enabled = true;
        if (startText)
        {
            startText.enabled = true;
            startText.alignment = TextAlignmentOptions.Center;
            startText.text = string.Format(teaserMessage, Mathf.CeilToInt(teaserSeconds));
        }

        _inTeaser = true;
        _teaserEndT = Time.unscaledTime + teaserSeconds;

        if (glyphSpawner) glyphSpawner.enabled = false;
    }

    void UpdateTeaserUI()
    {
        if (!startText) return;
        int remain = Mathf.Max(0, Mathf.CeilToInt(_teaserEndT - Time.unscaledTime));
        startText.text = string.Format(teaserMessage, remain);
    }

    void StartGame()
    {
        Time.timeScale = 1f;       // ensure unpaused
        Time.fixedDeltaTime = 0.02f;
        _started = true;

        ClearStartTextOnly();
        HideWinUI();

        if (enableGlyphGame && glyphSpawner != null)
        {
            glyphSpawner.enabled = true;
            glyphSpawner.Begin(root, new[] { wallN, wallS, wallE, wallW });
        }
    }

    bool DetectPointGesture(bool rightHand, bool logEdge = false)
    {
        if (_handSubsystem == null) return false;
        var hand = rightHand ? _handSubsystem.rightHand : _handSubsystem.leftHand;
        if (!hand.isTracked)
        {
            if (Time.time >= _dbgNextLog)
            {
                Debug.Log($"[RB] {(rightHand ? "Right" : "Left")} hand not tracked.");
                _dbgNextLog = Time.time + 1f;
            }
            return false;
        }

        float i = GetFingerBend(hand, XRHandJointID.IndexTip, XRHandJointID.IndexIntermediate, XRHandJointID.IndexProximal);
        float m = GetFingerBend(hand, XRHandJointID.MiddleTip, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleProximal);
        float r = GetFingerBend(hand, XRHandJointID.RingTip, XRHandJointID.RingIntermediate, XRHandJointID.RingProximal);
        float l = GetFingerBend(hand, XRHandJointID.LittleTip, XRHandJointID.LittleIntermediate, XRHandJointID.LittleProximal);

        bool pointing = (i < 0.30f && m > 0.70f && r > 0.70f && l > 0.70f);

        if (logEdge && pointing && !_dbgLoggedPokeEdge)
        {
            _dbgLoggedPokeEdge = true;
            Debug.Log($"[RB] Poke({(rightHand ? "R" : "L")}) edge. bends: idx={i:F2}, mid={m:F2}, ring={r:F2}, lit={l:F2}");
        }
        if (!pointing) _dbgLoggedPokeEdge = false;

        return pointing;
    }

    float GetFingerBend(XRHand hand, XRHandJointID tip, XRHandJointID pip, XRHandJointID mcp)
    {
        var tipJ = hand.GetJoint(tip);
        var pipJ = hand.GetJoint(pip);
        var mcpJ = hand.GetJoint(mcp);
        Pose tipPose, pipPose, mcpPose;
        if (tipJ.TryGetPose(out tipPose) && pipJ.TryGetPose(out pipPose) && mcpJ.TryGetPose(out mcpPose))
        {
            float bend = Vector3.Angle(tipPose.position - pipPose.position, pipPose.position - mcpPose.position);
            return Mathf.Clamp01((bend - 0f) / (80f - 0f)); // 0 straight, 1 very bent
        }
        return 1f;
    }

    // ────────── Unity Messages ──────────
    void Start()
    {
        Rebuild();

        if (startCanvas) startCanvas.enabled = true;
        if (startText)
        {
            startText.enabled = true;
            startText.alignment = TextAlignmentOptions.Center;
            startText.text = startMessage;
        }
        if (backgroundMusic)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = backgroundMusic;
            musicSource.loop = loopMusic;
            musicSource.volume = musicVolume;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f; // 2D sound
            musicSource.Play();
        }


        ShowStartUI();
        HideWinUI();
        LogXRState();
        EnsureHandsBound();
        StartCoroutine(TryBindHands());

        if (glyphSpawner) glyphSpawner.enabled = false;
    }

    void Update()
    {
        if (_handSubsystem == null) EnsureHandsBound();
        if (_started) return;

        // Animate start title
        _pulseT += Time.deltaTime * 2.0f;
        if (startText)
        {
            float pulse = Mathf.Lerp(0.92f, 1.10f, (Mathf.Sin(_pulseT) + 1f) * 0.5f);
            startText.fontSize = 36f * pulse;
        }

        string reason = "";
        if (_handSubsystem == null) reason = "No XRHandSubsystem";
        else if (startCanvas == null) reason = "startCanvas == null";
        else if (!startCanvas.enabled) reason = "startCanvas disabled";

        if (!string.IsNullOrEmpty(reason))
        {
            if (Time.time >= _dbgNextLog || reason != _dbgLastReason)
            {
                Debug.Log($"[RB] Waiting: {reason}");
                _dbgNextLog = Time.time + 1f;
                _dbgLastReason = reason;
            }
            if (startText)
                startText.text = $"<b>◆ NEON ARENA ◆</b>\n\n<color=#ff8888>Waiting: {reason}</color>\nPoint (poke) to start";
            if (Input.GetKeyDown(KeyCode.Space)) StartGame();
            return;
        }

        bool pointingR = DetectPointGesture(true, logEdge: true);
        bool pointingL = DetectPointGesture(false, logEdge: true);
        bool pointing = pokeRightToStart ? pointingR : (pointingR || pointingL);

        if (startText)
        {
            string handStr = pokeRightToStart ? "RIGHT" : "RIGHT or LEFT";
            string status = pointing ? "<color=#4CFF8A>POKE DETECTED</color>" : "<color=#66d9ff>waiting...</color>";
            startText.text =
                "<size=160%><b>◆◆◆  NEON ARENA  ◆◆◆</b></size>\n\n" +
                $"Poke ({handStr}) to start\n{status}";
        }


        if (!_started && !_inTeaser)
        {
            if (pointing && !_wasPointing)
            {
                if (useTeaser)
                    StartTeaser();
                else
                    StartGame();
            }
        }
        _wasPointing = pointing;

        // Space key: skip straight to game
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_inTeaser) { _inTeaser = false; }
            StartGame();
        }

        // Teaser countdown handling
        if (_inTeaser)
        {
            UpdateTeaserUI();
            if (Time.unscaledTime >= _teaserEndT)
            {
                _inTeaser = false;
                StartGame();
            }
            return; // don’t run poke-UI while teaser active
        }
    }

    IEnumerator TryBindHands(float timeout = 6f)
    {
        float t0 = Time.time; var list = new List<XRHandSubsystem>();

        while ((XRGeneralSettings.Instance == null ||
                XRGeneralSettings.Instance.Manager == null ||
                XRGeneralSettings.Instance.Manager.activeLoader == null) &&
               Time.time - t0 < timeout)
            yield return null;

        while (_handSubsystem == null && Time.time - t0 < timeout)
        {
            list.Clear(); SubsystemManager.GetInstances(list);
            if (list.Count > 0) { _handSubsystem = list[0]; break; }
            yield return null;
        }

        if (_handSubsystem == null)
            Debug.LogWarning("[RB] No XRHandSubsystem. Enable XR Hands in OpenXR Features and run on device/headset.");
    }

    // ────────── Build / Clear ──────────
    [ContextMenu("Rebuild Room")]
    public void Rebuild()
    {
        ClearPreviousBuild();
        EnsureMaterials();

        root = new GameObject(RootName).transform;
        root.SetParent(transform, false);
        root.position = GetAnchorPosition();

        BuildShell(); // floor, ceiling, base walls (+colliders)

        if (wallStyle == WallStyle.ShinyEdgesOverlay)
            BuildShinyEdgeWallsAll(); // skip overlays for ScrollingCode

        if (addPillars) PlaceCornerPillarsInside();


        if (addSecretKey) PlaceSecretKey();

        if (addCornerCaps) PlaceCornerCaps(false);
        if (addCeilingStuds) PlaceCeilingStuds(5);

        if (addReflectionProbe) AddRoomReflectionProbe();
        if (addGlobalVolume) EnsureGlobalPostFX();

        // Glyph spawner setup (replaces floater systems)
        if (enableGlyphGame)
        {
            glyphSpawner = GetComponent<GlyphWallSpawner>();
            if (!glyphSpawner) glyphSpawner = gameObject.AddComponent<GlyphWallSpawner>();
            glyphSpawner.Configure(
                glyphSpawnInterval, glyphLifetime, glyphGazeSeconds, glyphMaxAngle,
                glyphScaleRange, glyphWallInsetXY, oneGlyphAtATime, glyphMat   // <-- removed accentMat
            );
            glyphSpawner.roomController = this;
            glyphSpawner.enabled = false; // start after menu
        }


#if UNITY_EDITOR
        EditorApplication.delayCall += () => SceneView.RepaintAll();
        if (!Application.isPlaying) EditorSceneManager.MarkAllScenesDirty();
#endif
    }

    [ContextMenu("Clear Room")]
    public void ClearPreviousBuild()
    {
        var existing = transform.Find(RootName);
        if (existing)
        {
#if UNITY_EDITOR
            DestroyImmediate(existing.gameObject);
#else
            Destroy(existing.gameObject);
#endif
        }
        string[] auto = { "PostFX_Global (Auto)", "RoomProbe (Auto)", "GlyphWall" };
        foreach (var n in auto)
        {
            var t = transform.Find(n);
            if (t)
            {
#if UNITY_EDITOR
                DestroyImmediate(t.gameObject);
#else
                Destroy(t.gameObject);
#endif
            }
        }
        var sp = GetComponent<GlyphWallSpawner>();
        if (sp)
        {
#if UNITY_EDITOR
            DestroyImmediate(sp);
#else
            Destroy(sp);
#endif
        }
    }

    // ────────── Anchoring ──────────
    Vector3 GetAnchorPosition()
    {
        if (!spawnAroundCamera) return new Vector3(transform.position.x, floorY, transform.position.z);
        var cam = Camera.main;
        return cam ? new Vector3(cam.transform.position.x, floorY, cam.transform.position.z) : new Vector3(0f, floorY, 0f);
    }

    // ────────── Structure ──────────
    void BuildShell()
    {
        // Floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.SetParent(root, false);
        floor.transform.localScale = new Vector3(roomSize.x / 10f, 1f, roomSize.z / 10f);
        var floorR = floor.GetComponent<Renderer>();
        if (disableShadows) { floorR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; floorR.receiveShadows = false; }

        // Ceiling
        var ceil = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ceil.name = "Ceiling";
        ceil.transform.SetParent(root, false);
        ceil.transform.localPosition = new Vector3(0, roomSize.y, 0);
        ceil.transform.localRotation = Quaternion.Euler(180, 0, 0);
        ceil.transform.localScale = new Vector3(roomSize.x / 10f, 1f, roomSize.z / 10f);
        var ceilR = ceil.GetComponent<Renderer>();
        if (disableShadows) { ceilR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ceilR.receiveShadows = false; }

        // Use pulse material if present, else grid
        var safePulse = Shader.Find("Evolvium/NeonTileGridPulseURP_Safe");
        Material sharedMat = null;
        if (safePulse)
        {
            sharedMat = new Material(safePulse);
            floorR.sharedMaterial = sharedMat; ceilR.sharedMaterial = sharedMat;
        }
        else
        {
            SetMatAndFlags(floor, gridTileMat, keepCollider: true);
            SetMatAndFlags(ceil, buildCeilingGrid ? gridTileMat : wallBaseMat, keepCollider: false);
        }

#if UNITY_XR_INTERACTION_TOOLKIT
        var ta = floor.AddComponent<TeleportationArea>();
        ta.teleportationProvider = FindFirstObjectByType<TeleportationProvider>();
#endif

        var ctrl = GetComponent<GridPulseController>();
        if (!ctrl) ctrl = gameObject.AddComponent<GridPulseController>();
        ctrl.floorRenderer = floorR; ctrl.ceilingRenderer = ceilR; ctrl.sharedPulseMaterial = sharedMat;

        // Base walls with colliders (store references for glyphs)
        wallN = BuildWall("N_Base", new Vector3(0, Half.y, Half.z), new Vector3(0, 180, 0), roomSize.x, roomSize.y);
        wallS = BuildWall("S_Base", new Vector3(0, Half.y, -Half.z), new Vector3(0, 0, 0), roomSize.x, roomSize.y);
        wallE = BuildWall("E_Base", new Vector3(Half.x, Half.y, 0), new Vector3(0, -90, 0), roomSize.z, roomSize.y);
        wallW = BuildWall("W_Base", new Vector3(-Half.x, Half.y, 0), new Vector3(0, 90, 0), roomSize.z, roomSize.y);
    }

    // Returns the created wall's Transform so callers can store N/S/E/W for glyph logic.
    Transform BuildWall(string label, Vector3 pos, Vector3 euler, float width, float height)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Wall_" + label;
        q.transform.SetParent(root, false);
        q.transform.localPosition    = pos;
        q.transform.localEulerAngles = euler;
        q.transform.localScale       = new Vector3(width, height, 1f);

        var r = q.GetComponent<Renderer>();

        if (wallStyle == WallStyle.ScrollingCode && wallCodeMat)
        {
            var inst = new Material(wallCodeMat); // per-wall instance
            // Opaque + ZWrite + Double‑Sided so it renders from inside on Quest
            if (inst.HasProperty("_Surface"))   inst.SetFloat("_Surface", 0f);
            if (inst.HasProperty("_Blend"))     inst.SetFloat("_Blend", 0f);
            if (inst.HasProperty("_ZWrite"))    inst.SetFloat("_ZWrite", 1f);
            if (inst.HasProperty("_AlphaClip")) inst.SetFloat("_AlphaClip", 0f);
            if (inst.HasProperty("_SrcBlend"))  inst.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (inst.HasProperty("_DstBlend"))  inst.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            if (inst.HasProperty("_Cull"))      inst.SetFloat("_Cull", 0f); // double‑sided

            r.sharedMaterial = inst;

            if (wallCodeTilingOverride != Vector2.zero && inst.HasProperty("_BaseMap_ST"))
            {
                var st = inst.GetVector("_BaseMap_ST"); // (tiling.xy, offset.xy)
                st.x = wallCodeTilingOverride.x;
                st.y = wallCodeTilingOverride.y;
                inst.SetVector("_BaseMap_ST", st);
            }

            if (disableShadows)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows    = false;
            }
        }
        else
        {
            // Base opaque walls with collider for blocking
            SetMatAndFlags(q, wallBaseMat, keepCollider: true);

            // Ensure base mat is opaque + double‑sided as well
            var m = r.sharedMaterial;
            if (m)
            {
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
                if (m.HasProperty("_Blend"))   m.SetFloat("_Blend", 0f);
                if (m.HasProperty("_ZWrite"))  m.SetFloat("_ZWrite", 1f);
                if (m.HasProperty("_Cull"))    m.SetFloat("_Cull", 0f);
            }
        }

        return q.transform;
    }



    // SHINY EDGE OVERLAYS (legacy)
    void BuildShinyEdgeWallsAll()
    {
        if (!wallShinyEdgeMat)
        {
            Debug.LogWarning("wallShinyEdgeMat is missing; skipping shiny edge overlays.");
            return;
        }

        float b = wallThickness + 0.01f;

        BuildEdgeOverlay("Wire_N", new Vector3(0, Half.y, Half.z - b), new Vector3(0, 0, 0), roomSize.x, roomSize.y, axisHint: 0, flipInward: false);
        BuildEdgeOverlay("Wire_S", new Vector3(0, Half.y, -Half.z + b), new Vector3(0, 180, 0), roomSize.x, roomSize.y, axisHint: 0, flipInward: false);
        BuildEdgeOverlay("Wire_E", new Vector3(Half.x - b, Half.y, 0), new Vector3(0, -90, 0), roomSize.z, roomSize.y, axisHint: 1, flipInward: true);
        BuildEdgeOverlay("Wire_W", new Vector3(-Half.x + b, Half.y, 0), new Vector3(0, 90, 0), roomSize.z, roomSize.y, axisHint: 1, flipInward: true);
    }

    void ForceOpaque(Material m)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);  // double-sided
        if (m.HasProperty("_BaseColor")) { var c = m.GetColor("_BaseColor"); c.a = 1f; m.SetColor("_BaseColor", c); }
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    // ────────── Pillars ──────────
    void PlaceCornerPillarsInside()
    {
        float inward = pillarRadius + pillarClearMargin + wallThickness;
        Vector3[] corners =
        {
            new Vector3( Half.x - inward, 0,  Half.z - inward),
            new Vector3(-Half.x + inward, 0,  Half.z - inward),
            new Vector3( Half.x - inward, 0, -Half.z + inward),
            new Vector3(-Half.x + inward, 0, -Half.z + inward),
        };

        for (int i = 0; i < corners.Length; i++)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p.name = $"GlassPillar_{i + 1}";
            p.transform.SetParent(root, false);

            float halfHeight = roomSize.y * 0.5f;
            p.transform.localScale = new Vector3(pillarRadius * 2f, halfHeight, pillarRadius * 2f);
            p.transform.localPosition = new Vector3(corners[i].x, halfHeight, corners[i].z);

            SetMatAndFlags(p, pillarMat, keepCollider: false, transparent: true);

            var pulse = p.AddComponent<PulseEmission>();
            pulse.intensity = 2.1f + i * 0.2f;
            pulse.baseColor = new Color(1f, 0.35f, 0.95f);
        }
    }

    void BuildEdgeOverlay(string name, Vector3 pos, Vector3 euler,
                        float width, float height, int axisHint, bool flipInward)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        q.transform.SetParent(root, false);
        q.transform.localPosition = pos;
        q.transform.localEulerAngles = euler;
        q.transform.localScale = new Vector3(width, height, 1f);
        if (flipInward) q.transform.localEulerAngles += new Vector3(0f, 180f, 0f);

        var col = q.GetComponent<Collider>();
    #if UNITY_EDITOR
        if (col) DestroyImmediate(col);
    #else
        if (col) Destroy(col);
    #endif

        var inst = new Material(wallShinyEdgeMat);
        ForceOpaque(inst); inst.renderQueue = Mathf.Max(inst.renderQueue, 2450);

        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = inst;

        var mpb = new MaterialPropertyBlock();
        mpb.SetFloat("_EdgeAxis", axisHint);
        mpb.SetFloat("_AxisSwap", axisHint);
        mpb.SetColor("_EdgeColor", new Color(0.10f, 0.90f, 1f));
        mpb.SetFloat("_EdgeIntensity", 1.6f);
        mpb.SetFloat("_EdgeWidth", 0.015f);
        r.SetPropertyBlock(mpb);

        if (disableShadows)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    // ────────── Matrix-like Curtains (UV-scrolled quads) ──────────
    void PlaceCurtainsUVAll()
    {
        PlaceCurtainUV("DataRain_N", new Vector3(0, Half.y, Half.z),   new Vector3(0,   0, 0),  0.72f);
        PlaceCurtainUV("DataRain_S", new Vector3(0, Half.y, -Half.z),  new Vector3(0, 180, 0),  0.95f);
        PlaceCurtainUV("DataRain_E", new Vector3(Half.x, Half.y, 0),   new Vector3(0,  90, 0),  0.82f);
        PlaceCurtainUV("DataRain_W", new Vector3(-Half.x, Half.y, 0),  new Vector3(0, -90, 0),  1.05f);
    }

    void PlaceCurtainUV(string name, Vector3 wallCenter, Vector3 euler, float speed)
    {
        float marginInside = wallThickness + 0.02f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name; go.transform.SetParent(root, false);
        go.transform.localEulerAngles = euler;

        Vector3 inward = -go.transform.forward;
        go.transform.localPosition = wallCenter + inward * marginInside;

        bool NS = Mathf.Approximately(Mathf.Abs(euler.y) % 180f, 0f);
        float width  = NS ? (roomSize.x - 2f * marginInside) : (roomSize.z - 2f * marginInside);
        float height = roomSize.y * 0.95f;
        go.transform.localScale = new Vector3(width, height, 1f);

        var col = go.GetComponent<Collider>();
#if UNITY_EDITOR
        if (col) DestroyImmediate(col);
#else
        if (col) Destroy(col);
#endif

        var mr = go.GetComponent<MeshRenderer>();
        var mat = MakeDataRainMaterial();
        mr.sharedMaterial = mat;

        // Draw before UI transparents; opaque walls still occlude by depth
        mat.renderQueue = 2950;
        if (mat.HasProperty("_QueueOffset")) mat.SetFloat("_QueueOffset", -25f);
        mr.sortingOrder = -100;

        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        var scroller = go.AddComponent<UVScroller>();
        scroller.speedY = -speed;
        scroller.varianceX = 0.15f;
    }

    Material MakeDataRainMaterial()
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // Transparent
        if (m.HasProperty("_Blend"))   m.SetFloat("_Blend",   1f); // Additive
        if (m.HasProperty("_Cull"))    m.SetFloat("_Cull",    2f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);

        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor"))
            m.SetColor("_EmissionColor", new Color(0.75f, 0.9f, 1f) * 1.2f);

        Texture2D tex = GenerateStripeTexture(256, 512);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); else m.mainTexture = tex;

        return m;
    }

    Texture2D GenerateStripeTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        var clear = new Color(0, 0, 0, 0);
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = clear;

        int stripes = Mathf.RoundToInt(w * 0.18f);
        var rand = new System.Random();

        for (int s = 0; s < stripes; s++)
        {
            int x = rand.Next(0, w);
            int thickness = 1 + (rand.NextDouble() < 0.15 ? 1 : 0);
            float hue = Mathf.Lerp(0.55f, 0.85f, (float)rand.NextDouble());
            Color head = Color.HSVToRGB(hue, 0.25f, 1.0f);
            Color tail = Color.HSVToRGB(Mathf.Repeat(hue + 0.35f, 1f), 0.35f, 1.0f);

            int yOffset = rand.Next(0, h);

            for (int y = 0; y < h; y++)
            {
                int yy = (y + yOffset) % h;
                float t = (float)y / (h - 1);
                float a = Mathf.Pow(1f - t, 0.55f);
                Color c = Color.Lerp(head, tail, t);
                c *= 1.2f; c.a = a;

                for (int dx = 0; dx < thickness; dx++)
                {
                    int xx = (x + dx) % w;
                    int idx = yy * w + xx;

                    Color existing = px[idx];
                    Color blended = new Color(
                        existing.r + c.r * c.a,
                        existing.g + c.g * c.a,
                        existing.b + c.b * c.a,
                        Mathf.Clamp01(existing.a + c.a)
                    );
                    px[idx] = blended;
                }
            }
        }

        tex.SetPixels(px); tex.Apply(false, false);
        return tex;
    }

    // ────────── Secret Key (center decor) ──────────
    void PlaceSecretKey()
    {
        var key = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        key.name = "SecretKey"; key.transform.SetParent(root, false);
        key.transform.localScale = new Vector3(0.12f, 0.25f, 0.12f);
        key.transform.localPosition = new Vector3(0, 0.7f, 0);
        SetMatAndFlags(key, accentMat, keepCollider: false, transparent: true);

        key.AddComponent<PulseEmission>().intensity = 2.6f;
        key.AddComponent<SlowRotate>().degPerSec = new Vector3(0, 35, 0);
        key.AddComponent<FloatBob>().amplitude = 0.05f;
    }

    // ────────── PostFX & Probe ──────────
    void EnsureGlobalPostFX()
    {
        if (transform.Find("PostFX_Global (Auto)")) return;

        var go = new GameObject("PostFX_Global (Auto)"); go.transform.SetParent(transform, false);
        var volume = go.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = -10;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;

        var bloom = profile.Add<Bloom>(true); bloom.intensity.overrideState = true; bloom.intensity.value = 2.0f;
        bloom.threshold.overrideState = true; bloom.threshold.value = 0.82f;
        bloom.scatter.overrideState = true; bloom.scatter.value = 0.7f;

        var tone = profile.Add<Tonemapping>(true); tone.mode.overrideState = true; tone.mode.value = TonemappingMode.ACES;

        var ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.overrideState = true; ca.postExposure.value = 0.25f;
        ca.saturation.overrideState = true; ca.saturation.value = 12f;

        var vign = profile.Add<Vignette>(true); vign.intensity.overrideState = true; vign.intensity.value = 0.28f;
        vign.smoothness.overrideState = true; vign.smoothness.value = 0.8f;

        var film = profile.Add<FilmGrain>(true); film.intensity.overrideState = true; film.intensity.value = 0.12f;
        film.response.overrideState = true; film.response.value = 0.8f;

        var chroma = profile.Add<ChromaticAberration>(true); chroma.intensity.overrideState = true; chroma.intensity.value = 0.06f;
    }

    void AddRoomReflectionProbe()
    {
        if (transform.Find("RoomProbe (Auto)")) return;
        var go = new GameObject("RoomProbe (Auto)");
        go.transform.SetParent(transform, false);
        go.transform.position = root.position + new Vector3(0, roomSize.y * 0.5f, 0);
        var probe = go.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.size = new Vector3(roomSize.x, roomSize.y, roomSize.z);
        probe.boxProjection = true; probe.intensity = 0.6f;
    }

    // ────────── Materials ──────────
    void EnsureMaterials()
    {
        // Helper to force opaque, zwrite, and cull mode
        void ForceOpaqueZWriteDoubleSided(Material m)
        {
            if (!m) return;
            if (m.HasProperty("_Surface"))   m.SetFloat("_Surface", 0f);   // Opaque
            if (m.HasProperty("_Blend"))     m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_ZWrite"))    m.SetFloat("_ZWrite", 1f);    // Depth write ON
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            if (m.HasProperty("_SrcBlend"))  m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_DstBlend"))  m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            if (m.HasProperty("_Cull"))      m.SetFloat("_Cull", 0f);      // 0 = Off (double‑sided)
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        if (!gridTileMat)
            gridTileMat = CreateDefaultMat(
                new[] { "Evolvium/NeonTileGridURP", "Universal Render Pipeline/Unlit" },
                "GridTiles (Auto)", new Color(1f, 0.2f, 0.9f)
            );

        if (!wallBaseMat)
        {
            wallBaseMat = CreateDefaultMat(
                new[] { "Universal Render Pipeline/Lit" },
                "WallsBase (Auto)", Color.black * 0.1f
            );
            if (wallBaseMat.HasProperty("_BaseColor")) wallBaseMat.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.06f));
            if (wallBaseMat.HasProperty("_Smoothness")) wallBaseMat.SetFloat("_Smoothness", 0f);
            if (wallBaseMat.HasProperty("_Metallic"))   wallBaseMat.SetFloat("_Metallic", 0f);
        }

        if (!wallShinyEdgeMat)
            wallShinyEdgeMat = CreateDefaultMat(
                new[] { "Evolvium/ArcadeWall_ShinyEdgeURP", "Universal Render Pipeline/Unlit" },
                "WallsShinyEdges (Auto)", new Color(0f, 1f, 1f)
            );

        if (wallStyle == WallStyle.ScrollingCode && !wallCodeMat)
        {
            wallCodeMat = CreateDefaultMat(
                new[] { "Evolvium/ScrollTextCodeURP_Blue", "Evolvium/ScrollTextCodeURP_Safe", "Universal Render Pipeline/Unlit" },
                "Walls_ScrollCode (Auto)", Color.white
            );
        }

        if (!pillarMat)
            pillarMat = CreateDefaultMat(
                new[] { "Evolvium/GlassTubeURP", "Universal Render Pipeline/Unlit" },
                "Pillars (Auto)", new Color(1f, 0.35f, 0.95f)
            );

        if (!accentMat)
        {
            accentMat = CreateDefaultMat(
                new[] { "Universal Render Pipeline/Unlit" },
                "Accent (Auto)", new Color(1f, 0.35f, 0.95f)
            );
            EnableEmission(accentMat, new Color(1f, 0.35f, 0.95f) * 2.2f);
        }

        // 🔒 Make sure opaque + ZWrite + double‑sided for walls/tiles (Quest safe)
        ForceOpaqueZWriteDoubleSided(gridTileMat);
        ForceOpaqueZWriteDoubleSided(wallBaseMat);
        ForceOpaqueZWriteDoubleSided(wallShinyEdgeMat);
        if (wallCodeMat) ForceOpaqueZWriteDoubleSided(wallCodeMat);

        // Leave pillar glass alone (likely needs transparency); if you want opaque glass, uncomment:
        // ForceOpaqueZWriteDoubleSided(pillarMat);

        // Accent can stay unlit emissive (opaque is fine)
        ForceOpaqueZWriteDoubleSided(accentMat);
    }


    static Material CreateDefaultMat(string[] shaderPaths, string matName, Color baseColor)
    {
        Shader sh = null; foreach (var p in shaderPaths) { sh = Shader.Find(p); if (sh) break; }
        if (!sh) sh = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);
        m.name = matName;
        return m;
    }

    static void EnableEmission(Material m, Color emissive)
    {
        if (!m) return; m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emissive);
    }

    void SetMatAndFlags(GameObject go, Material m, bool keepCollider = false, bool transparent = false)
    {
        var r = go.GetComponent<Renderer>();
        if (r)
        {
            if (m) r.sharedMaterial = m;
            if (disableShadows)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
        if (!keepCollider)
        {
            var col = go.GetComponent<Collider>();
#if UNITY_EDITOR
            if (col) DestroyImmediate(col);
#else
            if (col) Destroy(col);
#endif
        }
        go.isStatic = !transparent;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Vector3 c = Application.isPlaying && root ? root.position : GetAnchorPosition();
        Gizmos.DrawWireCube(new Vector3(c.x, c.y + roomSize.y * 0.5f, c.z), new Vector3(roomSize.x, roomSize.y, roomSize.z));
    }

    // ────────── Decorative add-ons ──────────
    void PlaceCornerCaps(bool alsoTop = false)
    {
        var parent = new GameObject("CornerCaps").transform;
        parent.SetParent(root, false);

        float capRadius = 0.22f;
        float capHeight = 0.05f;
        float yFloor = 0.05f;
        float yTop = roomSize.y - 0.05f;

        Vector3[] bottoms =
        {
            new Vector3( Half.x - wallThickness, yFloor,  Half.z - wallThickness),
            new Vector3(-Half.x + wallThickness, yFloor,  Half.z - wallThickness),
            new Vector3( Half.x - wallThickness, yFloor, -Half.z + wallThickness),
            new Vector3(-Half.x + wallThickness, yFloor, -Half.z + wallThickness),
        };

        foreach (var pos in bottoms)
        {
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "CornerCap"; cap.transform.SetParent(parent, false);
            cap.transform.localPosition = pos;
            cap.transform.localScale = new Vector3(capRadius * 2f, capHeight * 0.5f, capRadius * 2f);
            SetMatAndFlags(cap, accentMat, keepCollider: false, transparent: true);

            var pulse = cap.AddComponent<PulseEmission>();
            pulse.intensity = 2.1f; pulse.baseColor = new Color(0.10f, 1f, 0.9f);
        }

        if (!alsoTop) return;

        Vector3[] tops =
        {
            new Vector3( Half.x - wallThickness, yTop,  Half.z - wallThickness),
            new Vector3(-Half.x + wallThickness, yTop,  Half.z - wallThickness),
            new Vector3( Half.x - wallThickness, yTop, -Half.z + wallThickness),
            new Vector3(-Half.x + wallThickness, yTop, -Half.z + wallThickness),
        };

        foreach (var pos in tops)
        {
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "CornerCap_Top"; cap.transform.SetParent(parent, false);
            cap.transform.localPosition = pos;
            cap.transform.localScale = new Vector3(capRadius * 2f, capHeight * 0.5f, capRadius * 2f);
            SetMatAndFlags(cap, accentMat, keepCollider: false, transparent: true);

            var pulse = cap.AddComponent<PulseEmission>();
            pulse.intensity = 1.8f; pulse.baseColor = new Color(0.8f, 0.3f, 1f);
        }
    }

    void PlaceCeilingStuds(int perEdge = 6)
    {
        var parent = new GameObject("CeilingStuds").transform; parent.SetParent(root, false);

        perEdge = Mathf.Max(1, perEdge);
        float y = roomSize.y - 0.03f;
        float inset = wallThickness + 0.06f;
        float sphereR = 0.05f;

        void SpawnLine(Vector3 a, Vector3 b)
        {
            for (int i = 0; i <= perEdge; i++)
            {
                float t = (float)i / perEdge;
                var p = Vector3.Lerp(a, b, t);
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "CeilStud"; s.transform.SetParent(parent, false);
                s.transform.localPosition = p + new Vector3(0, y, 0);
                s.transform.localScale = Vector3.one * (sphereR * 2f);
                SetMatAndFlags(s, accentMat, keepCollider: false, transparent: true);

                var pulse = s.AddComponent<PulseEmission>();
                pulse.intensity = 1.6f + Mathf.PingPong(i * 0.1f, 0.3f);
                pulse.baseColor = new Color(0.2f, 0.95f, 1f);
            }
        }

        Vector3 NE = new Vector3(Half.x - inset, 0, Half.z - inset);
        Vector3 NW = new Vector3(-Half.x + inset, 0, Half.z - inset);
        Vector3 SE = new Vector3(Half.x - inset, 0, -Half.z + inset);
        Vector3 SW = new Vector3(-Half.x + inset, 0, -Half.z + inset);

        SpawnLine(NW, NE); SpawnLine(NE, SE); SpawnLine(SE, SW); SpawnLine(SW, NW);
    }

    // ────────── Callbacks ──────────
    public void OnSecretDestroyed() { ShowWinUI(); }
}

// ─────────────────────────────────────────────────────────────────────────────
// Gaze-to-destroy for glyph (attach to spawned glyphs)
// ─────────────────────────────────────────────────────────────────────────────
[DisallowMultipleComponent]
public class GazeDestroyGlyph : MonoBehaviour
{
    public float requiredLookTime = 3f;
    public float maxAngle = 10f;
    public GlyphWallSpawner spawner;
    public Renderer glyphRenderer;

    float gazeTime;
    Camera cam;
    MaterialPropertyBlock mpb;
    Color baseEmiss = Color.white;
    bool hasEmission;



    void Start()
    {
        cam = Camera.main;
        mpb = new MaterialPropertyBlock();
        if (glyphRenderer && glyphRenderer.sharedMaterial && glyphRenderer.sharedMaterial.HasProperty("_EmissionColor"))
        {
            hasEmission = true;
            baseEmiss = glyphRenderer.sharedMaterial.GetColor("_EmissionColor");
        }
    }

    void Update()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        // If our renderer was (or gets) destroyed by the spawner, stop safely.
        if (!glyphRenderer)
        {
            enabled = false;
            return;
        }

        Vector3 dir = (transform.position - cam.transform.position).normalized;
        bool looking = Vector3.Angle(cam.transform.forward, dir) < maxAngle;

        // Gaze timer (uses unscaled time)
        if (looking) gazeTime += Time.unscaledDeltaTime;
        else         gazeTime = 0f;

        // Optional emission feedback
        if (hasEmission && glyphRenderer)
        {
            glyphRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", looking ? (baseEmiss * 1.5f) : baseEmiss);
            glyphRenderer.SetPropertyBlock(mpb);
        }

        // Win: notify spawner, then STOP updating. Do NOT self-destroy here.
        if (gazeTime >= requiredLookTime)
        {
            enabled = false;                 // prevent further Update calls
            var s = spawner;                 // cache in case object graph changes
            if (s) s.NotifyGlyphWon();       // spawner will clear/destroy glyphs
            return;
        }
    }


}


// ─────────────────────────────────────────────────────────────────────────────
// Glyph Wall Spawner (spawns a green glyph quad on random wall)
// ─────────────────────────────────────────────────────────────────────────────
// ─────────────────────────────────────────────────────────────────────────────
// Glyph Wall Spawner (fixed: robust on-wall placement + inward offset)
// ─────────────────────────────────────────────────────────────────────────────



[DisallowMultipleComponent]
public class GlyphWallSpawner : MonoBehaviour
{
    [Header("Room Bounds (for inward calc)")]
    public Vector3 roomSize = new Vector3(12f, 3.2f, 12f);

    [Header("Spawn Rules")]
    public float spawnInterval = 2.75f;
    public float glyphLifetime = 5.0f;
    public Vector2 glyphScaleRange = new Vector2(0.35f, 0.6f);
    public Vector2 wallInsetXY = new Vector2(0.25f, 0.25f);
    public bool oneGlyphAtATime = true;

    [Header("Glyph Visuals")]
    public float glyphThickness = 0.06f;
    public float embedEpsilon = 0.01f;

    [Header("Gaze Settings")]
    public float gazeSeconds = 3.0f;
    public float maxGazeAngle = 10.0f;

    [Header("Materials")]
    public Material glyphMat;     // optional override

    [Header("Game Hook")]
    public RoomBuilder_TronShinyMatrix roomController;

    // runtime
    Transform[] walls = null;
    Transform glyphRoot;
    Transform roomRoot;
    float nextSpawn;
    readonly List<GameObject> live = new();
    bool won = false;

    // Single Configure with optional params so 8-arg calls still work.
    public void Configure(
        float spawnEvery, float life, float gazeSecs, float maxAngle,
        Vector2 scaleRange, Vector2 inset, bool single, Material glyph,
        float thickness = -1f, float embed = -1f)
    {
        spawnInterval   = Mathf.Max(0.2f, spawnEvery);
        glyphLifetime   = Mathf.Max(0.2f, life);
        gazeSeconds     = Mathf.Max(0.1f, gazeSecs);
        maxGazeAngle    = Mathf.Clamp(maxAngle, 2f, 60f);
        glyphScaleRange = new Vector2(Mathf.Max(0.05f, scaleRange.x), Mathf.Max(0.06f, scaleRange.y));
        wallInsetXY     = new Vector2(Mathf.Max(0f, inset.x), Mathf.Max(0f, inset.y));
        oneGlyphAtATime = single;
        glyphMat        = glyph;

        // Use current inspector defaults if caller didn't supply the new args
        if (thickness >= 0f) glyphThickness = Mathf.Max(0.01f, thickness);
        if (embed     >= 0f) embedEpsilon   = Mathf.Max(0f, embed);
    }


    public void Begin(Transform parentUnderRoot, Transform[] wallsNSEW)
    {
        walls = wallsNSEW;
        roomRoot = parentUnderRoot;

        if (glyphRoot) { SafeDestroy(glyphRoot.gameObject); glyphRoot = null; }
        glyphRoot = new GameObject("GlyphWall").transform;
        glyphRoot.SetParent(parentUnderRoot ? parentUnderRoot : transform, false);
        glyphRoot.localPosition = Vector3.zero;
        glyphRoot.localRotation = Quaternion.identity;
        glyphRoot.localScale    = Vector3.one;

        won = false;
        live.Clear();
        nextSpawn = Time.unscaledTime + spawnInterval * 0.5f;
        enabled = true;
    }

    public void StopAndClear()
    {
        enabled = false;
        for (int i = live.Count - 1; i >= 0; i--) SafeDestroy(live[i]);
        live.Clear();
        if (glyphRoot) SafeDestroy(glyphRoot.gameObject);
        glyphRoot = null;
    }

    void Update()
    {
        for (int i = live.Count - 1; i >= 0; i--) if (!live[i]) live.RemoveAt(i);

        if (won) { enabled = false; return; }
        if (Time.unscaledTime < nextSpawn) return;
        if (oneGlyphAtATime && live.Count > 0) { nextSpawn = Time.unscaledTime + 0.25f; return; }

        var w = ChooseWall();
        if (!w) { nextSpawn = Time.unscaledTime + spawnInterval; return; }

        var g = SpawnGlyphOnWall(w);
        if (g) live.Add(g);

        nextSpawn = Time.unscaledTime + spawnInterval;
    }

    Transform ChooseWall()
    {
        if (walls == null || walls.Length == 0) return null;
        for (int tries = 0; tries < 8; tries++)
        {
            var w = walls[Random.Range(0, walls.Length)];
            if (w) return w;
        }
        return walls[0];
    }

    GameObject SpawnGlyphOnWall(Transform wall)
    {
        // Wall dimensions from Quad scale (x=width, y=height)
        float width  = wall.localScale.x;
        float height = wall.localScale.y;

        float halfW = width  * 0.5f - Mathf.Clamp(wallInsetXY.x, 0f, width  * 0.45f);
        float halfH = height * 0.5f - Mathf.Clamp(wallInsetXY.y, 0f, height * 0.45f);

        float rx = Random.Range(-halfW, halfW);
        float ry = Random.Range(-halfH, halfH);

        // Wall axes
        Vector3 center = wall.position;
        Vector3 right  = wall.right;
        Vector3 up     = wall.up;
        Vector3 fwd    = wall.forward;

        // Inward into the room
        Vector3 toRoom = (roomRoot ? (roomRoot.position - wall.position) : -fwd);
        Vector3 inward = fwd * Mathf.Sign(Vector3.Dot(fwd, toRoom));

        float halfT = Mathf.Max(0.005f, glyphThickness * 0.5f);

        // FIX: move inward, not outward
        Vector3 posW = center + right * rx + up * ry + inward * (halfT + Mathf.Max(0f, embedEpsilon));

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Glyph";
        go.transform.SetParent(glyphRoot ? glyphRoot : transform, false);
        go.transform.position = posW;
        go.transform.rotation = Quaternion.LookRotation(inward, up);

        float s = Random.Range(glyphScaleRange.x, glyphScaleRange.y);
        go.transform.localScale = new Vector3(s, s, Mathf.Max(0.01f, glyphThickness));

        var r = go.GetComponent<Renderer>();
        var m = glyphMat ? new Material(glyphMat) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        MakeOpaqueDoubleSidedEmissive(m, new Color(0.20f, 1f, 0.20f), 3.0f);
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        // remove collider
        var col = go.GetComponent<Collider>();
#if UNITY_EDITOR
        if (col) DestroyImmediate(col);
#else
        if (col) Destroy(col);
#endif

        var pulse = go.AddComponent<PulseEmission>();
        pulse.rendererRef = r;
        pulse.baseColor   = new Color(0.20f, 1f, 0.20f);
        pulse.intensity   = 3.0f;
        pulse.speed       = Random.Range(1.6f, 2.6f);

        var gz = go.AddComponent<GazeDestroyGlyph>();
        gz.requiredLookTime = gazeSeconds;
        gz.maxAngle         = maxGazeAngle;
        gz.spawner          = this;
        gz.glyphRenderer    = r;

        var life = go.AddComponent<AutoDestroyUnscaled>();
        life.life = glyphLifetime;

        return go;
    }

    // Called by GazeDestroyGlyph when the player finishes looking
    public void NotifyGlyphWon()
    {
        if (won) return;
        won = true;
        StopAndClear();
        if (roomController) roomController.OnSecretDestroyed();
    }

    // --- helpers ---

    static void MakeOpaqueDoubleSidedEmissive(Material m, Color baseCol, float emissiveMul)
    {
        if (!m) return;
        if (m.HasProperty("_Surface"))  m.SetFloat("_Surface", 0f);
        if (m.HasProperty("_ZWrite"))   m.SetFloat("_ZWrite", 1f);
        if (m.HasProperty("_Blend"))    m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        if (m.HasProperty("_Cull"))     m.SetFloat("_Cull", 0f); // double-sided
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", baseCol);
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", baseCol * emissiveMul);
        m.renderQueue = Mathf.Max(2000, m.renderQueue);
    }

    static void SafeDestroy(GameObject go)
    {
        if (!go) return;
#if UNITY_EDITOR
        DestroyImmediate(go);
#else
        Destroy(go);
#endif
    }
}



// ─────────────────────────────────────────────────────────────────────────────
// Simple emission pulser
// ─────────────────────────────────────────────────────────────────────────────
[DisallowMultipleComponent]
public class PulseEmission : MonoBehaviour
{
    public Renderer rendererRef;
    [ColorUsage(true, true)] public Color baseColor = Color.white;
    public float intensity = 2f;
    public float speed = 2f;
    MaterialPropertyBlock mpb;
    void Awake() { if (!rendererRef) rendererRef = GetComponent<Renderer>(); mpb = new MaterialPropertyBlock(); }
    void Update()
    {
        if (!rendererRef) return;
        float k = (Mathf.Sin(Time.time * speed) * 0.5f + 0.5f) * intensity;
        rendererRef.GetPropertyBlock(mpb);
        mpb.SetColor("_EmissionColor", baseColor * k);
        rendererRef.SetPropertyBlock(mpb);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Auto destroy using unscaled time (Quest-safe)
// ─────────────────────────────────────────────────────────────────────────────
[DisallowMultipleComponent]
public class AutoDestroyUnscaled : MonoBehaviour
{
    public float life = 5f;
    float t0;
    void OnEnable() { t0 = Time.unscaledTime; }
    void Update()
    {
        if (Time.unscaledTime - t0 >= life)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(gameObject);
#else
            Object.Destroy(gameObject);
#endif
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Neon helpers
// ─────────────────────────────────────────────────────────────────────────────
[DisallowMultipleComponent]
public class NeonOutlineFader : MonoBehaviour
{
    public Renderer outlineRenderer;
    public float targetIntensity = 2.2f;
    public float fadeTime = 0.12f;
    public TrailRenderer trail; // optional

    Material mat;
    Color baseEmission;
    float t0;

    void Awake()
    {
        if (!outlineRenderer) { enabled = false; return; }
        mat = outlineRenderer.sharedMaterial;
        if (!mat || !mat.HasProperty("_EmissionColor")) { enabled = false; return; }
        baseEmission = mat.GetColor("_EmissionColor");
        mat.SetColor("_EmissionColor", baseEmission * 0f);
        t0 = Time.time;
        if (trail) trail.time = 0f;
    }

    void Update()
    {
        float t = Mathf.Clamp01((Time.time - t0) / Mathf.Max(0.001f, fadeTime));
        float k = Mathf.SmoothStep(0f, 1f, t);
        mat.SetColor("_EmissionColor", baseEmission * k);
        if (trail) trail.time = 0.55f * k;
        if (t >= 1f) enabled = false;
    }
}

[DisallowMultipleComponent]
public class CoreFadeToBlack : MonoBehaviour
{
    public Renderer rend;
    public float fadeTime = 0.25f;
    Material mat;
    Color startColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    float t0;

    void Awake()
    {
        if (!rend) rend = GetComponent<Renderer>();
        if (!rend) { enabled = false; return; }
        mat = rend.material;
        if (!mat || !mat.HasProperty("_BaseColor")) { enabled = false; return; }
        mat.SetColor("_BaseColor", startColor);
        t0 = Time.time;
    }

    void Update()
    {
        float k = Mathf.Clamp01((Time.time - t0) / Mathf.Max(0.001f, fadeTime));
        mat.SetColor("_BaseColor", Color.Lerp(startColor, Color.black, k));
        if (k >= 1f) enabled = false;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Small helpers
// ─────────────────────────────────────────────────────────────────────────────
[DisallowMultipleComponent]
public class SlowRotate : MonoBehaviour
{
    public Vector3 degPerSec = new Vector3(0, 30, 0);
    void Update() => transform.Rotate(degPerSec * Time.deltaTime, Space.Self);
}

[DisallowMultipleComponent]
public class FloatBob : MonoBehaviour
{
    public float amplitude = 0.08f;
    public float freq = 1.1f;
    Vector3 basePos;
    void Awake() => basePos = transform.localPosition;
    void Update()
    {
        float y = Mathf.Sin(Time.time * freq) * amplitude;
        transform.localPosition = new Vector3(basePos.x, basePos.y + y, basePos.z);
    }
}

// UV scroller (for data-rain)
[DisallowMultipleComponent]
public class UVScroller : MonoBehaviour
{
    public float speedY = -1.0f;
    public float speedX = 0.0f;
    public float varianceX = 0.0f;
    public float swayHz = 0.07f;

    Renderer _r;
    Material _m;
    Vector2 _uv;

    void Awake()
    {
        _r = GetComponent<Renderer>();
        _m = _r ? _r.material : null; // instance
        _uv = Vector2.zero;
    }

    void Update()
    {
        if (_m == null) return;
        float t = Time.time;
        float sway = varianceX * Mathf.Sin(t * Mathf.PI * 2f * swayHz);
        _uv.x += (speedX * Time.deltaTime);
        _uv.y += (speedY * Time.deltaTime);
        if (_m.HasProperty("_BaseMap"))
            _m.SetTextureOffset("_BaseMap", new Vector2(_uv.x + sway, _uv.y));
        else
            _m.mainTextureOffset = new Vector2(_uv.x + sway, _uv.y);
    }
}
