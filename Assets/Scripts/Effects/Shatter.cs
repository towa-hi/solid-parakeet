using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shatter : MonoBehaviour
{
    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int BaseTextureLegacy = Shader.PropertyToID("_Base_Texture");
    float explosionForce = 20f;
    float explosionRadius = 10f;
    int gridX = 5;
    int gridY = 5;
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
        float vol = AudioManager.EffectsScalar;
        audioSource.volume = 1f; // ensure base is unscaled; one-shot uses scale
        audioSource.PlayOneShot(shatterSound, vol);

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
        // Determine the correct base texture from the sprite or renderer
        Texture baseTex = null;
        var pawnSprite = GetComponent<PawnSprite>();
        if (pawnSprite != null && pawnSprite.currentSprite != null)
        {
            baseTex = pawnSprite.currentSprite.texture;
        }
        if (baseTex == null)
        {
            var mpb = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(mpb);
            baseTex = mpb.GetTexture(BaseMap);
            if (baseTex == null) baseTex = mpb.GetTexture(BaseTextureLegacy);
        }
        if (baseTex == null)
        {
            baseTex = originalMaterial.GetTexture(BaseMap);
            if (baseTex == null) baseTex = originalMaterial.GetTexture(BaseTextureLegacy);
            if (baseTex == null) baseTex = originalMaterial.mainTexture;
        }

        Material instancedMaterial = new Material(SubMeshMaterial);
        if (baseTex != null)
        {
            instancedMaterial.SetTexture(BaseMap, baseTex);
            instancedMaterial.SetTexture(BaseTextureLegacy, baseTex);
        }
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
        Rect uvRect = new Rect(0, 0, 1, 1);
        var pawnSprite = GetComponent<PawnSprite>();
        if (pawnSprite != null && pawnSprite.currentSprite != null)
        {
            // Use normalized rect directly to avoid precision mistakes
            uvRect = pawnSprite.currentSprite.textureRect;
            var tex = pawnSprite.currentSprite.texture;
            uvRect = new Rect(
                uvRect.x / tex.width,
                (tex.height - (uvRect.y + uvRect.height)) / tex.height, // convert to bottom-left origin
                uvRect.width / tex.width,
                uvRect.height / tex.height
            );
        }
        // Pass the original mesh so submesh can slice triangles directly
        subMesh.Initialize(transform, instancedMaterial, originalMesh, x, y, new Vector2Int(gridX, gridY), explosionForce, explosionCenter, explosionRadius, uvRect);

        //subMeshes.Add(subMeshObj);
    }

}
