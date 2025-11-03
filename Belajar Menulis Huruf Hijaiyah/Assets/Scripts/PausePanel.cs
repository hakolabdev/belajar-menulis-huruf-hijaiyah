using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PausePanel : MonoBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Panel Pause")]
    [SerializeField] private GameObject pausePanel;

    private bool isPaused = false;

    void Start()
    {
        // Sembunyikan panel di awal
        pausePanel.SetActive(false);

        // Tambahkan listener ke tombol
        pauseButton.onClick.AddListener(Pause);
        resumeButton.onClick.AddListener(Resume);
        mainMenuButton.onClick.AddListener(MainMenu);
    }

    void Update()
    {
        // Saat tombol back ditekan
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }
    }

    private void Pause()
    {
        SFXManager.Instance.PlaySFX(SFXManager.Instance.audioButtonNegative);
        isPaused = true;
        pausePanel.SetActive(true);
        Time.timeScale = 0f; // Hentikan waktu
    }

    private void Resume()
    {
        SFXManager.Instance.PlaySFX(SFXManager.Instance.audioButtonPositive);
        isPaused = false;
        pausePanel.SetActive(false);
        Time.timeScale = 1f; // Lanjutkan waktu
    }

    private void MainMenu()
    {
        SFXManager.Instance.PlaySFX(SFXManager.Instance.audioButtonPositive);
        Time.timeScale = 1f; // pastikan waktu normal sebelum pindah scene
        SceneManager.LoadScene("MainMenu"); // ganti dengan nama scene utama kamu
    }
}
