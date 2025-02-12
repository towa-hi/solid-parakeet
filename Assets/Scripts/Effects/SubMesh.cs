using System.Collections;
using PrimeTween;
using UnityEngine;

public class SubMesh : MonoBehaviour
{
    public Rigidbody rigidBody;
    public MeshCollider meshCollider;
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;
    public float lifespan;
    
    Sequence decay;
    
    public void Initialize(Transform original, Material originalMaterial, int x, int y, Vector2Int grid, float spriteWidth, float spriteHeight, Vector3 boundsMin, float explosionForce, Vector3 explosionCenter, float explosionRadius)
    {
        transform.position = original.position;
        transform.rotation = original.rotation;
        transform.localScale = original.localScale;
        meshRenderer.material = originalMaterial;

        // MeshFilter and Mesh
        Mesh mesh = new Mesh
        {
            name = "Generated SubMesh",
        };
        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;

        // Define vertices in local space
        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(0, 0, 0);
        vertices[1] = new Vector3(0, spriteHeight, 0);
        vertices[2] = new Vector3(spriteWidth, spriteHeight, 0);
        vertices[3] = new Vector3(spriteWidth, 0, 0);
        // vertices[4] = vertices[0]; // Backside vertices
        // vertices[5] = vertices[1];
        // vertices[6] = vertices[2];
        // vertices[7] = vertices[3];

        // Adjust the sub-mesh object's position to the correct offset
        Vector3 offset = new(boundsMin.x + spriteWidth * x, boundsMin.y + spriteHeight * y, boundsMin.z);
        transform.position = transform.TransformPoint(offset);

        // Triangles (front + back)
        int[] triangles = new int[6]; // 6 front + 6 back
        // Front side
        triangles[0] = 0; triangles[1] = 1; triangles[2] = 2;
        triangles[3] = 2; triangles[4] = 3; triangles[5] = 0;
        // Backside (reverse winding order)
        // triangles[6] = 4; triangles[7] = 7; triangles[8] = 6;
        // triangles[9] = 6; triangles[10] = 5; triangles[11] = 4;

        // UVs
        Vector2[] uv = new Vector2[4];
        float uMin = (float)x / grid.x;
        float uMax = (float)(x + 1) / grid.x;
        float vMin = (float)y / grid.y;
        float vMax = (float)(y + 1) / grid.y;

        // Front side UVs
        uv[0] = new Vector2(uMin, vMin);
        uv[1] = new Vector2(uMin, vMax);
        uv[2] = new Vector2(uMax, vMax);
        uv[3] = new Vector2(uMax, vMin);
        // Backside UVs (make them match the front)
        // uv[4] = uv[0];
        // uv[5] = uv[1];
        // uv[6] = uv[2];
        // uv[7] = uv[3];

        // Assign to mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        meshCollider.sharedMesh = mesh;
        // rigidBody.isKinematic = true;
        // rigidBody.angularVelocity = Vector3.zero;
        // rigidBody.linearVelocity = Vector3.zero;
        rigidBody.AddExplosionForce(explosionForce, explosionCenter, explosionRadius);
        StartCoroutine(Decay());
    }

    IEnumerator Decay()
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(block);
        block.SetColor("_Base_Color", meshRenderer.material.GetColor("_Base_Color"));
        Color originalColor = block.GetColor("_Base_Color");
        meshRenderer.SetPropertyBlock(block);
        Debug.Log(originalColor);
        float startAlpha = originalColor.a;
        Debug.Log(startAlpha);
        float currentTime = 0f;
        while (currentTime < lifespan)
        {
            // TODO: figure out why this makes submeshes invisible
            // float alpha = Mathf.Lerp(startAlpha, 0f, currentTime / lifespan);
            // Color newColor = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            // block.SetColor("_Base_Color", newColor);
            // meshRenderer.SetPropertyBlock(block);
            // Debug.Log($"setting color to: {newColor}");
            currentTime += Time.deltaTime;
            yield return null;
        }
        GameManager.instance.poolManager.ReturnSubMeshObject(gameObject);
        meshRenderer.material.color = originalColor;
    }
}
