using System.Collections;
using PrimeTween;
using UnityEngine;

public class SubMesh : MonoBehaviour
{
    static readonly int Color1 = Shader.PropertyToID("_Color");
    public Rigidbody rigidBody;
    public MeshCollider meshCollider;
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;
    public float lifespan;
    
    Sequence decay;
    
    public void Initialize(Transform original, Material originalMaterial, Mesh sourceMesh, int x, int y, Vector2Int grid, float explosionForce, Vector3 explosionCenter, float explosionRadius, Rect uvRect)
    {
        transform.position = original.position;
        transform.rotation = original.rotation;
        // Match world scale (final scale) of original, compensating for our parent's scale
        Vector3 originalWorldScale = original.lossyScale;
        Vector3 parentWorldScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        Vector3 safeParentScale = new Vector3(
            parentWorldScale.x == 0f ? 1f : parentWorldScale.x,
            parentWorldScale.y == 0f ? 1f : parentWorldScale.y,
            parentWorldScale.z == 0f ? 1f : parentWorldScale.z
        );
        transform.localScale = new Vector3(
            originalWorldScale.x / safeParentScale.x,
            originalWorldScale.y / safeParentScale.y,
            originalWorldScale.z / safeParentScale.z
        );
        transform.name = $"SubMesh[{x},{y}]";
        meshRenderer.material = originalMaterial;

        // Build a simple quad sized by the original mesh's bounds and positioned per grid cell
        Bounds srcBounds = sourceMesh.bounds;
        float pieceWidth = srcBounds.size.x / grid.x;
        float pieceHeight = srcBounds.size.y / grid.y;
        // Geometry uses original orientation; we'll flip V in UV mapping instead

        // Define vertices in original local space, offset per grid cell
        Vector3 offset = new Vector3(srcBounds.min.x + pieceWidth * x, srcBounds.min.y + pieceHeight * y, srcBounds.min.z);
        Vector3[] vertices = new Vector3[8];
        // Front
        vertices[0] = new Vector3(offset.x, offset.y, offset.z);
        vertices[1] = new Vector3(offset.x, offset.y + pieceHeight, offset.z);
        vertices[2] = new Vector3(offset.x + pieceWidth, offset.y + pieceHeight, offset.z);
        vertices[3] = new Vector3(offset.x + pieceWidth, offset.y, offset.z);
        // Back (duplicate positions)
        vertices[4] = vertices[0];
        vertices[5] = vertices[1];
        vertices[6] = vertices[2];
        vertices[7] = vertices[3];

        // Triangles
        int[] triangles = new int[12];
        // Front
        triangles[0] = 0; triangles[1] = 1; triangles[2] = 2;
        triangles[3] = 2; triangles[4] = 3; triangles[5] = 0;
        // Back (reverse winding)
        triangles[6] = 4; triangles[7] = 7; triangles[8] = 6;
        triangles[9] = 6; triangles[10] = 5; triangles[11] = 4;

        // UVs with V flipped inside uvRect to keep textures upright
        Vector2[] uv = new Vector2[8];
        float cellUMin = (float)x / grid.x;
        float cellUMax = (float)(x + 1) / grid.x;
        float cellVMin = (float)y / grid.y;
        float cellVMax = (float)(y + 1) / grid.y;

        float uMin = uvRect.x + cellUMin * uvRect.width;
        float uMax = uvRect.x + cellUMax * uvRect.width;
        float vMin = uvRect.y + cellVMin * uvRect.height;
        float vMax = uvRect.y + cellVMax * uvRect.height;

        // Front
        uv[0] = new Vector2(uMin, vMin); // bottom-left
        uv[1] = new Vector2(uMin, vMax); // top-left
        uv[2] = new Vector2(uMax, vMax); // top-right
        uv[3] = new Vector2(uMax, vMin); // bottom-right
        // Back (match front)
        uv[4] = uv[0];
        uv[5] = uv[1];
        uv[6] = uv[2];
        uv[7] = uv[3];

        // Explicit normals to ensure proper lighting for both sides
        Vector3[] normals = new Vector3[8];
        normals[0] = Vector3.back; // front face
        normals[1] = Vector3.back;
        normals[2] = Vector3.back;
        normals[3] = Vector3.back;
        normals[4] = Vector3.forward; // back face
        normals[5] = Vector3.forward;
        normals[6] = Vector3.forward;
        normals[7] = Vector3.forward;

        Mesh mesh = new Mesh { name = "Generated SubMesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;
        // rigidBody.isKinematic = true;
        // rigidBody.angularVelocity = Vector3.zero;
        // rigidBody.linearVelocity = Vector3.zero;
        rigidBody.AddExplosionForce(explosionForce, explosionCenter, explosionRadius);
        //StartCoroutine(Decay());
    }

    IEnumerator Decay()
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(block);
        block.SetColor(Color1, meshRenderer.material.GetColor(Color1));
        Color originalColor = block.GetColor(Color1);
        meshRenderer.SetPropertyBlock(block);
        //Debug.Log(originalColor);
        float startAlpha = originalColor.a;
        //Debug.Log(startAlpha);
        float currentTime = 0f;
        while (currentTime < lifespan)
        {
            // TODO: figure out why this makes submeshes invisible
            float alpha = Mathf.Lerp(startAlpha, 0f, currentTime / lifespan);
            Color newColor = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            block.SetColor(Color1, newColor);
            meshRenderer.SetPropertyBlock(block);
            // Debug.Log($"setting color to: {newColor}");
            currentTime += Time.deltaTime;
            yield return null;
        }
        GameManager.instance.poolManager.ReturnSubMeshObject(gameObject);
        meshRenderer.material.color = originalColor;
    }
}
