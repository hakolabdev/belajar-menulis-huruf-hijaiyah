#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[CustomEditor(typeof(HijaiyahWriter))]
public class HijaiyahWriterEditor : Editor
{
    HijaiyahWriter T;

    bool recording = false;
    int strokeIndex = 0;
    float gridSnap = 0.1f;

    // cache refresh guide
    HijaiyahLevelData _lastLevelRef;
    Sprite _lastAppliedGuide;

    // editor-only preview index (disimpan di EditorPrefs biar gak nambah field di component)
    const string kPreviewKey = "HW_EditorPreviewIndex";

    // ======== Level Resolver (Editor) ========
    HijaiyahLevelData Level
    {
        get
        {
            if (T == null || T == null) return null;
            var levelListField = serializedObject.FindProperty("levelList");
            if (levelListField == null || levelListField.objectReferenceValue == null) return null;

            var levelList = (LevelList)levelListField.objectReferenceValue;
            if (levelList.levels == null || levelList.levels.Length == 0) return null;

            // ambil index preview dari EditorPrefs; default = PlayerPrefs("Level")
            int def = Mathf.Clamp(PlayerPrefs.GetInt("Level", 0), 0, levelList.levels.Length - 1);
            int idx = EditorPrefs.GetInt(kPreviewKey, def);
            idx = Mathf.Clamp(idx, 0, levelList.levels.Length - 1);

            return levelList.levels[idx];
        }
    }

    int GetPreviewIndex(out int max)
    {
        max = 0;
        var levelListField = serializedObject.FindProperty("levelList");
        if (levelListField == null || levelListField.objectReferenceValue == null) return 0;
        var levelList = (LevelList)levelListField.objectReferenceValue;
        if (levelList.levels == null) return 0;
        max = Mathf.Max(0, levelList.levels.Length - 1);

        int def = Mathf.Clamp(PlayerPrefs.GetInt("Level", 0), 0, max);
        return Mathf.Clamp(EditorPrefs.GetInt(kPreviewKey, def), 0, max);
    }

    void SetPreviewIndex(int idx)
    {
        EditorPrefs.SetInt(kPreviewKey, Mathf.Max(0, idx));
        ApplyLevelVisuals(true);
        Repaint();
        SceneView.RepaintAll();
    }

    // ======== Unity Editor Hooks ========
    void OnEnable()
    {
        T = (HijaiyahWriter)target;

        Undo.undoRedoPerformed += ApplyLevelVisuals;
        EditorApplication.projectChanged += ApplyLevelVisuals;
        EditorApplication.update += OnEditorUpdate;

        SceneView.duringSceneGui += DuringSceneGUI;

        ApplyLevelVisuals(true);
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        Undo.undoRedoPerformed -= ApplyLevelVisuals;
        EditorApplication.projectChanged -= ApplyLevelVisuals;
        EditorApplication.update -= OnEditorUpdate;
    }

    void OnEditorUpdate()
    {
        if (EditorApplication.isPlaying) return;
        ApplyLevelVisuals();
        if (recording) SceneView.RepaintAll();
    }

    // === GUIDE REFRESH ===
    void ApplyLevelVisuals() => ApplyLevelVisuals(false);
    void ApplyLevelVisuals(bool force)
    {
        if (T == null) return;

        var sr = T.guideSprite;
        var lvl = Level; // <<== pakai Level hasil resolve
        var targetSprite = lvl != null ? lvl.guideSprite : null;

        if (sr == null)
        {
            _lastLevelRef = lvl;
            _lastAppliedGuide = targetSprite;
            return;
        }

        bool changed =
            force ||
            _lastLevelRef != lvl ||
            _lastAppliedGuide != targetSprite ||
            sr.sprite != targetSprite;

        if (!changed) return;

        Undo.RecordObject(sr, "Apply Guide Sprite");
        sr.sprite = targetSprite;
        var mask = sr.GetComponent<SpriteMask>();
        if (mask) mask.sprite = targetSprite;
        EditorUtility.SetDirty(sr);

        _lastLevelRef = lvl;
        _lastAppliedGuide = targetSprite;

        Repaint();
        SceneView.RepaintAll();
    }

    // === INSPECTOR ===
    public override void OnInspectorGUI()
    {
        // tampilkan inspector default (tanpa field levelData, karena udah dihapus di runtime)
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
            ApplyLevelVisuals(true);

        // ===== Preview picker (LevelList based) =====
        var levelListProp = serializedObject.FindProperty("levelList");
        if (levelListProp == null || levelListProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign LevelList di atas untuk mulai edit level.", MessageType.Info);
            return;
        }

        var levelList = (LevelList)levelListProp.objectReferenceValue;
        int max;
        int cur = GetPreviewIndex(out max);

        using (new EditorGUI.DisabledScope(levelList.levels == null || levelList.levels.Length == 0))
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("=== Level Preview ===", EditorStyles.boldLabel);

            var names = (levelList.levels == null || levelList.levels.Length == 0)
                ? new[] { "(no levels)" }
                : levelList.levels.Select((x, i) => x ? $"{i:00} - {x.name}" : $"{i:00} - (null)").ToArray();

            int newIdx = EditorGUILayout.Popup("Preview Level", cur, names);
            if (newIdx != cur) SetPreviewIndex(newIdx);

            if (GUILayout.Button("Refresh Guide From Level"))
                ApplyLevelVisuals(true);

            // tombol buat nambah level baru ke LevelList
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Level (SO)"))
            {
                var asset = ScriptableObject.CreateInstance<HijaiyahLevelData>();
                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Hijaiyah Level",
                    "HijaiyahLevel",
                    "asset",
                    "Pilih lokasi menyimpan HijaiyahLevelData"
                );
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(asset, path);
                    AssetDatabase.SaveAssets();

                    Undo.RecordObject(levelList, "Add Level To LevelList");
                    var list = new List<HijaiyahLevelData>(levelList.levels ?? new HijaiyahLevelData[0]);
                    list.Add(asset);
                    levelList.levels = list.ToArray();
                    EditorUtility.SetDirty(levelList);

                    // auto select level baru
                    SetPreviewIndex((levelList.levels?.Length ?? 1) - 1);
                }
            }
            if (GUILayout.Button("Remove Preview Level"))
            {
                if (levelList.levels != null && levelList.levels.Length > 0 && cur < levelList.levels.Length)
                {
                    if (EditorUtility.DisplayDialog("Remove Level",
                        $"Hapus level index {cur} dari LevelList? (asset tidak dihapus)", "Remove", "Cancel"))
                    {
                        Undo.RecordObject(levelList, "Remove Level From LevelList");
                        var list = new List<HijaiyahLevelData>(levelList.levels);
                        list.RemoveAt(cur);
                        levelList.levels = list.ToArray();
                        EditorUtility.SetDirty(levelList);
                        SetPreviewIndex(Mathf.Clamp(cur - 1, 0, (levelList.levels?.Length ?? 1) - 1));
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("=== Stroke Recorder (Active Level) ===", EditorStyles.boldLabel);

        if (Level == null)
        {
            EditorGUILayout.HelpBox("Level aktif NULL. Pilih LevelList & Preview Level yang valid.", MessageType.Warning);
            return;
        }

        // init list strokes
        if (Level.strokes == null)
        {
            Undo.RecordObject(Level, "Init Strokes List");
            Level.strokes = new List<Stroke>();
            EditorUtility.SetDirty(Level);
        }

        // === Dropdown pilih stroke aktif ===
        var strokeNames = GetStrokeNames(Level.strokes);
        EnsureIndexValid(Level.strokes.Count);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Active Stroke", GUILayout.Width(100));
        int newStrokeIdx = EditorGUILayout.Popup(strokeIndex, strokeNames);
        if (newStrokeIdx != strokeIndex)
        {
            strokeIndex = newStrokeIdx;
            Repaint();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("New", GUILayout.Width(60)))
        {
            Undo.RecordObject(Level, "New Stroke");
            Level.strokes.Add(new Stroke() { name = $"stroke-{Level.strokes.Count}" });
            strokeIndex = Level.strokes.Count - 1;
            EditorUtility.SetDirty(Level);
        }
        using (new EditorGUI.DisabledScope(!ValidStroke()))
        {
            if (GUILayout.Button("Duplicate", GUILayout.Width(80)))
            {
                var s = Level.strokes[strokeIndex];
                Undo.RecordObject(Level, "Duplicate Stroke");
                var dup = new Stroke()
                {
                    name = s.name + " (copy)",
                    tolerance = s.tolerance,
                    startRadius = s.startRadius,
                    endRadius = s.endRadius,
                    points = new List<Vector2>(s.points)
                };
                Level.strokes.Insert(strokeIndex + 1, dup);
                strokeIndex++;
                EditorUtility.SetDirty(Level);
            }
            if (GUILayout.Button("Delete", GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog("Delete Stroke", "Yakin hapus stroke ini?", "Delete", "Cancel"))
                {
                    Undo.RecordObject(Level, "Delete Stroke");
                    Level.strokes.RemoveAt(strokeIndex);
                    strokeIndex = Mathf.Clamp(strokeIndex, 0, Level.strokes.Count - 1);
                    EditorUtility.SetDirty(Level);
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        // Nama stroke + properti
        if (ValidStroke())
        {
            var s = Level.strokes[strokeIndex];
            string newName = EditorGUILayout.TextField("Stroke Name", s.name);
            if (newName != s.name)
            {
                Undo.RecordObject(Level, "Rename Stroke");
                s.name = string.IsNullOrWhiteSpace(newName) ? s.name : newName;
                EditorUtility.SetDirty(Level);
            }

            s.tolerance = EditorGUILayout.Slider("Tolerance", s.tolerance, 0.05f, 2f);
            s.startRadius = EditorGUILayout.Slider("Start Radius", s.startRadius, 0.05f, 2f);
            s.endRadius = EditorGUILayout.Slider("End Radius", s.endRadius, 0.05f, 2f);
            EditorGUILayout.LabelField($"Points: {s.points.Count}");
        }

        // Rekam titik
        gridSnap = EditorGUILayout.FloatField("Grid Snap (world)", gridSnap);
        EditorGUILayout.BeginHorizontal();
        var btnLabel = recording ? "Stop Recording" : "Start Recording";
        if (GUILayout.Button(btnLabel))
        {
            recording = !recording;

            if (recording && Level.strokes.Count == 0)
            {
                Undo.RecordObject(Level, "Auto Create Stroke");
                Level.strokes.Add(new Stroke() { name = "stroke-0" });
                strokeIndex = 0;
                EditorUtility.SetDirty(Level);
            }
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Clear Points"))
        {
            if (ValidStroke())
            {
                Undo.RecordObject(Level, "Clear Stroke");
                Level.strokes[strokeIndex].points.Clear();
                EditorUtility.SetDirty(Level);
                SceneView.RepaintAll();
            }
        }
        if (GUILayout.Button("Undo Point"))
        {
            if (ValidStroke() && Level.strokes[strokeIndex].points.Count > 0)
            {
                Undo.RecordObject(Level, "Undo Stroke Point");
                var pts = Level.strokes[strokeIndex].points;
                pts.RemoveAt(pts.Count - 1);
                EditorUtility.SetDirty(Level);
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "Tips:\n" +
            "- Pilih LevelList & Preview Level untuk edit.\n" +
            "- Start Recording lalu klik di Scene View (plane z=0) untuk nambah titik.\n" +
            "- Shift+Click = stop recording. Backspace = undo titik.\n",
            MessageType.Info
        );
    }

    // === SCENE VIEW: gambar SEMUA stroke + edit stroke aktif ===
    void DuringSceneGUI(SceneView sv)
    {
        if (Level == null || Level.strokes == null) return;

        for (int i = 0; i < Level.strokes.Count; i++)
            DrawStrokeGizmos(Level.strokes[i], i == strokeIndex);

        if (!recording || !ValidStroke()) return;
        if (Selection.activeGameObject != ((HijaiyahWriter)target).gameObject) return;

        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        // ray ke plane z=0
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
            e.Use();
        }

        // Backspace = undo titik terakhir
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Backspace)
        {
            UndoPoint();
            e.Use();
        }
    }

    void DrawStrokeGizmos(Stroke s, bool isActive)
    {
        if (s == null) return;

        Color lineCol = isActive ? new Color(0f, 0f, 0f, 0.95f) : new Color(0f, 0f, 0f, 0.35f);
        Color startCol = isActive ? Color.green : new Color(0.2f, 0.6f, 0.2f, 0.5f);
        Color endCol = isActive ? new Color(0f, 0.5f, 1f, 1f) : new Color(0f, 0.5f, 1f, 0.5f);

        Handles.color = lineCol;
        for (int i = 0; i < s.points.Count - 1; i++)
        {
            var a = s.points[i];
            var b = s.points[i + 1];
            Handles.DrawLine(new Vector3(a.x, a.y, 0), new Vector3(b.x, b.y, 0));
        }

        if (s.points.Count > 0)
        {
            Handles.color = startCol;
            Handles.DrawSolidDisc(new Vector3(s.points[0].x, s.points[0].y, 0), Vector3.forward, 0.055f);

            Handles.color = endCol;
            var last = s.points[s.points.Count - 1];
            Handles.DrawSolidDisc(new Vector3(last.x, last.y, 0), Vector3.forward, 0.055f);
        }

        if (s.points != null && s.points.Count > 0)
        {
            Handles.color = isActive ? new Color(0f, 0f, 0f, 0.7f) : new Color(0f, 0f, 0f, 0.25f);
            float r = isActive ? 0.025f : 0.018f;
            foreach (var p in s.points)
                Handles.DrawSolidDisc(new Vector3(p.x, p.y, 0), Vector3.forward, r);
        }

        if (!string.IsNullOrEmpty(s.name) && s.points.Count > 0)
        {
            Handles.color = Color.white;
            var pos = new Vector3(s.points[0].x, s.points[0].y, 0);
            GUIStyle lbl = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = isActive ? Color.white : new Color(1f, 1f, 1f, 0.7f) },
                alignment = TextAnchor.MiddleLeft
            };
            Handles.Label(pos + new Vector3(0.06f, 0.06f, 0), s.name, lbl);
        }
    }

    bool ValidStroke()
        => Level != null && Level.strokes != null && Level.strokes.Count > 0 &&
           strokeIndex >= 0 && strokeIndex < Level.strokes.Count;

    void EnsureIndexValid(int count)
    {
        if (count == 0) { strokeIndex = 0; return; }
        if (strokeIndex < 0 || strokeIndex >= count)
            strokeIndex = Mathf.Clamp(strokeIndex, 0, count - 1);
    }

    string[] GetStrokeNames(List<Stroke> strokes)
    {
        if (strokes == null || strokes.Count == 0)
            return new[] { "(no strokes)" };
        return strokes.Select((s, i) =>
            string.IsNullOrWhiteSpace(s.name) ? $"stroke-{i}" : s.name).ToArray();
    }

    void AddPoint(Vector2 p)
    {
        if (!ValidStroke()) return;
        Undo.RecordObject(Level, "Add Stroke Point");
        Level.strokes[strokeIndex].points.Add(p);
        EditorUtility.SetDirty(Level);
        SceneView.RepaintAll();
    }

    void UndoPoint()
    {
        if (!ValidStroke()) return;
        var pts = Level.strokes[strokeIndex].points;
        if (pts.Count == 0) return;
        Undo.RecordObject(Level, "Undo Stroke Point");
        pts.RemoveAt(pts.Count - 1);
        EditorUtility.SetDirty(Level);
        SceneView.RepaintAll();
    }
}
#endif
