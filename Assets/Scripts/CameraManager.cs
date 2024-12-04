using System;
using System.Text.RegularExpressions;
using PrimeTween;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public CameraAnchor startAnchor;
    public CameraAnchor boardAnchor;
    public CameraAnchor gateAnchor;
    public CameraAnchor lobbySetupAnchor;
    public CameraAnchor lobbyAnchor;
    public CameraAnchor settingsAnchor;
    public CameraAnchor galleryAnchor;
    public Camera mainCamera;
    [SerializeField] TweenSettings<Vector3> moveSettings;
    [SerializeField] TweenSettings<Vector3> rotationSettings;
    [SerializeField] TweenSettings<float> fovSettings;
    public void Initialize()
    {
        GameManager.instance.guiManager.OnShowMenu += OnShowMenu;
        mainCamera.transform.position = startAnchor.GetPosition();
        mainCamera.transform.rotation = startAnchor.GetQuaternion();
        mainCamera.fieldOfView = startAnchor.fov;

    }

    void OnShowMenu(MenuElement menu)
    {
        switch (menu)
        {
            case GuiStartMenu guiStartMenu:
                MoveCameraTo(startAnchor);
                break;
            case GuiMainMenu guiMainMenu:
                MoveCameraTo(gateAnchor);
                break;
            case GuiSettingsMenu guiSettingsMenu:
                MoveCameraTo(settingsAnchor);
                break;
            case GuiLobbySetupMenu guiLobbySetupMenu:
                MoveCameraTo(lobbySetupAnchor);
                break;
            case GuiLobbyMenu guiLobbyMenu:
                MoveCameraTo(lobbyAnchor);
                break;
            case GuiGame guiGame:
                MoveCameraTo(boardAnchor);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(menu));
        }
    }

    void MoveCameraTo(CameraAnchor target)
    {
        if (target.GetPosition() == mainCamera.transform.position)
        {
            Debug.Log("MoveCameraTo: already here");
            return;
        }

        moveSettings.startValue = mainCamera.transform.position;
        moveSettings.endValue = target.GetPosition();
        rotationSettings.startValue = mainCamera.transform.rotation.eulerAngles;
        rotationSettings.endValue = target.GetEuler();
        fovSettings.startValue = mainCamera.fieldOfView;
        fovSettings.endValue = target.fov;
        Debug.Log($"MoveCameraTo: {target.gameObject.name}");
        Sequence.Create()
            .Group(Tween.Position(mainCamera.transform, moveSettings))
            .Group(Tween.Rotation(mainCamera.transform, rotationSettings))
            .Group(Tween.CameraFieldOfView(mainCamera, fovSettings));
    }


}
