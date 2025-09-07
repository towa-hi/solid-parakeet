using System.Collections.Generic;
using UnityEngine;

public class CardSorting : MonoBehaviour
{
	public GameObject window;
    public List<GameObject> layers;

	// Allocator for unique stencil ids across cards (1..255)
	static readonly bool[] s_StencilInUse = new bool[256];
	const int kMinStencil = 1;
	const int kMaxStencil = 255;

	[SerializeField]
	[Range(kMinStencil, kMaxStencil)]
	int stencilRef;

	public int StencilRef => stencilRef;

	// Unique render queue bias per card
	static int s_NextQueueBiasIndex = 0;
	int queueBiasIndex = -1;
	const int kQueueStride = 20; // spacing between cards (used only for frames, if any)
	const int kMaskBaseQueue = 2950;
	const int kInnerBaseQueue = 3000;
	const int kFrameBaseQueue = 3050; // optional visible frame
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
		// If not assigned in inspector, auto-assign a unique id and apply
		if (stencilRef < kMinStencil || stencilRef > kMaxStencil)
		{
			AssignUniqueStencilRef();
		}
		ApplyStencilRefToAllLayers();
		AssignUniqueRenderQueues();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

	void OnDisable()
	{
		// Return id to pool
		if (stencilRef >= kMinStencil && stencilRef <= kMaxStencil)
		{
			s_StencilInUse[stencilRef] = false;
		}
	}

	// Public API: set a specific stencil ref on all layer renderers
	public void SetStencilRefForAllLayers(int value)
	{
		value = Mathf.Clamp(value, kMinStencil, kMaxStencil);
		// Release previous id if we owned it
		if (stencilRef >= kMinStencil && stencilRef <= kMaxStencil)
		{
			s_StencilInUse[stencilRef] = false;
		}
		stencilRef = value;
		s_StencilInUse[stencilRef] = true;
		ApplyStencilRefToAllLayers();
		AssignUniqueRenderQueues();
	}

	// Public API: assign a unique id (1..255) and apply to all layers
	public void AssignUniqueStencilRef()
	{
		int id = AcquireStencilId();
		Debug.Log($"stencilRef: {id}");
		stencilRef = id;
		ApplyStencilRefToAllLayers();
	}

	static int AcquireStencilId()
	{
		for (int i = kMinStencil; i <= kMaxStencil; i++)
		{
			if (!s_StencilInUse[i])
			{
				s_StencilInUse[i] = true;
				return i;
			}
		}
		// Fallback: reuse 1 if exhausted
		s_StencilInUse[kMinStencil] = true;
		return kMinStencil;
	}

	void ApplyStencilRefToAllLayers()
	{
		// Ensure window mask gets the same stencil ref
		if (window != null)
		{
			ApplyToRenderersInObject(window, stencilRef);
		}

		// If explicit layers list is empty, default to children of this GameObject
		if (layers == null || layers.Count == 0)
		{
			ApplyToRenderersInObject(gameObject, stencilRef);
			return;
		}

		foreach (var go in layers)
		{
			if (go == null) continue;
			ApplyToRenderersInObject(go, stencilRef);
		}
	}

	static void ApplyToRenderersInObject(GameObject root, int stencilValue)
	{
		var renderers = root.GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			var r = renderers[i];
			if (r == null) continue;
			var m = r.material; // ensure unique material per renderer
			m.SetFloat("_StencilRef", stencilValue);
		}
	}

	void AssignUniqueRenderQueues()
	{
		if (queueBiasIndex < 0)
		{
			queueBiasIndex = s_NextQueueBiasIndex++;
		}
		int bias = queueBiasIndex * kQueueStride;

		// Window/mask: draw before inners; depth decides top by Z. Keep same queue for all cards.
		if (window != null)
			ApplyRenderQueueToObject(window, kMaskBaseQueue);

		// Inners: draw after all masks. Use consistent queue across cards; only layer index offsets within the card.
		if (layers == null || layers.Count == 0)
		{
			ApplyRenderQueueToObject(gameObject, kInnerBaseQueue, true);
		}
		else
		{
			for (int i = 0; i < layers.Count; i++)
			{
				var go = layers[i];
				if (go == null) continue;
				ApplyRenderQueueToObject(go, kInnerBaseQueue + i);
			}
		}

		// If you later add a visible frame, you can bias frames per card with 'bias'.
	}

	static void ApplyRenderQueueToObject(GameObject root, int baseQueue, bool addIndexOffsets = false)
	{
		var renderers = root.GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			var r = renderers[i];
			if (r == null) continue;
			var m = r.material; // instanced
			m.renderQueue = addIndexOffsets ? baseQueue + i : baseQueue;
		}
	}
}
