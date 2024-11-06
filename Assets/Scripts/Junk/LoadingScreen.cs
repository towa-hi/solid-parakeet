using UnityEngine;

public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen instance;
    
    void Awake()
    {
        instance = this;
        ShowLoadingScreen(false);
    }

    public void ShowLoadingScreen(bool show)
    {
        gameObject.SetActive(show);
    }
}
