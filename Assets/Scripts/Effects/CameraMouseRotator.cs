using UnityEngine;

public class CameraMouseRotator : MonoBehaviour
{
    [SerializeField] private float rotationStrength = 5f; // How strong the rotation is
    [SerializeField] private float maxRotationAngle = 45f; // Maximum rotation angle in degrees
    [SerializeField] private Camera targetCamera; // The camera to rotate (defaults to main camera if not set)

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        RotateCameraTowardsMouse();
    }

    private void RotateCameraTowardsMouse()
    {
        if (targetCamera == null)
        {
            Debug.LogWarning("No camera assigned to CameraMouseRotator.");
            return;
        }

        // Get the mouse position in screen space
        Vector3 mousePosition = Input.mousePosition;

        // Convert the mouse position to a point in the world space
        Ray ray = targetCamera.ScreenPointToRay(mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 targetPosition = hit.point;

            // Calculate the direction to look at
            Vector3 direction = targetPosition - targetCamera.transform.position;
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // Interpolate rotation towards the target
            targetCamera.transform.rotation = Quaternion.Slerp(
                targetCamera.transform.rotation,
                targetRotation,
                rotationStrength * Time.deltaTime
            );

            // Limit the rotation angle (optional)
            Vector3 eulerAngles = targetCamera.transform.eulerAngles;
            eulerAngles.x = Mathf.Clamp(eulerAngles.x, -maxRotationAngle, maxRotationAngle);
            eulerAngles.y = Mathf.Clamp(eulerAngles.y, -maxRotationAngle, maxRotationAngle);
            targetCamera.transform.eulerAngles = eulerAngles;
        }
    }

    public void SetRotationStrength(float strength)
    {
        rotationStrength = Mathf.Max(0f, strength);
    }
}
