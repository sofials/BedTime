using UnityEngine;
using UnityEditor;

/// <summary>
/// Utility per configurare automaticamente il materiale Ghost Occluded con le impostazioni corrette
/// </summary>
public class GhostMaterialSetup : EditorWindow
{
    private Material targetMaterial;
    private Texture2D baseTexture;
    private Color baseColor = new Color(1f, 1f, 1f, 0.4f); // Bianco semi-trasparente (A=100/255)
    private Color emissionColor = new Color(0f, 1f, 1f, 1f); // Cyan brillante
    private float emissionIntensity = 2.0f;

    [MenuItem("Tools/Setup Ghost Occluded Material")]
    public static void ShowWindow()
    {
        GetWindow<GhostMaterialSetup>("Ghost Material Setup");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Ghost Occluded Material Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Questo tool configura automaticamente un materiale per il ghost quando è dentro oggetti.\n" +
            "Imposta: Transparent, ZWrite OFF, Render Queue, Emission, ecc.",
            MessageType.Info);

        EditorGUILayout.Space();

        // Material selection
        targetMaterial = (Material)EditorGUILayout.ObjectField(
            "Materiale da Configurare",
            targetMaterial,
            typeof(Material),
            false);

        if (targetMaterial == null)
        {
            EditorGUILayout.HelpBox("Seleziona un materiale da configurare", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();

        // Texture (optional)
        baseTexture = (Texture2D)EditorGUILayout.ObjectField(
            "Base Texture (opzionale)",
            baseTexture,
            typeof(Texture2D),
            false);

        // Colors
        baseColor = EditorGUILayout.ColorField("Base Color (con Alpha)", baseColor);
        emissionColor = EditorGUILayout.ColorField("Emission Color", emissionColor);
        emissionIntensity = EditorGUILayout.Slider("Emission Intensity", emissionIntensity, 0f, 5f);

        EditorGUILayout.Space();

        // Preview info
        EditorGUILayout.LabelField("Configurazione:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"• Surface Type: Transparent");
        EditorGUILayout.LabelField($"• Render Face: Both");
        EditorGUILayout.LabelField($"• ZWrite: OFF");
        EditorGUILayout.LabelField($"• ZTest: Always (visibile attraverso muri)");
        EditorGUILayout.LabelField($"• Render Queue: Overlay (4000)");
        EditorGUILayout.LabelField($"• Emission: ON");

        EditorGUILayout.Space();

        // Apply button
        if (GUILayout.Button("Applica Configurazione", GUILayout.Height(40)))
        {
            ApplyMaterialSettings();
        }
    }

    private void ApplyMaterialSettings()
    {
        if (targetMaterial == null)
        {
            EditorUtility.DisplayDialog("Errore", "Seleziona un materiale!", "OK");
            return;
        }

        Undo.RecordObject(targetMaterial, "Setup Ghost Occluded Material");

        // Determina se è URP o Built-in
        string shaderName = targetMaterial.shader.name;
        bool isURP = shaderName.Contains("Universal Render Pipeline") || shaderName.Contains("URP");

        if (isURP)
        {
            SetupURPMaterial();
        }
        else
        {
            SetupStandardMaterial();
        }

        EditorUtility.SetDirty(targetMaterial);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Successo!",
            $"Materiale '{targetMaterial.name}' configurato correttamente!\n\n" +
            "Impostazioni applicate:\n" +
            "• Transparent\n" +
            "• ZWrite OFF\n" +
            "• ZTest Always (visibile attraverso muri)\n" +
            "• Render Queue: Overlay (4000)\n" +
            "• Emission: ON\n" +
            "• Both Faces visibili",
            "OK");
    }

    private void SetupURPMaterial()
    {
        // ⚠️ CONFIGURAZIONE PER URP/Unlit SHADER (più semplice e diretto per ghost)

        // Cull: OFF (Both faces)
        targetMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        // ZWrite: OFF (non scrive nel depth buffer)
        targetMaterial.SetFloat("_ZWrite", 0);

        // ZTest: Always (visibile anche dietro i muri)
        targetMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

        // Render Queue: Overlay (sopra tutto)
        targetMaterial.renderQueue = 4000;

        // Base texture (Unlit usa _BaseMap)
        if (baseTexture != null)
        {
            targetMaterial.SetTexture("_BaseMap", baseTexture);
        }

        // Base color con alpha
        targetMaterial.SetColor("_BaseColor", baseColor);

        // Emission (in Unlit è solo un colore additivo)
        targetMaterial.EnableKeyword("_EMISSION");
        targetMaterial.SetColor("_EmissionColor", emissionColor * emissionIntensity);

        Debug.Log($"[GhostMaterialSetup] Materiale URP/Unlit configurato: {targetMaterial.name}");
        Debug.Log($"  ✅ ZTest: Always | ZWrite: OFF | RenderQueue: 4000 | Cull: OFF");
    }

    private void SetupStandardMaterial()
    {
        // Rendering Mode: Transparent
        targetMaterial.SetFloat("_Mode", 3); // 0=Opaque, 1=Cutout, 2=Fade, 3=Transparent
        targetMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        targetMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        // ZWrite: OFF
        targetMaterial.SetInt("_ZWrite", 0);

        // ZTest: Always (rende visibile anche dietro oggetti)
        targetMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

        // Cull: OFF (Both faces)
        targetMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        // Render Queue: Overlay (4000) per renderizzare sopra tutto
        targetMaterial.renderQueue = 4000; // Overlay queue

        // Base Map & Color
        if (baseTexture != null)
        {
            targetMaterial.SetTexture("_MainTex", baseTexture);
        }
        targetMaterial.SetColor("_Color", baseColor);

        // Metallic & Smoothness
        targetMaterial.SetFloat("_Metallic", 0f);
        targetMaterial.SetFloat("_Glossiness", 0.3f);

        // Emission
        targetMaterial.EnableKeyword("_EMISSION");
        targetMaterial.SetColor("_EmissionColor", emissionColor * emissionIntensity);

        // Enable transparency keywords
        targetMaterial.EnableKeyword("_ALPHABLEND_ON");
        targetMaterial.DisableKeyword("_ALPHATEST_ON");
        targetMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        Debug.Log($"[GhostMaterialSetup] Materiale Standard configurato: {targetMaterial.name}");
    }
}
