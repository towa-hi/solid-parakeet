using PrimeTween;
using UnityEngine;

public class SpotLight : MonoBehaviour
{

    public Transform target;

    public Transform restTarget;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public void LookAt(Transform inTarget)
    {
        target = inTarget != null ? inTarget : restTarget;
        if (target)
        {
            Debug.Log($"looking at target {target.gameObject}");
        }
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
