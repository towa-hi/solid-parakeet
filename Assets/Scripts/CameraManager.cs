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

    public bool rotateOnMouse;
    public float rotateMaxAngle;
    Quaternion originalOrientation;
    float basePitch; // X-axis rotation
    float baseYaw;   // Y-axis rotation
    
    public void Initialize()
    {
        GameManager.instance.guiManager.OnShowMenu += OnShowMenu;
        mainCamera.transform.position = startAnchor.GetPosition();
        mainCamera.transform.rotation = startAnchor.GetQuaternion();
        mainCamera.fieldOfView = startAnchor.fov;

    }

    MenuElement currentElement;
    void OnShowMenu(MenuElement menu)
    {
        switch (menu)
        {
            case GuiStartMenu guiStartMenu:
                MoveCameraTo(startAnchor, false);
                break;
            case GuiMainMenu guiMainMenu:
                MoveCameraTo(gateAnchor, false);
                break;
            case GuiSettingsMenu guiSettingsMenu:
                MoveCameraTo(settingsAnchor, false);
                break;
            case GuiLobbySetupMenu guiLobbySetupMenu:
                MoveCameraTo(lobbySetupAnchor, false);
                break;
            case GuiLobbyMenu guiLobbyMenu:
                MoveCameraTo(lobbyAnchor, false);
                break;
            case GuiGame guiGame:
                MoveCameraTo(boardAnchor, true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(menu));
        }

        currentElement = menu;
    }

    Sequence currentSequence;
    
    void MoveCameraTo(CameraAnchor target, bool inRotateOnMouse)
    {
        if (target.GetPosition() == mainCamera.transform.position)
        {
            Debug.Log("MoveCameraTo: already here");
            return;
        }
        SetRotateOnMouse(false);
        moveSettings.startValue = mainCamera.transform.position;
        moveSettings.endValue = target.GetPosition();
        rotationSettings.startValue = mainCamera.transform.rotation.eulerAngles;
        rotationSettings.endValue = target.GetEuler();
        fovSettings.startValue = mainCamera.fieldOfView;
        fovSettings.endValue = target.fov;
        Debug.Log($"MoveCameraTo: {target.gameObject.name}");
        currentSequence = Sequence.Create()
            .Group(Tween.Position(mainCamera.transform, moveSettings))
            .Group(Tween.Rotation(mainCamera.transform, rotationSettings))
            .Group(Tween.CameraFieldOfView(mainCamera, fovSettings))
            .ChainCallback(() => SetRotateOnMouse(inRotateOnMouse));
    }

    void SetRotateOnMouse(bool inRotateOnMouse)
    {
        rotateOnMouse = inRotateOnMouse;
        if (rotateOnMouse)
        {
            originalOrientation = mainCamera.transform.rotation;
            Vector3 euler = originalOrientation.eulerAngles;
            basePitch = euler.x;
            baseYaw = euler.y;
        }
    }

    Vector2 screenPointerPosition;
    Tween currentTween;
    void Update()
    {
        if (rotateOnMouse && GameManager.instance.enableCameraRotation)
        {
            screenPointerPosition = Globals.inputActions.Game.PointerPosition.ReadValue<Vector2>();
            if (!IsPointerWithinScreen(screenPointerPosition))
            {
                return;
            }
            float pointerX = screenPointerPosition.x;
            float pointerY = screenPointerPosition.y;
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            // Normalize mouse positions to range [-1, 1]
            float normalizedX = (pointerX / screenWidth - 0.5f) * 2f;
            float normalizedY = (pointerY / screenHeight - 0.5f) * 2f;

            normalizedX = Mathf.Clamp(normalizedX, -1f, 1f);
            normalizedY = Mathf.Clamp(normalizedY, -1f, 1f);

            // Calculate rotation angles
            float angleYaw = normalizedX * rotateMaxAngle; // Y-axis rotation (yaw)
            float anglePitch = -normalizedY * rotateMaxAngle; // X-axis rotation (pitch)

            // Calculate new rotation angles
            float newYaw = baseYaw + angleYaw;
            float newPitch = basePitch + anglePitch;

            // Clamp the pitch angle to prevent flipping
            newPitch = Mathf.Clamp(newPitch, basePitch - rotateMaxAngle, basePitch + rotateMaxAngle);

            // Apply the new rotation
            Quaternion finalRotation = Quaternion.Euler(newPitch, newYaw, 0f);
            if (mainCamera.transform.rotation != finalRotation)
            {
                Tween.Rotation(mainCamera.transform, finalRotation, 0.3f);
            }
        }
    }
    
    bool IsPointerWithinScreen(Vector2 pointerPosition)
    {
        return pointerPosition.x >= 0 && pointerPosition.x <= Screen.width &&
               pointerPosition.y >= 0 && pointerPosition.y <= Screen.height;
    }
}
