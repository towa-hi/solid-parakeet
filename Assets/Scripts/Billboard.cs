using UnityEngine;

public class Billboard : MonoBehaviour
{
    Vector3 cameraDir;

    void Update()
    {
        cameraDir = GameManager.instance.cameraManager.mainCamera.transform.forward;
        transform.rotation = Quaternion.LookRotation(cameraDir);
    }
}
