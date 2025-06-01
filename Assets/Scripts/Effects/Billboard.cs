using UnityEngine;

public class Billboard : MonoBehaviour
{
    Vector3 cameraDir;

    void Update()
    {
        if (!Application.isPlaying) return;
        if (GameManager.instance)
        {
            if (GameManager.instance.cameraManager.isActiveAndEnabled)
            {
                if (GameManager.instance.cameraManager.mainCamera)
                {
                    cameraDir = GameManager.instance.cameraManager.mainCamera.transform.forward;
                    transform.rotation = Quaternion.LookRotation(cameraDir);
                }
            }
        }
        
    }
}
