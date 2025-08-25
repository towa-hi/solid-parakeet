using System;
using UnityEngine;
using UnityEngine.UI;

public class BoardTesterGui : MonoBehaviour
{
    public Button button1;

    public Button button2;

    public Button button3;
    public Button button4;
    public Button button5;
    public Button button6;
    public Button button7;
    public Button button8;

    public event Action OnButton1; 
    public event Action OnButton2; 
    public event Action OnButton3; 
    public event Action OnButton4; 
    public event Action OnButton5; 
    public event Action OnButton6; 
    public event Action OnButton7; 
    public event Action OnButton8; 
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        button1.onClick.AddListener(() =>
        {
            Debug.Log("BoardTestGui clicked button1");
            OnButton1?.Invoke();
        });
        button2.onClick.AddListener(() =>
        {
            Debug.Log("BoardTestGui clicked button2");
            OnButton2?.Invoke();
        });
        button3.onClick.AddListener(() =>
        {
            Debug.Log("BoardTestGui clicked button3");
            OnButton3?.Invoke();
        });
        button4.onClick.AddListener(() =>
        {
            Debug.Log("BoardTestGui clicked button4");
            OnButton4?.Invoke();
        });
        button5.onClick.AddListener(() =>
        {
            Debug.Log("BoardTestGui clicked button5");
            OnButton5?.Invoke();
        });
        button6.onClick.AddListener(() =>
        {
            Debug.Log("BoardTestGui clicked button6");
            OnButton6?.Invoke();
        });
        button7.onClick.AddListener(() =>
        {
            Debug.Log("BoardTestGui clicked button7");
            OnButton7?.Invoke();
        });
        button8.onClick.AddListener(() =>
        {
            Debug.Log("BoardTestGui clicked button8");
            OnButton8?.Invoke();
        });
    }

    
}
