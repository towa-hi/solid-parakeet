using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Camera camera;
    Vector3 cameraDir;

    void Start()
    {
        if (!camera)
        {
            camera = GameManager.instance.cameraManager.mainCamera;
        }
    }
    void Update()
    {
        if (!Application.isPlaying) return;
        cameraDir = camera.transform.forward;
        transform.rotation = Quaternion.LookRotation(cameraDir);
    }
}
