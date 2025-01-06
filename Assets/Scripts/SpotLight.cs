using PrimeTween;
using UnityEngine;

public class SpotLight : MonoBehaviour
{

    public Transform target;
    public Transform restTarget;

    public void LookAt(Transform inTarget)
    {
        target = inTarget != null ? inTarget : restTarget;
    }

    void Update()
    {
        if (target)
        {
            Vector3 forward = target.position - transform.position;
            transform.rotation = Quaternion.LookRotation(forward);
        }
    }
    
    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (target != null)
        {
            Vector3 myForward = target.position - transform.position;
            Gizmos.DrawCube(myForward, Vector3.one);
        }
        
    }
    
#endif
}
