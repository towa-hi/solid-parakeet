using System.Collections;
using UnityEngine;

public class Vortex : MonoBehaviour
{
    public Renderer vortexRenderer;
    public float vortexTransitionDuration = 0.25f;
    public Light directionalLight;
    public Color vortexOnColor = Color.white;
    public Color vortexOffColor = Color.white;
    public float vortexOnIntensity = 0.5f;
    public float vortexOffIntensity = 1f;
    public Light spotLight;
    public SpotLight spotLightHandler;
    static readonly int timeScaleID = Shader.PropertyToID("_TimeScale");
    static readonly int breatheFloorID = Shader.PropertyToID("_BreatheFloor");
    static readonly int breatheTimeID = Shader.PropertyToID("_BreatheTime");
    static readonly int breathePowerID = Shader.PropertyToID("_BreathePower");
    static readonly int twistednessID = Shader.PropertyToID("_Twistedness");
    Coroutine currentVortexLerp;
    bool vortexOn;
    
    // Reset all vortex-related visual state to the non-resolve baseline immediately
    public void ResetAll()
    {
        if (currentVortexLerp != null)
        {
            StopCoroutine(currentVortexLerp);
            currentVortexLerp = null;
        }
        vortexOn = false;
        var block = new MaterialPropertyBlock();
        if (vortexRenderer != null)
        {
            vortexRenderer.GetPropertyBlock(block);
            // Baseline (vortex off) values should match LerpVortex(false) targets
            block.SetFloat(timeScaleID, -0.1f);
            block.SetFloat(twistednessID, 1f);
            //block.SetFloat(breatheFloorID, 1.88f);
            //block.SetFloat(breatheTimeID, 2f);
            //block.SetFloat(breathePowerID, 0.12f);
            vortexRenderer.SetPropertyBlock(block);
        }
        if (directionalLight != null)
        {
            directionalLight.intensity = vortexOffIntensity;
            directionalLight.color = vortexOffColor;
        }
        if (spotLight != null)
        {
            spotLight.intensity = 0f;
        }
    }
    
    public void StartVortex()
    {
        if (vortexOn) return;
        vortexOn = true;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        vortexRenderer.GetPropertyBlock(block);
        float currentTimeScale = block.GetFloat(timeScaleID);
        Debug.Log($"StartVortex started timescale at {currentTimeScale}");
        if (currentVortexLerp != null)
        {
            StopCoroutine(currentVortexLerp);
        }
        currentVortexLerp = StartCoroutine(LerpVortex(true));
    }
    
    public void EndVortex()
    {
        if (!vortexOn) return;
        vortexOn = false;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        vortexRenderer.GetPropertyBlock(block);
        float currentTimeScale = block.GetFloat(timeScaleID);
        Debug.Log($"EndVortex started timescale at {currentTimeScale}");
        Debug.Log("EndVortex");
        if (currentVortexLerp != null)
        {
            StopCoroutine(currentVortexLerp);
        }
        currentVortexLerp = StartCoroutine(LerpVortex(false));
    }
    
    IEnumerator LerpVortex(bool isOn)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        vortexRenderer.GetPropertyBlock(block);
        float initTwistedness = block.GetFloat(twistednessID);
        float initTimeScale = block.GetFloat(timeScaleID);
        float initBreatheFloor = block.GetFloat(breatheFloorID);
        float initBreatheTime = block.GetFloat(breatheTimeID);
        float initDirectionalLightIntensity = directionalLight.intensity;
        Color initDirectionalLightColor = directionalLight.color;
        float initSpotLightIntensity = spotLight.intensity;
        //float initBreathePower = block.GetFloat(breathePowerID);
        float targetTwistedness = isOn ? 0f : 1f;
        float targetTimeScale = isOn? -1f : -0.1f;
        float targetDirectionalLightIntensity = isOn ? vortexOnIntensity : vortexOffIntensity;
        Color targetDirectionalLightColor = isOn ? vortexOnColor : vortexOffColor;
        float targetSpotLightIntensity = isOn ? 40f : 0f;
       // float targetBreatheFloor = isOn? 4f : 1.88f;
        //float targetBreatheTime = isOn ? 5.3f : 2f;
        //float targetBreathePower = isOn ? 200f : 0.12f;
        
        float elapsedTime = 0f;
        while (elapsedTime < vortexTransitionDuration)
        {
            float t = elapsedTime / vortexTransitionDuration;
            float easedT = Shared.EaseInOutQuad(t);
            elapsedTime += Time.deltaTime;
            float currentTwistedness = Mathf.Lerp(initTwistedness, targetTwistedness, easedT);
            float currentTimeScale = Mathf.Lerp(initTimeScale, targetTimeScale, easedT);
            float currentDirectionalLightIntensity = Mathf.Lerp(initDirectionalLightIntensity, targetDirectionalLightIntensity, easedT);
            Color currentDirectionalLightColor = Color.Lerp(initDirectionalLightColor, targetDirectionalLightColor, easedT);
            float currentSpotLightIntensity = Mathf.Lerp(initSpotLightIntensity, targetSpotLightIntensity, easedT);
            //float currentBreatheFloor = Mathf.Lerp(initBreatheFloor, targetBreatheFloor, delta);
            //float currentBreatheTime = Mathf.Lerp(initBreatheTime, targetBreatheTime, delta);
            //float currentBreathePower = Mathf.Lerp(initBreathePower, targetBreathePower, delta);
            block.SetFloat(timeScaleID, currentTimeScale);
            block.SetFloat(twistednessID, currentTwistedness);
            directionalLight.intensity = currentDirectionalLightIntensity;
            directionalLight.color = currentDirectionalLightColor;
            spotLight.intensity = currentSpotLightIntensity;
            //currentBlock.SetFloat(breatheFloorID, currentBreatheFloor);
            //currentBlock.SetFloat(breatheTimeID, currentBreatheTime);
            //block.SetFloat(breathePowerID, currentBreathePower);
            vortexRenderer.SetPropertyBlock(block);
            yield return null;
        }
        //block.SetFloat(breatheFloorID, targetBreatheFloor);
        //block.SetFloat(breatheTimeID, targetBreatheTime);
        //block.SetFloat(breathePowerID, targetBreathePower);
        
    }
    
    
}
