using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    
    public Button startButton;
    public Button settingsButton;
    public Button exitButton;

    public void ShowMainMenu(bool show)
    {
        // internal state cleanup here
        gameObject.SetActive(show);
    }
    void Start()
    {
        startButton.onClick.AddListener(OnStartButton);
        settingsButton.onClick.AddListener(OnSettingsButton);
        exitButton.onClick.AddListener(OnExitButton);
    }
    void OnStartButton()
    {
        GameManager.instance.StartGame();
    }

    void OnSettingsButton()
    {
        // open settings menu
    }

    void OnExitButton()
    {
        GameManager.instance.QuitGame();
    }
}
