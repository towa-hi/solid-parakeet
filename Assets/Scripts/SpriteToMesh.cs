using System;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class SpriteToMesh : MonoBehaviour
{
    [Tooltip("The sprite to convert into a mesh.")]
    public Sprite sprite;

    public void Activate(Sprite inSprite)
    {
        if (inSprite == null)
        {
            Debug.LogError("SpriteToMesh: No sprite assigned. Please assign a sprite in the Inspector.");
            return;
        }
        sprite = inSprite;
        // Get the MeshFilter component
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        // Convert the sprite to a mesh
        Mesh mesh = SpriteToMeshConverter(sprite);

        // Assign the mesh to the MeshFilter
        meshFilter.mesh = mesh;

        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider)
        {
            collider.sharedMesh = mesh;
        }
        // Get the MeshRenderer component
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        // Assign the sprite's texture to the material
        if (meshRenderer.sharedMaterial == null)
        {
            // Create a new material with a shader suitable for rendering textures
            meshRenderer.material = new Material(Shader.Find("Standard"));
        }

        // Assign the sprite's texture to the material
        meshRenderer.material.mainTexture = sprite.texture;
    }

    Mesh SpriteToMeshConverter(Sprite sprite)
    {
        Mesh mesh = new Mesh();

        // Convert sprite vertices to 3D space
        Vector3[] vertices = Array.ConvertAll(sprite.vertices, i => (Vector3)i);

        int[] triangles = Array.ConvertAll(sprite.triangles, i => (int)i);

        Vector2[] uvs = sprite.uv;

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
