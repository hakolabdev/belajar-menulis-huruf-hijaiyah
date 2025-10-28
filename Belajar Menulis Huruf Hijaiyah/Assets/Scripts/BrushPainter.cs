using System.Collections.Generic;
using UnityEngine;

public class BrushPainter : MonoBehaviour
{
    [Header("Brush (Prefab harus SpriteRenderer)")]
    public SpriteRenderer brushPrefab;        // dot sprite (lingkaran kecil)
    [Range(0.01f, 0.5f)] public float spacing = 0.05f; // jarak antar dot (world units)
    [Range(0.02f, 5f)] public float brushSize = 0.12f;
    public Color brushColor = new Color(0.13f, 0.77f, 0.38f, 1f); // hijau

    [Header("Mask & Sorting")]
    public SpriteMask spriteMask;             // SpriteMask huruf (wajib diisi)
    public SpriteRenderer guideSprite;        // SpriteRenderer huruf (buat acuan sorting)

    // runtime
    Vector3? lastPos = null;
    readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> currentStroke = new List<SpriteRenderer>();
    int poolCursor = 0;

    void OnEnable() { poolCursor = 0; }
    void OnDisable() { poolCursor = 0; }

    SpriteRenderer GetDot()
    {
        if (!brushPrefab)
        {
            Debug.LogError("[BrushPainter] brushPrefab kosong!");
            return null;
        }

        if (poolCursor < pool.Count)
        {
            var sr = pool[poolCursor++];
            if (sr) sr.gameObject.SetActive(true);
            return sr;
        }

        var inst = Instantiate(brushPrefab, transform);
        pool.Add(inst);
        poolCursor++;
        return inst;
    }

    public void BeginStroke()
    {
        lastPos = null;
        currentStroke.Clear();
        // reset cursor pool per stroke (opsional: biar reuse) — bisa dihapus kalau mau “permanent ink”
        // poolCursor = 0; // uncomment kalau mau tiap stroke reuse dari awal
    }

    public void Paint(Vector3 worldPos)
    {
        if (lastPos == null)
        {
            PlaceDot(worldPos);
            lastPos = worldPos;
            return;
        }

        var from = lastPos.Value;
        float dist = Vector3.Distance(from, worldPos);
        if (dist < spacing) return;

        int steps = Mathf.CeilToInt(dist / spacing);
        for (int i = 1; i <= steps; i++)
        {
            var t = (float)i / steps;
            var p = Vector3.Lerp(from, worldPos, t);
            PlaceDot(p);
        }
        lastPos = worldPos;
    }

    void PlaceDot(Vector3 p)
    {
        var sr = GetDot();
        if (!sr) return;

        sr.transform.position = new Vector3(p.x, p.y, 0);
        sr.transform.localScale = Vector3.one * brushSize;

        var c = brushColor; c.a = 1f;
        sr.color = c;

        sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        // Sorting supaya brush pasti kelihatan di atas guide dan di-range masker
        if (guideSprite)
        {
            sr.sortingLayerID = guideSprite.sortingLayerID;
            sr.sortingOrder = guideSprite.sortingOrder + 10;
        }

        currentStroke.Add(sr);
    }

    public void CancelStroke() // kalau gagal, buang “tinta” stroke ini
    {
        foreach (var sr in currentStroke)
            if (sr) sr.gameObject.SetActive(false);
        currentStroke.Clear();
        lastPos = null;
    }

    public void EndStroke()    // kalau sukses, biarin “tinta” nempel
    {
        currentStroke.Clear();
        lastPos = null;
    }

    public void ClearAll()     // bersihin semua tinta
    {
        foreach (var sr in pool)
            if (sr) sr.gameObject.SetActive(false);
        poolCursor = 0;
        currentStroke.Clear();
        lastPos = null;
    }
}
