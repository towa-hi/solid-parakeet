using System;
using System.Text.RegularExpressions;
using PrimeTween;
using UnityEngine;
using UnityEngine.Animations;

public class CameraManager : MonoBehaviour
{
    public CameraAnchor outsideAnchor;
    public CameraAnchor lairOuterAnchor;
    public CameraAnchor lairInnerAnchor;
    public CameraAnchor lairAltarAnchor;
    public CameraAnchor lairDungeonAnchor;
    public CameraAnchor boardAnchor;
    
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
        mainCamera.transform.position = outsideAnchor.GetPosition();
        mainCamera.transform.rotation = outsideAnchor.GetQuaternion();
        mainCamera.fieldOfView = outsideAnchor.fov;
        originalOrientation = mainCamera.transform.rotation;
    }

    float GetTransitionDuration()
    {
        WarmancerSettings s = SettingsManager.Load();
        if (s.fastMode)
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
    
    public void MoveCameraTo(Area area, bool inRotateOnMouse)
    {
        CameraAnchor target = area switch
        {
            Area.OUTSIDE => outsideAnchor,
            Area.LAIR_OUTER => lairOuterAnchor,
            Area.LAIR_INNER => lairInnerAnchor,
            Area.LAIR_ALTAR => lairAltarAnchor,
            Area.LAIR_DUNGEON => lairDungeonAnchor,
            Area.BOARD => boardAnchor,
            _ => throw new ArgumentOutOfRangeException(nameof(area), area, null)
        };
        if (target.GetPosition() == mainCamera.transform.position)
        {
            Debug.Log("MoveCameraTo: already here");
            SetRotateOnMouse(inRotateOnMouse);
            OnTransitionFinished();
            return;
        }
        currentTarget = target;
        SetRotateOnMouse(false);
        parentConstraint.constraintActive = false;
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
        if (currentTarget is CameraAnchor currentTargetValue)
        {
            SetRotateOnMouse(enableCameraMovement);
            if (currentTargetValue == boardAnchor)
            {
                parentConstraint.constraintActive = enableCameraMovement;
            }
        }
    }

    Vector2 screenPointerPosition;
    Tween currentTween;
    void Update()
    {
        if (rotateOnMouse && SettingsManager.Load().moveCamera)
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

public enum Area
{
    OUTSIDE,
    LAIR_OUTER,
    LAIR_INNER,
    LAIR_ALTAR,
    LAIR_DUNGEON,
    BOARD,
}