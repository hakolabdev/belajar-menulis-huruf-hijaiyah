using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HijaiyahWriter : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] private LevelList levelList;

    [Header("Panel")]
    [SerializeField] private GameObject finishPanel;

    [Header("Button")]
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Painter / Mask")]
    public BrushPainter painter;
    public SpriteRenderer guideSprite;

    [Header("Level Settings")]
    [SerializeField] private float minMove = 0.01f;
    [Range(0.01f, 0.5f)] public float progressSnap = 0.12f;

    // ===== Stroke Step UI =====
    [Header("Stroke Hints UI")]
    [Tooltip("Teks langkah, mis. `Langkah 1/3`")]
    [SerializeField] private TextMeshProUGUI stepText;
    [SerializeField] private TextMeshProUGUI namaHurufText;
    [Tooltip("LineRenderer untuk garis hint jalur stroke")]
    [SerializeField] private LineRenderer pathHint;

    public enum StrokeSpace { World, GuideLocalUnits, GuideSpritePixels }
    [Header("Stroke Coord Space")]
    [SerializeField] private StrokeSpace strokeSpace = StrokeSpace.GuideLocalUnits;

    [Tooltip("Marker bulat start (LineRenderer)")]
    [SerializeField] private LineRenderer startMarkerRing;
    [Tooltip("Marker bulat end (LineRenderer)")]
    [SerializeField] private LineRenderer endMarkerRing;

    [SerializeField] private int ringSegments = 32;
    [Tooltip("Fade hint saat lagi menggambar")]
    [SerializeField] private float hintAlphaWhileDrawing = 0.25f;

    // ===== Runtime state =====
    private HijaiyahLevelData currentLevelData;
    private HijaiyahLevelData[] hijaiyahLevels;

    private int currentStrokeIdx = 0;
    private bool drawing;
    private int furthestSegment;
    private Camera cam;
    private Vector3 lastValidWorld;
    private readonly List<Vector3> drawn = new();
    private bool completionLatched = false;

    void Awake()
    {
        cam = Camera.main;
    }

    void Start()
    {
        finishPanel.SetActive(false);

        if (levelList == null || levelList.levels == null || levelList.levels.Length == 0)
        {
            Debug.LogError("[HijaiyahWriter] LevelList belum di-assign atau kosong!");
            return;
        }

        hijaiyahLevels = levelList.levels;

        int selectedLevel = PlayerPrefs.GetInt("Level", 0);
        selectedLevel = Mathf.Clamp(selectedLevel, 0, hijaiyahLevels.Length - 1);

        currentLevelData = hijaiyahLevels[selectedLevel];
        ApplyLevelData();

        if (nextLevelButton) nextLevelButton.onClick.AddListener(NextLevel);
        if (mainMenuButton) mainMenuButton.onClick.AddListener(MainMenu);
    }

    private void MainMenu()
    {
        SFXManager.Instance.PlaySFX(SFXManager.Instance.audioButtonPositive);
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void NextLevel()
    {
        SFXManager.Instance.PlaySFX(SFXManager.Instance.audioButtonPositive);
        int currentLevel = PlayerPrefs.GetInt("Level", 0);
        int nextLevel = currentLevel + 1;
        int maxLevel = hijaiyahLevels.Length - 1;

        if (nextLevel > maxLevel)
        {
            SceneManager.LoadScene("MainMenu");
        }
        else
        {
            PlayerPrefs.SetInt("Level", nextLevel);
            PlayerPrefs.Save();
            SceneManager.LoadScene("Game");
        }
    }

    void ApplyLevelData()
    {
        if (currentLevelData == null)
        {
            Debug.LogWarning("[HijaiyahWriter] currentLevelData NULL saat ApplyLevelData");
            return;
        }

        ResetRuntime();

        SFXManager.Instance.PlaySFX(currentLevelData.sound);
        
        namaHurufText.text = currentLevelData.letterName;

        if (guideSprite && currentLevelData.guideSprite)
        {
            guideSprite.sprite = currentLevelData.guideSprite;
            var mask = guideSprite.GetComponent<SpriteMask>();
            if (mask) mask.sprite = currentLevelData.guideSprite;
        }

        if (painter)
        {
            if (!painter.guideSprite && guideSprite)
                painter.guideSprite = guideSprite;
            painter.CancelStroke();
        }

        if (currentLevelData.sound)
            AudioSource.PlayClipAtPoint(currentLevelData.sound, Vector3.zero);

        UpdateStrokeHintsUI();
    }

    void ResetRuntime()
    {
        currentStrokeIdx = 0;
        drawing = false;
        furthestSegment = 0;
        completionLatched = false;
        drawn.Clear();
    }

    void Update()
    {
        if (currentLevelData == null || currentLevelData.strokes == null || currentStrokeIdx >= currentLevelData.strokes.Count)
            return;

        var stroke = currentLevelData.strokes[currentStrokeIdx];

#if UNITY_EDITOR
        bool pressed = Input.GetMouseButton(0);
        Vector3 pos = Input.mousePosition;
#else
        bool pressed = Input.touchCount > 0 &&
                       (Input.GetTouch(0).phase != TouchPhase.Ended &&
                        Input.GetTouch(0).phase != TouchPhase.Canceled);
        Vector3 pos = Input.touchCount > 0 ? (Vector3)Input.GetTouch(0).position : Vector3.zero;
#endif
        if (cam == null) cam = Camera.main;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(pos.x, pos.y, Mathf.Abs(cam.transform.position.z)));
        world.z = 0f;

        if (pressed)
        {
            if (!drawing)
            {
                if (stroke.points.Count >= 2 &&
                    Vector2.Distance(world, stroke.points[0]) <= stroke.startRadius)
                {
                    BeginDraw(world);
                }
            }
            else
            {
                if (drawn.Count == 0 || Vector3.Distance(drawn[^1], world) >= minMove)
                {
                    bool inside = IsInsideCorridor(stroke, world, out int segAdvanced);

                    if (inside)
                    {
                        lastValidWorld = world;
                        if (painter) painter.Paint(world);
                        if (furthestSegment < segAdvanced) furthestSegment = segAdvanced;

                        bool tailReached = furthestSegment >= stroke.points.Count - 2;
                        if (tailReached) completionLatched = true;

                        drawn.Add(world);
                    }
                }
            }
        }
        else if (drawing)
        {
            EndDraw(stroke);
        }

        if (!drawing) ApplyHintVisualAlpha(1f);
        UpdateHintTransformsOnly();
    }

    void BeginDraw(Vector3 start)
    {
        drawing = true;
        furthestSegment = 0;
        completionLatched = false;
        drawn.Clear();
        lastValidWorld = start;

        if (painter)
        {
            if (!painter.guideSprite && guideSprite) painter.guideSprite = guideSprite;
            painter.BeginStroke();
            painter.Paint(start);
        }

        ApplyHintVisualAlpha(hintAlphaWhileDrawing);
    }

    void EndDraw(Stroke stroke)
    {
        drawing = false;
        bool endNear = Vector2.Distance(lastValidWorld, stroke.points[^1]) <= stroke.endRadius;
        bool reachedTail = furthestSegment >= stroke.points.Count - 2;
        bool completed = endNear && reachedTail;

        if (completed)
        {
            if (painter) painter.EndStroke();
            currentStrokeIdx++;

            if (currentStrokeIdx >= currentLevelData.strokes.Count)
            {
                SFXManager.Instance.PlaySFX(SFXManager.Instance.audioVictory);
                finishPanel.SetActive(true);
                ToggleHints(false);
            }
            else
            {
                UpdateStrokeHintsUI();
                ApplyHintVisualAlpha(1f);
            }
        }
        else
        {
            if (painter) painter.CancelStroke();
            ApplyHintVisualAlpha(1f);
        }
    }

    bool IsInsideCorridor(Stroke stroke, Vector2 p, out int segAdvanced)
    {
        segAdvanced = furthestSegment;
        float minDist = float.MaxValue;

        for (int i = 0; i < stroke.points.Count - 1; i++)
        {
            Vector2 a = stroke.points[i];
            Vector2 b = stroke.points[i + 1];
            float d = DistancePointToSegment(p, a, b, out float t);

            if (i == furthestSegment && d <= stroke.tolerance && t >= progressSnap)
                segAdvanced = i + 1;

            if (d < minDist) minDist = d;
        }
        return minDist <= stroke.tolerance;
    }

    float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b, out float t)
    {
        Vector2 ab = b - a;
        float ab2 = Vector2.Dot(ab, ab);
        if (ab2 <= Mathf.Epsilon) { t = 0f; return Vector2.Distance(p, a); }
        t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab2);
        Vector2 proj = a + t * ab;
        return Vector2.Distance(p, proj);
    }

    // ================== HINTS (Step UI) ==================

    void UpdateStrokeHintsUI()
    {
        if (currentLevelData == null || currentLevelData.strokes == null) { ToggleHints(false); return; }
        if (currentStrokeIdx < 0 || currentStrokeIdx >= currentLevelData.strokes.Count) { ToggleHints(false); return; }

        ToggleHints(true);

        var stroke = currentLevelData.strokes[currentStrokeIdx];

        if (stepText)
            stepText.text = $"Langkah {currentStrokeIdx + 1}/{currentLevelData.strokes.Count}";

        // Path hint (rounded)
        if (pathHint)
        {
            pathHint.useWorldSpace = true;
            pathHint.numCornerVertices = 8;
            pathHint.numCapVertices = 8;
            pathHint.positionCount = stroke.points.Count;
            for (int i = 0; i < stroke.points.Count; i++)
                pathHint.SetPosition(i, StrokePointToWorld(stroke.points[i]));
            pathHint.enabled = true;
        }

        // Marker ring di start/end (radius pakai corridor radius di world)
        Vector3 wStart = StrokePointToWorld(stroke.points[0]);
        Vector3 wEnd = StrokePointToWorld(stroke.points[^1]);
        float rStart = CorridorRadiusWorld(stroke.startRadius);
        float rEnd = CorridorRadiusWorld(stroke.endRadius);

        DrawRing(startMarkerRing, wStart, rStart);
        DrawRing(endMarkerRing, wEnd, rEnd);
    }

    void UpdateHintTransformsOnly()
    {
        if (currentLevelData == null || currentLevelData.strokes == null) return;
        if (currentStrokeIdx < 0 || currentStrokeIdx >= currentLevelData.strokes.Count) return;

        var stroke = currentLevelData.strokes[currentStrokeIdx];

        if (pathHint && pathHint.enabled && pathHint.positionCount == stroke.points.Count)
        {
            for (int i = 0; i < stroke.points.Count; i++)
                pathHint.SetPosition(i, StrokePointToWorld(stroke.points[i]));
        }

        Vector3 wStart = StrokePointToWorld(stroke.points[0]);
        Vector3 wEnd = StrokePointToWorld(stroke.points[^1]);
        float rStart = CorridorRadiusWorld(stroke.startRadius);
        float rEnd = CorridorRadiusWorld(stroke.endRadius);
        DrawRing(startMarkerRing, wStart, rStart);
        DrawRing(endMarkerRing, wEnd, rEnd);
    }

    Vector3 StrokePointToWorld(Vector2 p)
    {
        if (strokeSpace == StrokeSpace.World)
            return new Vector3(p.x, p.y, 0f);

        if (!guideSprite)
            return new Vector3(p.x, p.y, 0f);

        if (strokeSpace == StrokeSpace.GuideLocalUnits)
        {
            return guideSprite.transform.TransformPoint(new Vector3(p.x, p.y, 0f));
        }

        // GuideSpritePixels
        var sp = guideSprite.sprite;
        if (!sp) return new Vector3(p.x, p.y, 0f);

        Vector2 pivotPx = sp.pivot;               // pixel
        float ppu = sp.pixelsPerUnit;             // pixel per unit
        Vector2 localUnits = (p - pivotPx) / ppu; // pixel -> local (unit)
        return guideSprite.transform.TransformPoint(new Vector3(localUnits.x, localUnits.y, 0f));
    }

    void DrawRing(LineRenderer lr, Vector3 center, float radius)
    {
        if (!lr) return;
        lr.useWorldSpace = true;
        lr.numCornerVertices = 8;
        lr.numCapVertices = 8;
        int seg = Mathf.Max(8, ringSegments);
        lr.positionCount = seg + 1;
        for (int i = 0; i <= seg; i++)
        {
            float t = (i / (float)seg) * Mathf.PI * 2f;
            float x = Mathf.Cos(t) * radius;
            float y = Mathf.Sin(t) * radius;
            lr.SetPosition(i, center + new Vector3(x, y, 0f));
        }
        lr.enabled = true;
    }

    float CorridorRadiusWorld(float r)
    {
        switch (strokeSpace)
        {
            case StrokeSpace.World:
                return r;
            case StrokeSpace.GuideLocalUnits:
                {
                    if (!guideSprite) return r;
                    var s = guideSprite.transform.lossyScale;
                    return r * ((Mathf.Abs(s.x) + Mathf.Abs(s.y)) * 0.5f);
                }
            case StrokeSpace.GuideSpritePixels:
                {
                    if (!guideSprite || !guideSprite.sprite) return r;
                    float ppu = guideSprite.sprite.pixelsPerUnit;
                    var s = guideSprite.transform.lossyScale;
                    float localUnits = r / ppu;
                    return localUnits * ((Mathf.Abs(s.x) + Mathf.Abs(s.y)) * 0.5f);
                }
            default:
                return r;
        }
    }

    void ApplyHintVisualAlpha(float a)
    {
        if (pathHint)
        {
#if UNITY_2022_1_OR_NEWER
            var c = pathHint.startColor; 
            c.a = a; 
            pathHint.startColor = c;
            c = pathHint.endColor; 
            c.a = a; 
            pathHint.endColor = c;
#else
            var grad = pathHint.colorGradient;
            var keys = grad.alphaKeys;
            for (int i = 0; i < keys.Length; i++) { keys[i].alpha = a; }
            grad.alphaKeys = keys;
            pathHint.colorGradient = grad;
#endif
        }
        SetLRAlpha(startMarkerRing, a);
        SetLRAlpha(endMarkerRing, a);
    }

    void SetLRAlpha(LineRenderer lr, float a)
    {
        if (!lr) return;
#if UNITY_2022_1_OR_NEWER
        var c = lr.startColor; c.a = a; lr.startColor = c;
        c = lr.endColor; c.a = a; lr.endColor = c;
#else
        var grad = lr.colorGradient;
        var keys = grad.alphaKeys;
        for (int i = 0; i < keys.Length; i++) { keys[i].alpha = a; }
        grad.alphaKeys = keys;
        lr.colorGradient = grad;
#endif
    }

    void ToggleHints(bool on)
    {
        if (stepText) stepText.enabled = on;
        if (pathHint) pathHint.enabled = on;
        if (startMarkerRing) startMarkerRing.gameObject.SetActive(on);
        if (endMarkerRing) endMarkerRing.gameObject.SetActive(on);
    }
}
