using System;
using System.Text.RegularExpressions;
using PrimeTween;
using UnityEngine;
using UnityEngine.Animations;

public class CameraManager : MonoBehaviour
{
    public CameraAnchor startAnchor;
    public CameraAnchor networkAnchor;
    public CameraAnchor boardAnchor;
    public CameraAnchor gateAnchor;
    public CameraAnchor lobbySetupAnchor;
    public CameraAnchor lobbyAnchor;
    public CameraAnchor settingsAnchor;
    public CameraAnchor galleryAnchor;
    public Camera mainCamera;
    public float baseTransitionDuration = 1.5f;
    public ParentConstraint parentConstraint;
    
    [SerializeField] TweenSettings<Vector3> moveSettings;
    [SerializeField] TweenSettings<Vector3> rotationSettings;
    [SerializeField] TweenSettings<float> fovSettings;

    public bool rotateOnMouse;
    public float rotateMaxAngle;
    Quaternion originalOrientation;
    public float basePitch; // X-axis rotation
    public float baseYaw;   // Y-axis rotation
    
    public void Initialize()
    {
        mainCamera.transform.position = startAnchor.GetPosition();
        mainCamera.transform.rotation = startAnchor.GetQuaternion();
        mainCamera.fieldOfView = startAnchor.fov;
        originalOrientation = mainCamera.transform.rotation;
        Vector3 euler = originalOrientation.eulerAngles;
        //SetRotateOnMouse(PlayerPrefs.GetInt("ROTATECAMERA") == 1);
    }

    float GetTransitionDuration()
    {
        int fastMode = PlayerPrefs.GetInt("FASTMODE");
        if (fastMode == 1)
        {
            return baseTransitionDuration * 0.25f;
        }
        else
        {
            return baseTransitionDuration;
        }
    }

    Sequence currentSequence;
    CameraAnchor currentTarget;
    public bool enableCameraMovement;
    
    public void MoveCameraTo(CameraAnchor target, bool inRotateOnMouse)
    {
        if (target.GetPosition() == mainCamera.transform.position)
        {
            Debug.Log("MoveCameraTo: already here");
            SetRotateOnMouse(inRotateOnMouse);
            OnTransitionFinished();
            return;
        }
        currentTarget = target;
        SetRotateOnMouse(false);
        moveSettings.settings.duration = GetTransitionDuration();
        moveSettings.startValue = mainCamera.transform.position;
        moveSettings.endValue = target.GetPosition();
        rotationSettings.settings.duration = GetTransitionDuration();
        rotationSettings.startValue = mainCamera.transform.rotation.eulerAngles;
        rotationSettings.endValue = target.GetEuler();
        fovSettings.settings.duration = GetTransitionDuration();
        fovSettings.startValue = mainCamera.fieldOfView;
        fovSettings.endValue = target.fov;
        Debug.Log($"MoveCameraTo: {target.gameObject.name}");
        currentSequence = Sequence.Create()
            .Group(Tween.Position(mainCamera.transform, moveSettings))
            .Group(Tween.Rotation(mainCamera.transform, rotationSettings))
            .Group(Tween.CameraFieldOfView(mainCamera, fovSettings))
            .ChainCallback(() => SetRotateOnMouse(inRotateOnMouse))
            .ChainCallback(() => OnTransitionFinished());
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

    void OnTransitionFinished()
    {
        //GameManager.instance.guiManager.OnTransitionFinished();
        if (currentTarget != null && currentTarget == boardAnchor)
        {
            //GameManager.instance.boardManager.OnGameStartTransitionFinished();
            if (enableCameraMovement)
            {
                parentConstraint.constraintActive = true;
                SetRotateOnMouse(true);
            }
        }
    }

    Vector2 screenPointerPosition;
    Tween currentTween;
    void Update()
    {
        if (rotateOnMouse && SettingsManager.GetPref(SettingsKey.MOVECAMERA) == 1)
        {
            screenPointerPosition = Globals.InputActions.Game.PointerPosition.ReadValue<Vector2>();
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
