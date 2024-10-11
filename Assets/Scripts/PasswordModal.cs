using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PasswordModal : MonoBehaviour
{
    public static PasswordModal instance;
    
    public TMP_InputField passwordInput;
    public Button backButton;
    public Button startButton;

    string password;
    int maxPasswordLength = 5;
    bool passwordValid;

    void Awake()
    {
        instance = this;
        backButton.onClick.AddListener(OnBackButtonClicked);
        startButton.onClick.AddListener(OnStartButton);
        passwordInput.onValueChanged.AddListener(OnPasswordChanged);
    }
    
    public void Show(bool show)
    {
        gameObject.SetActive(show);
        passwordInput.text = "";
        password = "";
        passwordValid = false;
        // Disable the continue button initially
        startButton.interactable = false;
        
    }
    
    void OnBackButtonClicked()
    {
        
    }

    void OnStartButton()
    {
        if (passwordValid)
        {
            GameManager.instance.OnPasswordEntered(password);
        }
    }

    void OnPasswordChanged(string inPassword)
    {
        password = inPassword;
        // Allow only numbers and a max length of 5
        string filteredPassword = Regex.Replace(inPassword, "[^0-9]", ""); // Remove non-numeric characters

        // Check if the password exceeds the max length
        if (filteredPassword.Length > maxPasswordLength)
        {
            filteredPassword = filteredPassword.Substring(0, maxPasswordLength);
        }

        passwordInput.text = filteredPassword; // Set the filtered password back to the input field
        password = filteredPassword;           // Update the password field
        passwordValid = filteredPassword.Length == maxPasswordLength;
        startButton.interactable = passwordValid;
    }

}
