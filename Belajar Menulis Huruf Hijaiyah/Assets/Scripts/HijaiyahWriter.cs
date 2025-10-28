using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class HijaiyahWriter : MonoBehaviour
{
    [Header("Meta")]
    public string letterName = "Alif";

    [Header("Visual")]
    public Sprite sprite;

    [Header("Strokes (urutan penting)")]
    public List<Stroke> strokes = new List<Stroke>();

    [Header("Draw Settings")]
    [Tooltip("Filter gerakan kecil biar gak jitter.")]
    public float minMove = 0.01f;
    [Tooltip("Seberapa jauh maju di segmen (0..1) biar dianggap progres.")]
    [Range(0.01f, 0.5f)] public float progressSnap = 0.12f;

    [Header("Painter / Mask")]
    [Tooltip("Komponen BrushPainter (pakai SpriteMask). Wajib diisi.")]
    public BrushPainter painter;                 // drag GameObject yang ada BrushPainter
    [Tooltip("Sprite huruf (guide). Dipakai buat sorting & referensi visual saja.")]
    public SpriteRenderer guideSprite;           // drag SpriteRenderer huruf PNG

    [Header("Events")]
    public UnityEvent onStrokeCompleted;
    public UnityEvent onLetterCompleted;
    public UnityEvent onOutsideCorridor; // dipanggil saat keluar jalur (opsional: SFX/Vibrate)

    // ===== Runtime state =====
    int currentStrokeIdx = 0;
    bool drawing;
    int furthestSegment;                 // segmen polyline paling jauh yang sudah valid
    Camera cam;
    Vector3 lastValidWorld;              // titik valid terakhir (buat cek end)
    bool lastInside;                     // status sample sebelumnya
    readonly List<Vector3> drawn = new(); // hanya buat tracking internal
    bool completionLatched = false;   // << NEW


    // ===== Gizmos =====
    [Header("Gizmos")]
    public Color gizmoStroke = new Color(0, 0, 0, 0.85f);
    public Color gizmoStart = new Color(0, 1, 0, 0.95f);
    public Color gizmoEnd = new Color(0, 0.5f, 1, 0.95f);
    public float gizmoDotSize = 0.12f;
    public bool showStartEndRadius = true;

    void Awake()
    {
        cam = Camera.main;
        if (strokes == null) strokes = new List<Stroke>();

        // Default contoh kalau kosong: Alif lurus vertikal
        if (strokes.Count == 0)
        {
            var s = new Stroke
            {
                name = "alif",
                points = new List<Vector2> { new Vector2(0, 2f), new Vector2(0, -2f) },
                tolerance = 0.35f,
                startRadius = 0.4f,
                endRadius = 0.4f
            };
            strokes.Add(s);
        }
    }

    void Update()
    {
        if (strokes.Count == 0 || currentStrokeIdx >= strokes.Count) return;

        // input mouse/touch → world (z = 0)
#if UNITY_EDITOR
        bool pressed = Input.GetMouseButton(0);
        Vector3 pos = Input.mousePosition;
#else
        bool pressed = Input.touchCount > 0 &&
                       (Input.GetTouch(0).phase != TouchPhase.Ended &&
                        Input.GetTouch(0).phase != TouchPhase.Canceled);
        Vector3 pos = Input.touchCount > 0 ? (Vector3)Input.GetTouch(0).position : Vector3.zero;
#endif
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(pos.x, pos.y, Mathf.Abs(cam.transform.position.z)));
        world.z = 0f;

        var stroke = strokes[currentStrokeIdx];

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
                // kalau belum di start area, diemin aja
            }
            else
            {
                // gerak cukup jauh?
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
                            bool tailPassed = furthestSegment < segAdvanced;
                            bool endNearNow = Vector2.Distance(world, stroke.points[^1]) <= stroke.endRadius;
                            if (tailPassed || endNearNow)
                                completionLatched = true;
                        }
                    }
                    else
                    {
                        // keluar koridor → gak dicat
                        if (lastInside) onOutsideCorridor?.Invoke();
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
        completionLatched = false;    // << NEW
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
            onStrokeCompleted?.Invoke();
            currentStrokeIdx++;
            if (currentStrokeIdx >= strokes.Count) 
            {
                onLetterCompleted?.Invoke();
                Debug.Log("Finish");
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

    // ====== Gizmos ======
    void OnDrawGizmos()
    {
        if (strokes == null) return;

        foreach (var s in strokes)
        {
            if (s.points == null || s.points.Count == 0) continue;

            // garis jalur
            Gizmos.color = gizmoStroke;
            for (int i = 0; i < s.points.Count - 1; i++)
            {
                var a = new Vector3(s.points[i].x, s.points[i].y, 0);
                var b = new Vector3(s.points[i + 1].x, s.points[i + 1].y, 0);
                Gizmos.DrawLine(a, b);
            }

            // start / end marker
            Gizmos.color = gizmoStart;
            Gizmos.DrawSphere(new Vector3(s.points[0].x, s.points[0].y, 0), gizmoDotSize);
            Gizmos.color = gizmoEnd;
            Gizmos.DrawSphere(new Vector3(s.points[^1].x, s.points[^1].y, 0), gizmoDotSize);

            // radius (optional)
            if (showStartEndRadius)
            {
                DrawCircle(new Vector3(s.points[0].x, s.points[0].y, 0), s.startRadius, gizmoStart);
                DrawCircle(new Vector3(s.points[^1].x, s.points[^1].y, 0), s.endRadius, gizmoEnd);
            }
        }
    }

    void DrawCircle(Vector3 center, float radius, Color c, int segments = 64)
    {
        Gizmos.color = new Color(c.r, c.g, c.b, 0.6f);
        Vector3 prev = center + new Vector3(radius, 0, 0);
        float step = Mathf.PI * 2f / segments;
        for (int i = 1; i <= segments; i++)
        {
            float ang = i * step;
            Vector3 cur = center + new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
}
