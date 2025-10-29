using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class ProceduralArrow : MonoBehaviour
{
    public float stemWidth;
    public float tipLength;
    public float tipWidth;
    public float arcHeight;
    public float arcDuration;
    // Static tip/tail mode removed; curved mesh builder is used for both static and animated
    public int curvedSegments = 16; // resolution of the curved mesh

    // Internal reusable buffers to minimize GC during mesh generation
    List<Vector3> centersList;
    float[] cumulativeBuffer;
    List<Vector3> normalsList;

    public List<Vector3> verticesList;
    public List<int> trianglesList;
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;
    Mesh mesh;

    // Track the currently running arc animation to cancel/replace cleanly
    Coroutine activeArcCoroutine;

    void Start()
    {
        mesh = new Mesh();
        mesh.MarkDynamic();
        meshFilter.mesh = mesh;
        // Preallocate reusable buffers to reduce allocations
        if (verticesList == null) verticesList = new List<Vector3>(64);
        if (trianglesList == null) trianglesList = new List<int>(128);
        centersList = new List<Vector3>(curvedSegments + 1);
        cumulativeBuffer = new float[curvedSegments + 1];
        normalsList = new List<Vector3>(64);
        
        // Auto-detect MeshRenderer if not set
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
    }
    // Update removed: static and animated arrows are built via BuildCurvedArrowMesh

    public void Clear()
    {
        if (activeArcCoroutine != null)
        {
            StopCoroutine(activeArcCoroutine);
            activeArcCoroutine = null;
        }
        if (mesh)
        {
            mesh.Clear();
        }
    }

    public void SetColor(Color color)
    {
        currentColor = color;
        
        // Ensure meshRenderer is initialized
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
        
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = color;
        }
    }

    public void PointToTarget(Transform start, Transform target)
    {
        if (!start || !target)
        {
            throw new ArgumentNullException();
        }
        // Unanimated arc (straight path with zero arc height), reuse curved mesh builder
        Clear();
        if (!mesh)
        {
            mesh = new Mesh();
            mesh.MarkDynamic();
            meshFilter.mesh = mesh;
        }
        
        // Ensure meshRenderer is initialized
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
        
        // Apply color
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = currentColor;
        }
        
        Vector3 startWorld = start.position;
        Vector3 endWorld = target.position;
        float baseY = startWorld.y;
        BuildCurvedArrowMesh(startWorld, endWorld, 1f, 0.15f, baseY);
    }

    public void ArcFromTiles(TileView fromTile, TileView toTile)
    {
        if (!fromTile || !toTile || !fromTile.origin || !toTile.origin)
        {
            return;
        }
        Clear();
        activeArcCoroutine = StartCoroutine(ArcFromToCoroutine(fromTile.origin, toTile.origin, arcDuration, arcHeight));
    }
    
    // Store the current color so it persists through animation
    private Color currentColor = Color.white;

    IEnumerator ArcFromToCoroutine(Transform from, Transform to, float duration, float arcHeight)
    {
        if (!mesh)
        {
            mesh = new Mesh();
            meshFilter.mesh = mesh;
        }
        
        // Ensure meshRenderer is initialized
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
        
        // Apply color at the start of animation
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = currentColor;
        }
        
        Vector3 startWorld = from.position;
        Vector3 endWorld = to.position;
        float baseY = startWorld.y;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            t = Shared.EaseOutQuad(t);

            BuildCurvedArrowMesh(startWorld, endWorld, t, arcHeight, baseY);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // finalize at t=1
        BuildCurvedArrowMesh(startWorld, endWorld, 1f, arcHeight, baseY);
        
        // Apply color after animation completes
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = currentColor;
        }
        
        activeArcCoroutine = null;
    }

    void BuildCurvedArrowMesh(Vector3 startWorld, Vector3 endWorld, float progress, float arcHeight, float baseY)
    {
        // progress in [0,1]: how far along the arc the tip currently is
        if (progress <= 0f)
        {
            if (mesh) mesh.Clear();
            return;
        }

        int segments = Mathf.Max(2, curvedSegments);
        // Sample the arc from u = 0..progress
        if (centersList.Capacity < segments + 1) centersList.Capacity = segments + 1;
        centersList.Clear();
        float invSegments = 1f / segments;
        for (int i = 0; i <= segments; i++)
        {
            float u = progress * (i * invSegments); // maps along traveled portion
            Vector3 p = Vector3.Lerp(startWorld, endWorld, u);
            float vOffset = arcHeight * (1f - Mathf.Pow(2f * u - 1f, 2f));
            p.y = baseY + vOffset; // parabolic vertical component
            centersList.Add(p);
        }

        // Estimate curve length and split stem vs tip by actual distance
        float totalLen = 0f;
        if (cumulativeBuffer == null || cumulativeBuffer.Length < centersList.Count)
        {
            cumulativeBuffer = new float[centersList.Count];
        }
        for (int i = 1; i < centersList.Count; i++)
        {
            totalLen += Vector3.Distance(centersList[i - 1], centersList[i]);
            cumulativeBuffer[i] = totalLen;
        }
        float stemLen = Mathf.Max(0f, totalLen - tipLength);
        int splitIndex = centersList.Count - 1;
        for (int i = 0; i < centersList.Count; i++)
        {
            if (cumulativeBuffer[i] >= stemLen)
            {
                splitIndex = i;
                break;
            }
        }

        verticesList.Clear();
        trianglesList.Clear();

        float halfStem = stemWidth * 0.5f;
        float halfTip = tipWidth * 0.5f;
        // Build stem strip up to splitIndex
        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
        for (int i = 0; i <= splitIndex; i++)
        {
            Vector3 tangent;
            if (i == 0) tangent = (centersList[1] - centersList[0]).normalized;
            else if (i == centersList.Count - 1) tangent = (centersList[i] - centersList[i - 1]).normalized;
            else tangent = (centersList[i + 1] - centersList[i - 1]).normalized;

            Vector3 right = Vector3.Cross(tangent, Vector3.up).normalized;
            if (right.sqrMagnitude < 1e-6f)
            {
                right = Vector3.right; // fallback
            }
            Vector3 leftPos = centersList[i] - right * halfStem;
            Vector3 rightPos = centersList[i] + right * halfStem;

            // convert to local
            leftPos = worldToLocal.MultiplyPoint3x4(leftPos);
            rightPos = worldToLocal.MultiplyPoint3x4(rightPos);
            verticesList.Add(leftPos);
            verticesList.Add(rightPos);
        }
        for (int i = 0; i < splitIndex; i++)
        {
            int li = 2 * i;
            int ri = 2 * i + 1;
            int ln = 2 * (i + 1);
            int rn = 2 * (i + 1) + 1;
            trianglesList.Add(li);
            trianglesList.Add(ri);
            trianglesList.Add(rn);
            trianglesList.Add(li);
            trianglesList.Add(rn);
            trianglesList.Add(ln);
        }

        // Tip triangle using separate base (tipWidth) at splitIndex and apex at current tip
        // Compute base orientation at splitIndex
        {
            Vector3 tangentBase;
            if (splitIndex == 0) tangentBase = (centersList[1] - centersList[0]).normalized;
            else if (splitIndex == centersList.Count - 1) tangentBase = (centersList[splitIndex] - centersList[splitIndex - 1]).normalized;
            else tangentBase = (centersList[splitIndex + 1] - centersList[splitIndex - 1]).normalized;
            Vector3 rightBase = Vector3.Cross(Vector3.up, tangentBase).normalized;
            if (rightBase.sqrMagnitude < 1e-6f) rightBase = Vector3.right;
            Vector3 baseCenterWorld = centersList[splitIndex];
            Vector3 baseLeftWorld = baseCenterWorld - rightBase * halfTip;
            Vector3 baseRightWorld = baseCenterWorld + rightBase * halfTip;
            Vector3 baseLeftLocal = worldToLocal.MultiplyPoint3x4(baseLeftWorld);
            Vector3 baseRightLocal = worldToLocal.MultiplyPoint3x4(baseRightWorld);

            Vector3 apexWorld = centersList[^1];
            Vector3 apexLocal = worldToLocal.MultiplyPoint3x4(apexWorld);
            int baseLeftIndex = verticesList.Count;
            verticesList.Add(baseLeftLocal);
            int apexIndex = verticesList.Count;
            verticesList.Add(apexLocal);
            int baseRightIndex = verticesList.Count;
            verticesList.Add(baseRightLocal);

            trianglesList.Add(baseLeftIndex);
            trianglesList.Add(apexIndex);
            trianglesList.Add(baseRightIndex);
        }

        // assign
        mesh.Clear();
        mesh.SetVertices(verticesList);
        mesh.SetTriangles(trianglesList, 0, true);
        EnsureNormalsSize(verticesList.Count);
        mesh.SetNormals(normalsList);
        mesh.bounds = ComputeBounds(verticesList);
    }

    void EnsureNormalsSize(int count)
    {
        if (normalsList.Capacity < count) normalsList.Capacity = count;
        normalsList.Clear();
        for (int i = 0; i < count; i++) normalsList.Add(Vector3.up);
    }

    static Bounds ComputeBounds(List<Vector3> verts)
    {
        if (verts == null || verts.Count == 0) return new Bounds(Vector3.zero, Vector3.zero);
        Vector3 min = verts[0];
        Vector3 max = verts[0];
        for (int i = 1; i < verts.Count; i++)
        {
            Vector3 v = verts[i];
            if (v.x < min.x) min.x = v.x; if (v.y < min.y) min.y = v.y; if (v.z < min.z) min.z = v.z;
            if (v.x > max.x) max.x = v.x; if (v.y > max.y) max.y = v.y; if (v.z > max.z) max.z = v.z;
        }
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = new Vector3(max.x - min.x, max.y - min.y, max.z - min.z);
        return new Bounds(center, size);
    }
}
