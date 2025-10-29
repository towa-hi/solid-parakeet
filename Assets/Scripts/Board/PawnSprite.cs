using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PawnSprite : MonoBehaviour
{
    static readonly int BaseTexture = Shader.PropertyToID("_MainTex");
    static readonly Dictionary<Sprite, Mesh> MeshCache = new ();
    public Sprite currentSprite;
    public MeshFilter mf;
    public MeshRenderer mr;
    public Mesh mesh;
    public Sprite last;
    MaterialPropertyBlock props;
    public Shatter shatter;
    
    void Awake()  => InitMesh();
    void OnEnable() => InitMesh();

    void InitMesh()
    {
        if (mf == null) mf = GetComponent<MeshFilter>();
        if (mr == null) mr = GetComponent<MeshRenderer>();
        if (mesh == null)
        {
            mesh = new Mesh { name = "PawnSpriteMesh" };
            mf.sharedMesh = mesh;
        }
        if (props == null)
        {
            props = new MaterialPropertyBlock();
        }
    }

    public void SetSprite(Sprite sprite)
    {
        currentSprite = sprite;
        OnDidApplyAnimationProperties();
    }

    // Called at runtime _and_ when scrubbing animated properties in the Editor
    public void OnDidApplyAnimationProperties()
    {
        InitMesh();
        if (currentSprite == last || currentSprite == null) return;
        last = currentSprite;

        // Use cached mesh if available, otherwise build and cache a new one
        if (!MeshCache.TryGetValue(currentSprite, out Mesh cached))
        {
            cached = BuildDoubleSidedMesh(currentSprite);
            MeshCache[currentSprite] = cached;
        }
        mesh = cached;
        mf.sharedMesh = mesh;

        mr.GetPropertyBlock(props);
        props.SetTexture(BaseTexture, currentSprite.texture);
        mr.SetPropertyBlock(props);
    }

    static Mesh BuildDoubleSidedMesh(Sprite sprite)
    {
        Vector2[] v2 = sprite.vertices;
        int originalVertexCount = v2.Length;
        Vector3[] vertices = new Vector3[originalVertexCount * 2];
        for (int i = 0; i < originalVertexCount; i++)
        {
            vertices[i] = v2[i];
            vertices[i + originalVertexCount] = v2[i];
        }

        Vector2[] spriteUV = sprite.uv;
        Vector2[] uvs = new Vector2[originalVertexCount * 2];
        for (int i = 0; i < originalVertexCount; i++)
        {
            uvs[i] = spriteUV[i];
            uvs[i + originalVertexCount] = spriteUV[i];
        }

        int[] spriteTriangles = System.Array.ConvertAll(sprite.triangles, i => (int)i);
        int triangleCount = spriteTriangles.Length / 3;
        int[] triangles = new int[spriteTriangles.Length * 2];
        System.Array.Copy(spriteTriangles, 0, triangles, 0, spriteTriangles.Length);
        for (int t = 0; t < triangleCount; t++)
        {
            int a = spriteTriangles[t * 3 + 0];
            int b = spriteTriangles[t * 3 + 1];
            int c = spriteTriangles[t * 3 + 2];
            int baseIndex = spriteTriangles.Length + t * 3;
            triangles[baseIndex + 0] = a + originalVertexCount;
            triangles[baseIndex + 1] = c + originalVertexCount; // reverse
            triangles[baseIndex + 2] = b + originalVertexCount;
        }

        Vector3[] normals = new Vector3[originalVertexCount * 2];
        for (int i = 0; i < originalVertexCount; i++)
        {
            normals[i] = Vector3.back;      
            normals[i + originalVertexCount] = Vector3.forward;
        }

        Mesh m = new Mesh { name = "PawnSpriteMesh" };
        m.vertices = vertices;
        m.uv = uvs;
        m.normals = normals;
        m.triangles = triangles;
        m.RecalculateBounds();
        return m;
    }
    
}
