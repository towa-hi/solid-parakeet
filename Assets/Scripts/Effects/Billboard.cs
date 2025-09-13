using UnityEngine;

public class Billboard : MonoBehaviour
{
#pragma warning disable CS0108, CS0114
    public Camera camera;
#pragma warning restore CS0108, CS0114
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
