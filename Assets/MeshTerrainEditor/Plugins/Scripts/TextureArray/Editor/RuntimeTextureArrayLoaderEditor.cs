#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class RuntimeTextureArrayLoaderEditor : Editor
{
    // Qui va il tuo codice originale della classe

    public override void OnInspectorGUI()
    {
        // Esempio di override GUI custom
        DrawDefaultInspector();

        if (GUILayout.Button("Carica Texture Array"))
        {
            // Azioni personalizzate da editor
            Debug.Log("Texture Array caricata");
        }
    }

    // Altri metodi e logica editor specifica
}
#endif
