using UnityEngine;

public class Billboard : MonoBehaviour
{
    Vector3 cameraDir;

    void Update()
    {
        if (!Application.isPlaying) return;
        cameraDir = GameManager.instance.cameraManager.mainCamera.transform.forward;
        transform.rotation = Quaternion.LookRotation(cameraDir);
    }
}
