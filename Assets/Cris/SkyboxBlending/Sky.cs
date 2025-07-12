using UnityEngine;

public class SkyboxTransition : MonoBehaviour
{
    public Material skyboxRealWorld;
    public Material skyboxDreamWorld;
    public float transitionDuration = 5f;

    private float transitionTimer = 0f;
    public bool transitioning = false;

    void Start()
    {
        RenderSettings.skybox = skyboxRealWorld;
    }

    void Update()
    {
        if (transitioning)
        {
            transitionTimer += Time.deltaTime;
            float linearT = Mathf.Clamp01(transitionTimer / transitionDuration);
            float t = Mathf.SmoothStep(0f, 1f, linearT);

            if (skyboxRealWorld.HasProperty("_SkyTint") && skyboxDreamWorld.HasProperty("_SkyTint"))
            {
                Color dayColor = skyboxRealWorld.GetColor("_SkyTint");
                Color nightColor = skyboxDreamWorld.GetColor("_SkyTint");

                RenderSettings.skybox.SetColor("_SkyTint", Color.Lerp(dayColor, nightColor, t));
            }

            if (skyboxRealWorld.HasProperty("_Exposure") && skyboxDreamWorld.HasProperty("_Exposure"))
            {
                float dayExposure = skyboxRealWorld.GetFloat("_Exposure");
                float nightExposure = skyboxDreamWorld.GetFloat("_Exposure");

                RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(dayExposure, nightExposure, t));
            }

            DynamicGI.UpdateEnvironment();

            if (t >= 1f)
            {
                RenderSettings.skybox = skyboxDreamWorld;
                transitioning = false;
            }
        }
    }

    public void StartSkyboxTransition()
    {
        transitionTimer = 0f;
        transitioning = true;

        RenderSettings.skybox = new Material(skyboxRealWorld);
    }
}
