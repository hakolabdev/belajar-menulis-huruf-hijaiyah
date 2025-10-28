using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Stroke
{
    public string name = "stroke";
    // Jalur target dalam world space (z=0). Isi via recorder/editor atau manual.
    public List<Vector2> points = new List<Vector2>();

    [Header("Validation")]
    [Range(0.05f, 2f)] public float tolerance = 0.35f;   // lebar koridor (jarak max dari garis)
    [Range(0.05f, 2f)] public float startRadius = 0.4f;  // area sah buat mulai
    [Range(0.05f, 2f)] public float endRadius = 0.4f;    // area sah buat selesai
}

