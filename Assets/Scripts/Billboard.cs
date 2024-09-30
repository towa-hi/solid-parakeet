using UnityEngine;

/// <summary>
/// Makes a sprite face the camera at all times (billboard effect).
/// Attach this script to any GameObject with a SpriteRenderer.
/// </summary>
public class Billboard : MonoBehaviour
{
    /// <summary>
    /// The camera that the sprite will face. If null, defaults to Camera.main.
    /// </summary>
    [Tooltip("Camera that the sprite will face. If not set, defaults to the main camera.")]
    public Camera targetCamera;

    /// <summary>
    /// Optional: Lock rotation to specific axes.
    /// Example: To only rotate around the Y-axis, set lockXAxis and lockZAxis to true.
    /// </summary>
    [Header("Axis Constraints (Optional)")]
    [Tooltip("Lock rotation on the X-axis.")]
    public bool lockXAxis = false;

    [Tooltip("Lock rotation on the Y-axis.")]
    public bool lockYAxis = false;

    [Tooltip("Lock rotation on the Z-axis.")]
    public bool lockZAxis = false;

    /// <summary>
    /// Initialization.
    /// </summary>
    void Start()
    {
        // If no camera is assigned, default to the main camera
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                Debug.LogError("Billboard: No camera assigned and Camera.main is null. Please assign a camera.");
            }
        }
    }

    /// <summary>
    /// LateUpdate is called after all Update functions have been called.
    /// </summary>
    void LateUpdate()
    {
        if (targetCamera == null)
            return;

        // Get the direction from the sprite to the camera
        Vector3 direction = targetCamera.transform.position - transform.position;

        // Calculate the desired rotation
        Quaternion rotation = Quaternion.LookRotation(direction);

        // Apply axis constraints if any
        Vector3 euler = rotation.eulerAngles;

        if (lockXAxis)
            euler.x = 0f;

        if (lockYAxis)
            euler.y = 0f;

        if (lockZAxis)
            euler.z = 0f;

        rotation = Quaternion.Euler(euler);

        // Apply the rotation to the sprite
        transform.rotation = rotation;
    }
}
