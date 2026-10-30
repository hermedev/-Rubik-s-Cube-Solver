using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

[InitializeOnLoad]
public static class UpgradeToURP
{
    static UpgradeToURP()
    {
        EditorApplication.delayCall += ExecuteAutoUpgrade;
    }

    private static void ExecuteAutoUpgrade()
    {
        if (GraphicsSettings.defaultRenderPipeline == null)
        {
            SetupURP();
        }
    }

    [MenuItem("Tools/Setup URP Pipeline")]
    public static void SetupURP()
    {
        string settingsDir = "Assets/Settings";
        if (!Directory.Exists(settingsDir))
        {
            Directory.CreateDirectory(settingsDir);
            AssetDatabase.Refresh();
        }

        string rendererPath = settingsDir + "/UniversalRenderer.asset";
        string pipelineAssetPath = settingsDir + "/UniversalRenderPipelineAsset.asset";

        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, rendererPath);
        }

        UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelineAssetPath);
        if (urpAsset == null)
        {
            urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(urpAsset, pipelineAssetPath);
        }

        GraphicsSettings.defaultRenderPipeline = urpAsset;
        QualitySettings.renderPipeline = urpAsset;

        // Upgrade standard materials
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader != null)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            int convertedCount = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && (mat.shader.name == "Standard" || mat.shader.name == "Standard (Specular setup)"))
                {
                    Color col = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    float smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
                    float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;

                    mat.shader = urpLitShader;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

                    EditorUtility.SetDirty(mat);
                    convertedCount++;
                }
            }
            Debug.Log($"Converted {convertedCount} materials to URP Lit shader.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("URP Setup and Material upgrade completed successfully!");
    }
}
