using System.Collections;
using UnityEngine;

public static class FadeHelper
{
    public static void SetMaterialSurfaceType(Material mat, bool transparent)
    {
        if (transparent)
        {
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            mat.SetFloat("_Surface", 0f); // Opaque
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }
    }

    // Fade alpha con transizione morbida tra opaque e transparent
    public static IEnumerator FadeMaterialsAlpha(Material[] materials, float fromAlpha, float toAlpha, float duration, SkinnedMeshRenderer[] renderersToRefresh)
    {
        float elapsed = 0f;

        // Inizia sempre da surface transparent per poter variare alpha
        foreach (var mat in materials)
        {
            SetMaterialSurfaceType(mat, true);
            if (mat.HasProperty("_Color"))
            {
                Color c = mat.color;
                c.a = fromAlpha;
                mat.color = c;
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration);

            foreach (var mat in materials)
            {
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    c.a = alpha;
                    mat.color = c;
                }
            }

            // Cambia surface type solo vicino ai valori estremi alpha
            if (toAlpha < fromAlpha)
            {
                // Fade out: resta transparent finché alpha > 0.05, poi opaque
                if (alpha <= 0.05f)
                {
                    foreach (var mat in materials)
                    {
                        SetMaterialSurfaceType(mat, false);
                    }
                }
            }
            else
            {
                // Fade in: resta transparent finché alpha < 0.95, poi opaque
                if (alpha >= 0.95f)
                {
                    foreach (var mat in materials)
                    {
                        SetMaterialSurfaceType(mat, false);
                    }
                }
            }

            // Forza refresh renderer per evitare disallineamenti visivi
            foreach (var smr in renderersToRefresh)
            {
                if (smr != null)
                {
                    smr.enabled = false;
                    smr.enabled = true;
                }
            }

            yield return null;
        }

        // Assicura alpha finale
        foreach (var mat in materials)
        {
            if (mat.HasProperty("_Color"))
            {
                Color c = mat.color;
                c.a = toAlpha;
                mat.color = c;
            }
            // Assicura surface opaque a fine fade out o fade in
            SetMaterialSurfaceType(mat, false);
        }
    }

    // Versione ottimizzata per fade in (usa FadeMaterialsAlpha per fade out)
    public static IEnumerator FadeMaterialsAlphaFadeIn(Material[] materials, float duration, SkinnedMeshRenderer[] renderersToRefresh)
    {
        yield return FadeMaterialsAlpha(materials, 0f, 1f, duration, renderersToRefresh);
    }
}
