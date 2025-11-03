using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject hurufHijaiyahPanel;
    [SerializeField] private GameObject angkaHijaiyahPanel;

    [Header("Button")]
    [SerializeField] private Button hurufHijaiyahButton;
    [SerializeField] private Button angkaHijaiyahButton;
    [SerializeField] private Button backHurufButton;
    [SerializeField] private Button backAngkaButton;

    void Start()
    {
        // Awal: hanya tampilkan main panel
        mainPanel.SetActive(true);
        hurufHijaiyahPanel.SetActive(false);
        angkaHijaiyahPanel.SetActive(false);

        // Event tombol
        hurufHijaiyahButton.onClick.AddListener(HurufHijaiyahList);
        angkaHijaiyahButton.onClick.AddListener(AngkaHijaiyahList);
        backHurufButton.onClick.AddListener(HandleBackButton);
        backAngkaButton.onClick.AddListener(HandleBackButton);
    }

    void Update()
    {
        // Deteksi tombol back Android
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleBackButton();
        }
    }

    private void HurufHijaiyahList()
    {
        SFXManager.Instance.PlaySFX(SFXManager.Instance.audioButtonPositive);
        hurufHijaiyahPanel.SetActive(true);
        angkaHijaiyahPanel.SetActive(false);
    }

    private void AngkaHijaiyahList()
    {
        SFXManager.Instance.PlaySFX(SFXManager.Instance.audioButtonPositive);
        hurufHijaiyahPanel.SetActive(false);
        angkaHijaiyahPanel.SetActive(true);
    }

    private void HandleBackButton()
    {
        SFXManager.Instance.PlaySFX(SFXManager.Instance.audioButtonNegative);

        // Jika sedang di panel huruf → kembali ke main
        if (hurufHijaiyahPanel.activeSelf)
        {
            hurufHijaiyahPanel.SetActive(false);
            mainPanel.SetActive(true);
        }
        // Jika sedang di panel angka → kembali ke main
        else if (angkaHijaiyahPanel.activeSelf)
        {
            angkaHijaiyahPanel.SetActive(false);
            mainPanel.SetActive(true);
        }
        // Jika sudah di main panel → keluar aplikasi
        else if (mainPanel.activeSelf)
        {
            Application.Quit();
        }
    }
}
