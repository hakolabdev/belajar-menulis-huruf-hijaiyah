#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HijaiyahWriter))]
public class HijaiyahWriterEditor : Editor
{
    HijaiyahWriter T;
    bool recording = false;
    int strokeIndex = 0;
    float gridSnap = 0.1f;

    void OnEnable()
    {
        T = (HijaiyahWriter)target;
        SceneView.duringSceneGui += DuringSceneGUI;
    }
    void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("=== Stroke Recorder ===", EditorStyles.boldLabel);

        if (T.strokes == null) T.strokes = new System.Collections.Generic.List<Stroke>();
        strokeIndex = Mathf.Clamp(EditorGUILayout.IntField("Active Stroke Index", strokeIndex), 0, Mathf.Max(0, T.strokes.Count - 1));
        gridSnap = EditorGUILayout.FloatField("Grid Snap (world)", gridSnap);

        EditorGUILayout.BeginHorizontal();
        var btnLabel = recording ? "Stop Recording" : "Start Recording";
        if (GUILayout.Button(btnLabel))
        {
            recording = !recording;

            // Auto-create stroke kalau kosong
            if (recording && T.strokes.Count == 0)
            {
                Undo.RecordObject(T, "Auto Create Stroke");
                T.strokes.Add(new Stroke() { name = "stroke-0" });
                strokeIndex = 0;
                EditorUtility.SetDirty(T);
            }
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("New Stroke"))
        {
            Undo.RecordObject(T, "New Stroke");
            T.strokes.Add(new Stroke() { name = $"stroke-{T.strokes.Count}" });
            strokeIndex = T.strokes.Count - 1;
            EditorUtility.SetDirty(T);
        }
        if (GUILayout.Button("Clear Stroke"))
        {
            if (ValidStroke())
            {
                Undo.RecordObject(T, "Clear Stroke");
                T.strokes[strokeIndex].points.Clear();
                EditorUtility.SetDirty(T);
            }
        }
        if (GUILayout.Button("Undo Point"))
        {
            if (ValidStroke() && T.strokes[strokeIndex].points.Count > 0)
            {
                Undo.RecordObject(T, "Undo Point");
                T.strokes[strokeIndex].points.RemoveAt(T.strokes[strokeIndex].points.Count - 1);
                EditorUtility.SetDirty(T);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (ValidStroke())
        {
            var s = T.strokes[strokeIndex];
            s.name = EditorGUILayout.TextField("Stroke Name", s.name);
            s.tolerance = EditorGUILayout.Slider("Tolerance", s.tolerance, 0.05f, 2f);
            s.startRadius = EditorGUILayout.Slider("Start Radius", s.startRadius, 0.05f, 2f);
            s.endRadius = EditorGUILayout.Slider("End Radius", s.endRadius, 0.05f, 2f);
            EditorGUILayout.LabelField($"Points: {s.points.Count}");
        }

        EditorGUILayout.HelpBox(
            "Cara pakai:\n" +
            "- Klik Start Recording lalu KLIK di Scene View (plane z=0) buat nambah titik.\n" +
            "- Shift+Klik = stop recording.\n" +
            "- Backspace = undo titik terakhir.\n" +
            "- Pastikan GameObject HijaiyahWriter lagi dipilih.", MessageType.Info);
    }

    void DuringSceneGUI(SceneView sv)
    {
        if (!recording || !ValidStroke()) return;
        if (Selection.activeGameObject != ((HijaiyahWriter)target).gameObject) return;

        Event e = Event.current;
        if (e == null) return;

        // ambil kontrol input
        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        // hit plane z=0
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (Mathf.Abs(ray.direction.z) < 1e-6f) return;
        float t = -ray.origin.z / ray.direction.z;
        Vector3 hit = ray.origin + ray.direction * t;
        Vector2 p = new Vector2(hit.x, hit.y);

        // snap
        if (gridSnap > 0f)
        {
            p.x = Mathf.Round(p.x / gridSnap) * gridSnap;
            p.y = Mathf.Round(p.y / gridSnap) * gridSnap;
        }

        // preview cursor
        Handles.color = Color.yellow;
        Handles.DrawSolidDisc(new Vector3(p.x, p.y, 0), Vector3.forward, 0.04f);

        // Shift+Click = stop
        if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
        {
            recording = false;
            Repaint();
            SceneView.RepaintAll();
            e.Use();
            return;
        }

        // Click kiri = tambah titik
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            AddPoint(p);
            Debug.Log($"[Stroke {strokeIndex}] + point {p}");
            e.Use();
        }

        // Backspace = undo
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Backspace)
        {
            UndoPoint();
            e.Use();
        }

        // render garis & dot stroke aktif
        var s = T.strokes[strokeIndex];
        Handles.color = new Color(0, 0, 0, 0.85f);
        for (int i = 0; i < s.points.Count - 1; i++)
            Handles.DrawLine(new Vector3(s.points[i].x, s.points[i].y, 0), new Vector3(s.points[i + 1].x, s.points[i + 1].y, 0));

        if (s.points.Count > 0)
        {
            Handles.color = Color.green;
            Handles.DrawSolidDisc(new Vector3(s.points[0].x, s.points[0].y, 0), Vector3.forward, 0.055f);
            Handles.color = new Color(0, 0.5f, 1f, 1f);
            Handles.DrawSolidDisc(new Vector3(s.points[s.points.Count - 1].x, s.points[s.points.Count - 1].y, 0), Vector3.forward, 0.055f);
        }
    }

    bool ValidStroke() => T.strokes != null && T.strokes.Count > 0 && strokeIndex >= 0 && strokeIndex < T.strokes.Count;

    void AddPoint(Vector2 p)
    {
        if (!ValidStroke()) return;
        Undo.RecordObject(T, "Add Stroke Point");
        T.strokes[strokeIndex].points.Add(p);
        EditorUtility.SetDirty(T);
        SceneView.RepaintAll();
    }

    void UndoPoint()
    {
        if (!ValidStroke()) return;
        var pts = T.strokes[strokeIndex].points;
        if (pts.Count == 0) return;
        Undo.RecordObject(T, "Undo Stroke Point");
        pts.RemoveAt(pts.Count - 1);
        EditorUtility.SetDirty(T);
        SceneView.RepaintAll();
    }
}
#endif
