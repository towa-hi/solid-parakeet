using System.Collections;
using UnityEngine;

public class Shatter : MonoBehaviour
{
    // Remove originalSprite since we're using MeshFilter
    //public Sprite originalSprite;

    float explosionRadius = 10f;
    int gridX = 10;
    int gridY = 10;
    public float fadeOutDuration = 2f;
    public AudioClip shatterSound;
    public AudioSource audioSource;

    private Mesh originalMesh;
    private Material originalMaterial;
    private Vector3[] originalVertices;
    MeshRenderer meshRenderer;
    public void ShatterEffect(float force)
    {
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(shatterSound);

        // Get the original mesh and material
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        originalMesh = meshFilter.mesh;
        originalMaterial = meshRenderer.material;
        originalVertices = originalMesh.vertices;

        Subdivide();
        ApplyExplosionForce(force);

        // Optionally disable the original mesh
        meshRenderer.enabled = false;
        GetComponent<MeshCollider>().enabled = false;
    }

    void Subdivide()
    {
        // Get the bounds of the original mesh
        Bounds bounds = originalMesh.bounds;

        // Calculate piece sizes
        float pieceWidth = bounds.size.x / gridX;
        float pieceHeight = bounds.size.y / gridY;

        for (int x = 0; x < gridX; x++)
        {
            for (int y = 0; y < gridY; y++)
            {
                CreateSubMesh(x, y, pieceWidth, pieceHeight, bounds.min);
            }
        }
    }

void CreateSubMesh(int x, int y, float pieceWidth, float pieceHeight, Vector3 boundsMin)
    {
        // Create a new GameObject for the sub-mesh
        GameObject subMeshObj = Instantiate(new GameObject($"SubMesh_{x}_{y}"), this.transform);
        subMeshObj.transform.position = transform.position;
        subMeshObj.transform.rotation = transform.rotation;
        subMeshObj.transform.localScale = transform.localScale;

        // Add components
        Rigidbody rigidBody = subMeshObj.AddComponent<Rigidbody>();
        rigidBody.mass = 0.1f;
        MeshCollider meshCollider = subMeshObj.AddComponent<MeshCollider>();
        meshCollider.convex = true;

        // MeshRenderer and Material
        MeshRenderer meshRenderer = subMeshObj.AddComponent<MeshRenderer>();

        // Instantiate the material to avoid shared properties
        Material materialInstance = new Material(originalMaterial);
        materialInstance.SetFloat("_Fade", 1f); // Start fully opaque
        meshRenderer.material = materialInstance;

        // MeshFilter and Mesh
        MeshFilter meshFilter = subMeshObj.AddComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;

        // Define vertices in local space
        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(0, 0, 0);
        vertices[1] = new Vector3(0, pieceHeight, 0);
        vertices[2] = new Vector3(pieceWidth, pieceHeight, 0);
        vertices[3] = new Vector3(pieceWidth, 0, 0);

        // Adjust the sub-mesh object's position to the correct offset
        Vector3 pieceOffset = new Vector3(boundsMin.x + pieceWidth * x, boundsMin.y + pieceHeight * y, boundsMin.z);
        subMeshObj.transform.position = transform.TransformPoint(pieceOffset);

        // Triangles
        int[] triangles = { 0, 1, 2, 2, 3, 0 };

        // UVs
        Vector2[] uv = new Vector2[4];
        float uMin = (float)x / gridX;
        float uMax = (float)(x + 1) / gridX;
        float vMin = (float)y / gridY;
        float vMax = (float)(y + 1) / gridY;

        uv[0] = new Vector2(uMin, vMin);
        uv[1] = new Vector2(uMin, vMax);
        uv[2] = new Vector2(uMax, vMax);
        uv[3] = new Vector2(uMax, vMin);

        // Assign to mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        meshCollider.sharedMesh = mesh;

        StartCoroutine(FadeOut(subMeshObj, fadeOutDuration)); // Start the fade-out coroutine
    }


    IEnumerator FadeOut(GameObject obj, float duration)
    {
        Material mat = obj.GetComponent<MeshRenderer>().material;
        Color originalColor = mat.color;
        float currentTime = 0;

        while (currentTime < duration)
        {
            float alpha = Mathf.Lerp(1f, 0f, currentTime / duration);
            Color newColor = originalColor;
            newColor.a = alpha;
            mat.color = newColor;

            currentTime += Time.deltaTime;
            yield return null;
        }
        
        Destroy(obj); // Destroy the object after fading out
        meshRenderer.enabled = true;
    }

    void ApplyExplosionForce(float force)
    {
        foreach (Transform child in transform) // Adjusted to parent to find all submeshes
        {
            if (child.gameObject.name.StartsWith("SubMesh"))
            {
                Rigidbody rb = child.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 explosionPosition = transform.position + new Vector3(0, 0, -0.5f);
                    rb.AddExplosionForce(force, explosionPosition, explosionRadius);
                }
            }
        }
    }
}
