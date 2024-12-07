using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CameraAnchor : MonoBehaviour
{
    public Transform xGimbal;
    public float fov = 60;

    public Vector3 GetPosition()
    {
        return transform.position;
    }

    public Vector3 GetEuler()
    {
        return xGimbal.transform.eulerAngles;
    }

    public Quaternion GetQuaternion()
    {
        return xGimbal.rotation;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (Selection.activeTransform == transform || Selection.activeTransform == xGimbal)
        {
            DrawFrustum();
        }
    }

    void DrawFrustum()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("No Main Camera found in the scene.");
            return;
        }

        float aspect = mainCamera.aspect;
        float nearClip = mainCamera.nearClipPlane;
        float farClip = mainCamera.farClipPlane;

        // Calculate the frustum corners
        Matrix4x4 localToWorld = xGimbal.transform.localToWorldMatrix;

        float halfFov = fov * 0.5f * Mathf.Deg2Rad;
        float nearHeight = Mathf.Tan(halfFov) * nearClip;
        float nearWidth = nearHeight * aspect;
        float farHeight = Mathf.Tan(halfFov) * farClip;
        float farWidth = farHeight * aspect;

        Vector3 nearCenter = xGimbal.transform.position + xGimbal.transform.forward * nearClip;
        Vector3 farCenter = xGimbal.transform.position + xGimbal.transform.forward * farClip;

        Vector3[] corners = new Vector3[8];

        // Near plane corners
        corners[0] = localToWorld.MultiplyPoint(new Vector3(-nearWidth, -nearHeight, nearClip)); // Bottom left
        corners[1] = localToWorld.MultiplyPoint(new Vector3(nearWidth, -nearHeight, nearClip));  // Bottom right
        corners[2] = localToWorld.MultiplyPoint(new Vector3(-nearWidth, nearHeight, nearClip));  // Top left
        corners[3] = localToWorld.MultiplyPoint(new Vector3(nearWidth, nearHeight, nearClip));   // Top right

        // Far plane corners
        corners[4] = localToWorld.MultiplyPoint(new Vector3(-farWidth, -farHeight, farClip)); // Bottom left
        corners[5] = localToWorld.MultiplyPoint(new Vector3(farWidth, -farHeight, farClip));  // Bottom right
        corners[6] = localToWorld.MultiplyPoint(new Vector3(-farWidth, farHeight, farClip));  // Top left
        corners[7] = localToWorld.MultiplyPoint(new Vector3(farWidth, farHeight, farClip));   // Top right

        // Draw the edges of the frustum
        Gizmos.color = Color.cyan;

        // Draw near plane
        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[3]);
        Gizmos.DrawLine(corners[3], corners[2]);
        Gizmos.DrawLine(corners[2], corners[0]);

        // Draw far plane
        Gizmos.DrawLine(corners[4], corners[5]);
        Gizmos.DrawLine(corners[5], corners[7]);
        Gizmos.DrawLine(corners[7], corners[6]);
        Gizmos.DrawLine(corners[6], corners[4]);

        // Connect near and far planes
        Gizmos.DrawLine(corners[0], corners[4]);
        Gizmos.DrawLine(corners[1], corners[5]);
        Gizmos.DrawLine(corners[2], corners[6]);
        Gizmos.DrawLine(corners[3], corners[7]);
    }
#endif
}
