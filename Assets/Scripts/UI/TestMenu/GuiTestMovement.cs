using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiTestMovement : GameElement
{
    public TextMeshProUGUI statusText;
    public Button menuButton;
    public Button extraButton;
    public Button submitMoveButton;
    public Button graveyardButton;
    public Button refreshButton;

    public event Action OnMenuButton;
    public event Action OnExtraButton;
    public event Action OnSubmitMoveButton;
    public event Action OnGraveyardButton;
    public event Action OnRefreshButton;

    void Start()
    {
        menuButton.onClick.AddListener(() => OnMenuButton?.Invoke());
        extraButton.onClick.AddListener(() => OnExtraButton?.Invoke());
        submitMoveButton.onClick.AddListener(() => OnSubmitMoveButton?.Invoke());
        graveyardButton.onClick.AddListener(() => OnGraveyardButton?.Invoke());
        refreshButton.onClick.AddListener(() => OnRefreshButton?.Invoke());
    }

    public override void Initialize(TestBoardManager bm)
    {
        statusText.text = "status";
        Refresh(bm);
    }

    public override void Refresh(TestBoardManager bm)
    {
        
    }
}
