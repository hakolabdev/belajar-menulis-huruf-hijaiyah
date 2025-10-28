using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Hijaiyah/Letter", fileName = "New Hijaiyah Letter")]
public class HijaiyahLetter : ScriptableObject
{
    [Header("Meta")]
    public string letterName = "Alif";

    [Header("Visual")]
    public Sprite guideSprite;

    [Header("Audio")]
    public AudioClip sound; 

    [Header("Strokes (urutan penting)")]
    public List<Stroke> strokes = new List<Stroke>();
}
