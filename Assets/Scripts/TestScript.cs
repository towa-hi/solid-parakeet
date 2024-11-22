using UnityEngine;

public class TestScript : MonoBehaviour
{
    private Renderer objectRenderer;
    private Color originalColor;
    private Color targetColor;
    private float colorChangeSpeed = 2f;
    private bool isMouseOver = false;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalColor = objectRenderer.material.color;
            targetColor = originalColor;
        }
        else
        {
            Debug.LogWarning("No Renderer found on this game object.");
        }
    }

    void OnMouseEnter()
    {
        if (objectRenderer != null)
        {
            targetColor = Color.blue;
            isMouseOver = true;
        }
    }

    void OnMouseExit()
    {
        if (objectRenderer != null)
        {
            targetColor = originalColor;
            isMouseOver = false;
        }
    }

    void Update()
    {
        if (objectRenderer != null)
        {
            // Smoothly interpolate the color
            objectRenderer.material.color = Color.Lerp(objectRenderer.material.color, targetColor, colorChangeSpeed * Time.deltaTime);
        }
    }
}
