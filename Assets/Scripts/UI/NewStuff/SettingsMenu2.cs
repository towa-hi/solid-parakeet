using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu2 : MenuBase
{
    public Button backButton;
    public Button saveChangesButton;

    private void Start()
    {
        backButton.onClick.AddListener(HandleBack);
        saveChangesButton.onClick.AddListener(HandleSaveChanges);
    }

    public void HandleBack()
    {
        EmitAction(MenuAction.GotoMainMenu);
    }

    public void HandleSaveChanges()
    {
        EmitAction(MenuAction.SaveChanges);
    }
    public override void Refresh()
    {
    }
}


