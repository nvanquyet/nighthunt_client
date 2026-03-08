#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.Rendering;

public class PR_Convert_to_Pipeline : ScriptableWizard
{
    public enum Pipe
    {
        Standard, URP, HDRP
    }

    public Pipe target_pipeline = Pipe.Standard;

    public Material[] materials_to_convert; //To rename

    [HideInInspector]
    public RenderPipelineAsset universal_render_pipeline_asset;
    [HideInInspector]
    public RenderPipelineAsset HD_render_pipeline_asset;

    bool init = false;

    [MenuItem("PolygonR/Convert to Pipeline...")]
    static void CreateWizard()
    {
        DisplayWizard<PR_Convert_to_Pipeline>("PolygonR : Convert to Pipepline", "Convert");
    }

    void OnWizardUpdate()
    {
        if (init == false && GraphicsSettings.defaultRenderPipeline)
        {
            if (GraphicsSettings.defaultRenderPipeline.name == "LightweightRenderPipelineAsset")
            {
                target_pipeline = Pipe.URP;
            }
            else if (GraphicsSettings.defaultRenderPipeline.name == "HDRenderPipelineAsset")
            {
                target_pipeline = Pipe.HDRP;
            }
            init = true;
        }
       
    }

    void OnWizardCreate()
    {
        RenameAssets();
    }

    void ReplaceShaders(string shader_name, Material mat)
    {
        string[] universal_render_pipeline_shaders = new string[]
        {
            "Universal Render Pipeline/Lit", //Basic shader
            "Shader Graphs/PR_LWRP_Cha_PBR_Multiply", //Characters and weapons
            "Shader Graphs/PR_LWRP_Env_PBR_WorldUV", //Floors
            "PolygonR/Unlit HUD Shader", //Aiming
            "Universal Render Pipeline/Particles/Simple Lit", //Smokes
            "PolygonR/Particles Additive +", //Additive FX
            "Shader Graphs/PR_LWRP_Par_Add", //Additive particles
            "Legacy Shaders/Particles/Alpha Blended Premultiply", //alpha Blended particles
            "PolygonR/Laser +",// Lasers
            "Universal Render Pipeline/Particles/Unlit", //Distortion particles
            "PolygonR/TurretArea" //Area drawing
        };

        string[] standard_shaders_names = new string[]
        {
            "Standard", //Basic shader
            "PolygonR/PBRMetalRough_Multiply", //Characters and weapons
            "PolygonR/PBR World UV", //Floors
            "PolygonR/Unlit HUD Shader", //Aiming
            "PolygonR/Particles Smoke", //Smokes
            "PolygonR/Particles Additive +", //Additive FX
            "Mobile/Particles/Additive", //Additive particles
            "Legacy Shaders/Particles/Alpha Blended Premultiply",  //alpha Blended particles
            "PolygonR/Laser +", // Lasers
            "PolygonR/Particles Distort", //Distortion particles
            "PolygonR/TurretArea" //Area Drawing
        };

        string[] high_resolution_shaders_names = new string[]
        {
            "HDRP/Lit", //Basic shader
            "Shader Graphs/PR_HDRP_Cha_PBR_Multiply", //Characters and weapons
            "Shader Graphs/PR_HDRP_Env_PBR_WorldUV", //Floors
            "Shader Graphs/PR_HDRP_HUD_Unlit", //Aiming
            "PolygonR/Particles Smoke", //Smokes
            "Shader Graphs/PR_HDRP_Par_Add_2", //Additive FX
            "Shader Graphs/PR_HDRP_Par_Add", //Additive particles
            "Legacy Shaders/Particles/Alpha Blended Premultiply", //alpha Blended particles
            "Shader Graphs/PR_HDRP_Laser",// Lasers
            "PolygonR/Particles Distort", //Distortion particles NOT WORKING
            "PolygonR/TurretArea" //Area drawing
        };

        Debug.LogWarning("REPLACING " + materials_to_convert.Length + " MATERIALS");
        Debug.Log("USING " + standard_shaders_names.Length +" SHADERS");
        int a = 0;
        foreach (string x in standard_shaders_names)
        {
            if (target_pipeline == Pipe.Standard)
            {
                if (shader_name == universal_render_pipeline_shaders[a] || shader_name == high_resolution_shaders_names[a])
                {
                    Debug.Log("Shader found! Replacing shader!");
                    mat.shader = Shader.Find(standard_shaders_names[a]);
                }
            }
            else if (target_pipeline == Pipe.URP)
            {
                if (shader_name == standard_shaders_names[a] || shader_name == high_resolution_shaders_names[a])
                {
                    Debug.Log("Shader found! Replacing shader!");
                    mat.shader = Shader.Find(universal_render_pipeline_shaders[a]);
                }
            }
            else if (target_pipeline == Pipe.HDRP)
            {
                if (shader_name == universal_render_pipeline_shaders[a] || shader_name == standard_shaders_names[a])
                {
                    Debug.Log("Shader found! Replacing shader!");
                    mat.shader = Shader.Find(high_resolution_shaders_names[a]);
                    //
                    string material_path = AssetDatabase.GetAssetPath(mat);
                    AssetDatabase.ImportAsset(material_path, ImportAssetOptions.ImportRecursive);
                }
            }
            else
            {
                Debug.LogWarning(mat.name + " replacement shader for " + target_pipeline + " render pipeline");
            }

            a++;
        }
    }

    void RenameAssets()

    {
        if (materials_to_convert.Length > 0)
        {
            foreach (Material mat in materials_to_convert)
            {
                string shader_name = mat.shader.name;
                //string[] shader_types = new string[] {"Lit", "Cha", "WorldUV", "WorldUV_Floor", "Particles", "Simple Lit"};

                ReplaceShaders(shader_name, mat);

                Debug.Log(mat + " OLD SHADER " + shader_name + " NEW SHADER " + mat.shader.name);

                // AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(a), newName);
            }
        }

        if (target_pipeline == Pipe.HDRP)
        {
            GraphicsSettings.defaultRenderPipeline = HD_render_pipeline_asset;
        }
        else if (target_pipeline == Pipe.URP)
        {
            GraphicsSettings.defaultRenderPipeline = universal_render_pipeline_asset;
        }
        else
        {
            GraphicsSettings.defaultRenderPipeline = null;
        }
    }
}
#endif