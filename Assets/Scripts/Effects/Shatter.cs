using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shatter : MonoBehaviour
{
    float explosionForce = 20f;
    float explosionRadius = 10f;
    int gridX = 10;
    int gridY = 10;
    public float fadeOutDuration = 2f;
    public AudioClip shatterSound;
    public AudioSource audioSource;
    public Material SubMeshMaterial;
    private Mesh originalMesh;
    private Material originalMaterial;
    private Vector3[] originalVertices;
    MeshRenderer meshRenderer;

    public HashSet<GameObject> subMeshes = new();
    
    public void ShatterEffect()
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
        
        Material instancedMaterial = new Material(SubMeshMaterial);
        instancedMaterial.SetTexture("_Base_Texture", originalMaterial.GetTexture("_BaseMap"));
        for (int x = 0; x < gridX; x++)
        {
            for (int y = 0; y < gridY; y++)
            {
                CreateSubMesh(x, y, pieceWidth, pieceHeight, bounds.min, instancedMaterial);
            }
        }
    }

void CreateSubMesh(int x, int y, float pieceWidth, float pieceHeight, Vector3 boundsMin, Material instancedMaterial)
    {
        // Create a new GameObject for the sub-mesh
        GameObject subMeshObj = GameManager.instance.poolManager.GetSubMeshObject();
        SubMesh subMesh = subMeshObj.GetComponent<SubMesh>();
        Vector3 explosionCenter = transform.position + new Vector3(0, 0, -0.5f);
        subMesh.Initialize(transform, instancedMaterial, x, y, new Vector2Int(10, 10), pieceWidth, pieceHeight, boundsMin, explosionForce, explosionCenter, explosionRadius);

        //subMeshes.Add(subMeshObj);
    }

}
