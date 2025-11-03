using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelHurufManager : MonoBehaviour
{
    [SerializeField] LevelList levelList;
    [SerializeField] private GameObject buttonLevelPrefab;
    [SerializeField] private GameObject content;

    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < levelList.levels.Length; i++)
        {
            HijaiyahLevelData level = levelList.levels[i];

            GameObject newButton = Instantiate(buttonLevelPrefab, content.transform);
            newButton.name = level.letterName;

            // Set UI text & image
            Image imageComponent = newButton.GetComponent<ButtonLevel>().icon;

            if (imageComponent != null)
                imageComponent.sprite = level.guideSprite;

            // Capture the current index to avoid closure bug
            int index = i;
            newButton.GetComponent<ButtonLevel>().levelButton.onClick.AddListener(() => OnLevelSelected(index));
        }
    }

    void OnLevelSelected(int levelIndex)
    {
        PlayerPrefs.SetInt("Level", levelIndex);
        SceneManager.LoadScene("Game");
    }
}
