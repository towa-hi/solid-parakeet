using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PawnSprite : MonoBehaviour
{
    static readonly int BaseTexture = Shader.PropertyToID("_BaseMap");
    public Sprite currentSprite;
    public MeshFilter mf;
    public MeshRenderer mr;
    public Mesh mesh;
    public Sprite last;
    MaterialPropertyBlock props;
    
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

    // Called at runtime _and_ when scrubbing animated properties in the Editor
    void OnDidApplyAnimationProperties()
    {
        InitMesh();
        if (currentSprite == last || currentSprite == null) return;
        last = currentSprite;

        // rebuild outline mesh from import-generated data
        Vector2[] v2 = currentSprite.vertices;
        Vector3[] v3 = new Vector3[v2.Length];
        for (int i = 0; i < v2.Length; i++) v3[i] = v2[i];

        mesh.Clear();
        mesh.vertices  = v3;
        mesh.triangles = System.Array.ConvertAll(currentSprite.triangles, i => (int)i);
        mesh.uv        = currentSprite.uv;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mr.GetPropertyBlock(props);
        props.SetTexture(BaseTexture, currentSprite.texture);
        mr.SetPropertyBlock(props);
    }
    
}
