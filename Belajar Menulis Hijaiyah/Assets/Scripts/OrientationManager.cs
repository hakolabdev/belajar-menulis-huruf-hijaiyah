using UnityEngine;

public class OrientationManager : MonoBehaviour
{
    public static OrientationManager Instance;

    void Awake()
    {
        // Singleton: hanya satu instance
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // tetap ada di semua scene
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Atur AutoRotation Landscape
        Screen.orientation = ScreenOrientation.AutoRotation;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
    }
}
