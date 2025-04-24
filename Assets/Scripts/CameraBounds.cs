using UnityEngine;

public class CameraBounds : MonoBehaviour
{
    public Transform topRight;
    public Transform topLeft;
    public Transform bottomLeft;
    public Transform bottomRight;

    public GameObject testObject;
    bool screenPosBasedUpdate = true;
    public Vector3 targetPosition;
    private Vector3 velocity = Vector3.zero;
    public float smoothTime = 0.3f;
    void Update()
    {
        if (screenPosBasedUpdate)
        {
            ScreenPosBasedUpdate();
        }
        else
        {
            
        }
    }

    void ScreenPosBasedUpdate()
    {
        Vector2 mouseScreenPos = Globals.InputActions.Game.PointerPosition.ReadValue<Vector2>();
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);

        // Normalize mouse position (0 to 1)
        Vector2 normalized = new Vector2(mouseScreenPos.x / screenSize.x, mouseScreenPos.y / screenSize.y);

        // Lerp bottom edge between bottomLeft and bottomRight
        Vector3 bottomEdge = Vector3.Lerp(bottomLeft.position, bottomRight.position, normalized.x);
        // Lerp top edge between topLeft and topRight
        Vector3 topEdge = Vector3.Lerp(topLeft.position, topRight.position, normalized.x);

        // Lerp between those two based on normalized.y
        targetPosition = Vector3.Lerp(bottomEdge, topEdge, normalized.y);

        // SmoothDamp movement
        testObject.transform.position = Vector3.SmoothDamp(
            testObject.transform.position,
            targetPosition,
            ref velocity,
            smoothTime
        );        
    }
}
