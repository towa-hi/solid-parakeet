using System.Collections;
using UnityEngine;

public class Vortex : MonoBehaviour
{
    public Renderer vortexRenderer;
    public float vortexTransitionDuration = 0.25f;
    public Light directionalLight;
    public Light spotLight;
    public SpotLight spotLightHandler;
    static readonly int timeScaleID = Shader.PropertyToID("_TimeScale");
    static readonly int breatheFloorID = Shader.PropertyToID("_BreatheFloor");
    static readonly int breatheTimeID = Shader.PropertyToID("_BreatheTime");
    static readonly int breathePowerID = Shader.PropertyToID("_BreathePower");
    static readonly int twistednessID = Shader.PropertyToID("_Twistedness");
    Coroutine currentVortexLerp;
    
    public void StartVortex()
    {
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
        float initSpotLightIntensity = spotLight.intensity;
        //float initBreathePower = block.GetFloat(breathePowerID);
        float targetTwistedness = isOn ? 0f : 1f;
        float targetTimeScale = isOn? -1f : -0.1f;
        float targetDirectionalLightIntensity = isOn ? 1f : 2.5f;
        float targetSpotLightIntensity = isOn ? 40f : 0f;
       // float targetBreatheFloor = isOn? 4f : 1.88f;
        //float targetBreatheTime = isOn ? 5.3f : 2f;
        //float targetBreathePower = isOn ? 200f : 0.12f;
        
        float elapsedTime = 0f;
        while (elapsedTime < vortexTransitionDuration)
        {
            float t = elapsedTime / vortexTransitionDuration;
            float easedT = EaseInOutQuad(t);
            elapsedTime += Time.deltaTime;
            float currentTwistedness = Mathf.Lerp(initTwistedness, targetTwistedness, easedT);
            float currentTimeScale = Mathf.Lerp(initTimeScale, targetTimeScale, easedT);
            float currentDirectionalLightIntensity = Mathf.Lerp(initDirectionalLightIntensity, targetDirectionalLightIntensity, easedT);
            float currentSpotLightIntensity = Mathf.Lerp(initSpotLightIntensity, targetSpotLightIntensity, easedT);
            //float currentBreatheFloor = Mathf.Lerp(initBreatheFloor, targetBreatheFloor, delta);
            //float currentBreatheTime = Mathf.Lerp(initBreatheTime, targetBreatheTime, delta);
            //float currentBreathePower = Mathf.Lerp(initBreathePower, targetBreathePower, delta);
            block.SetFloat(timeScaleID, currentTimeScale);
            block.SetFloat(twistednessID, currentTwistedness);
            directionalLight.intensity = currentDirectionalLightIntensity;
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
    
    float EaseInOutQuad(float t) {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }
}
