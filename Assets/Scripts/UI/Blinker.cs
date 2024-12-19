using PrimeTween;
using TMPro;
using UnityEngine;

public class Blinker : MonoBehaviour
{
    public TextMeshProUGUI text;
    public float onDuration = 0.7f; // Time in seconds for the text to be visible
    public float offDuration = 0.3f; // Time in seconds for the text to be invisible
    private float timer;
    private bool isOn = true;

    void Update()
    {
        if (gameObject.activeInHierarchy && text != null)
        {
            if (gameObject.activeInHierarchy && text != null)
            {
                // Calculate alpha using a sine wave
                float alpha = 0.7f + 0.3f * Mathf.Sin(Time.time * Mathf.PI * 2); // Adjust the multiplier for speed if needed
            
                // Get the current color, set the alpha, and reassign it
                Color color = text.color;
                color.a = alpha;
                text.color = color;
            }
        }
    }
}
