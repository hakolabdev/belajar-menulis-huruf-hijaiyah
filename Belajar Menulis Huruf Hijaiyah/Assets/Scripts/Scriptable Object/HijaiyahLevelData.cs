using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HijaiyahLevel", menuName = "Hijaiyah/Level Data", order = 1)]
public class HijaiyahLevelData : ScriptableObject
{
    [Header("Meta Data")]
    public string letterName = "Alif";

    [Header("Visual")]
    public Sprite guideSprite;

    [Header("Audio")]
    public AudioClip sound;

    [Header("Stroke Data")]
    public List<Stroke> strokes = new List<Stroke>();
}
