using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HijaiyahWriter : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField]
    LevelList levelList;

    [Header("Panel")]
    [SerializeField]
    GameObject finishPanel;
    [SerializeField]
    GameObject pausePanel;

    [Header("Button")]
    [SerializeField]
    Button nextLevelButton;
    [SerializeField]
    Button mainMenuButton;

    [Header("Painter / Mask")]
    public BrushPainter painter;
    public SpriteRenderer guideSprite;

    [Header("Level Data For Create/Edit (ScriptableObject)")]
    public HijaiyahLevelData levelData;
    public float minMove = 0.01f;
    [Range(0.01f, 0.5f)] public float progressSnap = 0.12f;

    // ===== Runtime state =====
    int currentStrokeIdx = 0;
    bool drawing;
    int furthestSegment;
    Camera cam;
    Vector3 lastValidWorld;
    bool lastInside;
    readonly List<Vector3> drawn = new();
    bool completionLatched = false;

    void Awake()
    {
        cam = Camera.main;
        finishPanel.SetActive(false);
    }

    void OnEnable()
    {
        if (levelData != null) ApplyLevelData();
    }

    void ApplyLevelData()
    {
        if (levelData == null)
        {
            Debug.LogWarning("[HijaiyahWriter] levelData NULL saat ApplyLevelData");
            return;
        }

        ResetRuntime();

        // apply guide sprite
        if (guideSprite && levelData.guideSprite)
        {
            guideSprite.sprite = levelData.guideSprite;
            guideSprite.GetComponent<SpriteMask>().sprite = levelData.guideSprite;
        }

        // sinkron ke painter & bersihkan tinta lama
        if (painter)
        {
            if (!painter.guideSprite && guideSprite)
                painter.guideSprite = guideSprite;
            painter.CancelStroke(); // bersih sebelum mulai level baru
        }

        if (levelData.sound)
            AudioSource.PlayClipAtPoint(levelData.sound, Vector3.zero);
    }

    void ResetRuntime()
    {
        currentStrokeIdx = 0;
        drawing = false;
        furthestSegment = 0;
        completionLatched = false;
        drawn.Clear();
        lastInside = false;
    }

    void Update()
    {
        if (levelData == null || levelData.strokes == null || currentStrokeIdx >= levelData.strokes.Count)
            return;

        var stroke = levelData.strokes[currentStrokeIdx];

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
                // wajib mulai dari start radius
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
                        drawn.Add(world);

                        // kalau segmen terakhir sudah terlewati ATAU udah masuk endRadius → kunci selesai
                        if (!completionLatched)
                        {
                            bool tailPassed = furthestSegment < segAdvanced; // naik segmen
                            bool endNearNow = Vector2.Distance(world, stroke.points[^1]) <= stroke.endRadius;
                            if (tailPassed || endNearNow)
                                completionLatched = true;
                        }
                    }
                    else
                    {
                        // keluar koridor → gak dicat
                        if (lastInside)
                        {
                            //play suara
                        }
                    }

                    lastInside = inside;
                }
            }
        }
        else
        {
            if (drawing)
                EndDraw(stroke);
        }
    }

    // ====== Core flow ======
    void BeginDraw(Vector3 start)
    {
        drawing = true;
        furthestSegment = 0;
        completionLatched = false;
        drawn.Clear();
        lastValidWorld = start;
        lastInside = true;

        // mulai "tinta"
        if (painter)
        {
            if (!painter.guideSprite && guideSprite) painter.guideSprite = guideSprite;
            painter.BeginStroke();
            painter.Paint(start);
        }
    }

    void EndDraw(Stroke stroke)
    {
        drawing = false;
        bool endNear = Vector2.Distance(lastValidWorld, stroke.points[^1]) <= stroke.endRadius;
        bool reachedTail = furthestSegment >= stroke.points.Count - 2;

        bool completed = (endNear && reachedTail) || completionLatched;

        if (completed)
        {
            if (painter) painter.EndStroke();
            //play suara segaris berhasil
            currentStrokeIdx++;
            if (currentStrokeIdx >= levelData.strokes.Count)
            {
                finishPanel.SetActive(true);
                //nextLevelButton.onClick.AddListener();
            }
        }
        else
        {
            if (painter) painter.CancelStroke();
        }
    }

    // ====== Geometry / Validation ======
    bool IsInsideCorridor(Stroke stroke, Vector2 p, out int segAdvanced)
    {
        segAdvanced = furthestSegment;
        float minDist = float.MaxValue;

        for (int i = 0; i < stroke.points.Count - 1; i++)
        {
            Vector2 a = stroke.points[i];
            Vector2 b = stroke.points[i + 1];
            float d = DistancePointToSegment(p, a, b, out float t);

            // progres maju hanya di segmen aktif (berurutan)
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
}
