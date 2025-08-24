using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if UNITY_XR_INTERACTION_TOOLKIT
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif
#if TMP_PRESENT || TEXTMESHPRO_PRESENT
using TMPro;
#endif

[DisallowMultipleComponent]
public class StartMenuOverlay : MonoBehaviour
{
    System.Action _onStart;

    Canvas _canvas;
    GameObject _root;
    Button _bigButton;

    public void Show(System.Action onStart)
    {
        _onStart = onStart;

        EnsureEventSystem();

        _root = new GameObject("StartMenuOverlay");
        _canvas = _root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;

        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        _root.AddComponent<GraphicRaycaster>();
        #if UNITY_XR_INTERACTION_TOOLKIT
        _root.AddComponent<TrackedDeviceGraphicRaycaster>();
        #endif

        // Full‑screen, invisible button to receive pointer clicks
        var buttonGO = new GameObject("ClickCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(_root.transform, false);
        var r = buttonGO.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        var img = buttonGO.GetComponent<Image>();
        img.color = new Color(0,0,0,0.35f); // a subtle dark veil so text pops; set alpha 0 if you want fully transparent

        _bigButton = buttonGO.GetComponent<Button>();
        _bigButton.onClick.AddListener(StartClicked);

        // Center text
        var textGO = new GameObject("MenuText");
        textGO.transform.SetParent(buttonGO.transform, false);
        var tr = textGO.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.5f, 0.5f);
        tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.sizeDelta = new Vector2(1400, 700);

        // Prefer TextMeshPro if available, else Unity UI Text
        #if TMP_PRESENT || TEXTMESHPRO_PRESENT
        var txt = textGO.AddComponent<TextMeshProUGUI>();
        txt.raycastTarget = false;
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontSize = 56;
        txt.enableWordWrapping = false;
        txt.richText = true;
        txt.color = new Color(0.9f, 0.95f, 1f, 1f);
        txt.text =
            "<mspace=0.8em><b>◆◆◆</b></mspace>\n" +
            "<size=84%><b>❖  T R O N   A I M  R O O M  ❖</b></size>\n" +
            "<mspace=0.8em><b>◆◆◆</b></mspace>\n\n" +
            "<size=72%>Point at this screen and <b>Click / Trigger</b> to begin</size>\n" +
            "<size=60%>Press any key or tap also works</size>\n\n" +
            "<size=60%><alpha=#AA>Controls: Look around • Move/Teleport (XR) • Shoot = your setup</alpha></size>\n" +
            "<size=60%><alpha=#66>Tip: Keep your crosshair smooth—accuracy > speed</alpha></size>";
        // A subtle glow by duplicating shadow text:
        var outline = textGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.8f, 1f, 0.55f);
        outline.effectDistance = new Vector2(2f, -2f);
        #else
        var txt = textGO.AddComponent<Text>();
        txt.raycastTarget = false;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 24;
        txt.resizeTextMaxSize = 64;
        txt.color = new Color(0.9f, 0.95f, 1f, 1f);
        txt.text =
            "◆◆◆\n" +
            "❖  T R O N   A I M  R O O M  ❖\n" +
            "◆◆◆\n\n" +
            "Point at this screen and Click / Trigger to begin\n" +
            "Press any key or tap also works\n\n" +
            "Controls: Look around • Move/Teleport (XR) • Shoot = your setup\n" +
            "Tip: Keep your crosshair smooth—accuracy > speed";
        #endif
    }

    void StartClicked()
    {
        Hide();
        _onStart?.Invoke();
    }

    void Hide()
    {
        if (_root)
        {
            #if UNITY_EDITOR
            DestroyImmediate(_root);
            #else
            Destroy(_root);
            #endif
        }
    }

    void Update()
    {
        // Optional: allow keyboard/touch to start
        if (Input.anyKeyDown)
        {
            StartClicked();
        }
        // Optional: simple mouse/touch anywhere
        if (Input.GetMouseButtonDown(0))
        {
            StartClicked();
        }
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem").AddComponent<EventSystem>();
            es.gameObject.AddComponent<StandaloneInputModule>();
            #if UNITY_XR_INTERACTION_TOOLKIT
            if (es.gameObject.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            #endif
        }
    }
}
