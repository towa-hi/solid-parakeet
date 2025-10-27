using UnityEngine;

public class TileModel : MonoBehaviour
{
    public MeshCollider hitbox;
    public Renderer topRenderer;
    public RenderEffect renderEffect;
    public Renderer flatRenderer;
    public Transform tileOrigin;
    public Transform elevator;

    public GameObject fogObject;
    public ParticleSystem fogParticleSystem;
    public TooltipElement tooltipElement;
}
