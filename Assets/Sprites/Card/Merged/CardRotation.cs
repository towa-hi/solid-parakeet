using UnityEngine;

public class CardRotation : MonoBehaviour
{
    [Tooltip("Max tilt angle (°) along X/Y axes.")]
    public float maxRotationAngle = 10f;
    [Tooltip("How quickly the card eases into its target rotation.")]
    public float smoothSpeed = 5f;

    private Quaternion _targetRot;

    void Update()
    {
        // 1) Read pointer in screen‐space
        Vector2 pointer = Globals.InputActions.Game.PointerPosition.ReadValue<Vector2>();

        // 2) Screen center
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // 3) Delta from center, normalized to [-1…1]
        Vector2 delta = pointer - screenCenter;
        float nx = Mathf.Clamp(delta.x / screenCenter.x, -1f, 1f);
        float ny = Mathf.Clamp(delta.y / screenCenter.y, -1f, 1f);

        // 4) Map into tilt angles (invert Y so moving pointer up tilts “away”)
        float rotX = -ny * maxRotationAngle;
        float rotY =  nx * maxRotationAngle;

        // 5) Build target rotation
        _targetRot = Quaternion.Euler(rotX, rotY, 0f);

        // 6) Smoothly slerp into it
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            _targetRot,
            Time.deltaTime * smoothSpeed
        );
    }
}
