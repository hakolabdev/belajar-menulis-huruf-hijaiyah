using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelAngkaManager : MonoBehaviour
{
    [SerializeField] LevelList levelList;
    [SerializeField] private GameObject buttonLevelPrefab;
    [SerializeField] private GameObject content;

    private int startIndex = 30; // Index awal mulai dari 30

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

            // Gunakan index mulai dari 30
            int levelIndex = startIndex + i;
            newButton.GetComponent<ButtonLevel>().levelButton.onClick.AddListener(() => OnLevelSelected(levelIndex));
        }
    }

    void OnLevelSelected(int levelIndex)
    {
        Debug.Log("Level dipilih: " + levelIndex);

        PlayerPrefs.SetInt("Level", levelIndex);
        SceneManager.LoadScene("Game");
    }
}
