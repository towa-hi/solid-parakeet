using UnityEngine;

public class RayCasterTest : MonoBehaviour
{
    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * 100, Color.green);
        var hits = Physics.RaycastAll(ray, 1000f);
        Debug.Log("RayTest hits " + hits.Length);
    }
}
