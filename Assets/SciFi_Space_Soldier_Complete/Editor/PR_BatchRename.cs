#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;
using UnityEditorInternal;

public class PR_BatchRename : ScriptableWizard
{
    public string suffix = "_LWRP";
    public string prefix = "";
    public Object[] assetToRename; //To rename
    

    [MenuItem("PolygonR/rename Assets...")]
    static void CreateWizard()
    {
        ScriptableWizard.DisplayWizard<PR_BatchRename>("PolygonR : rename Assets", "RENAME");
    }

    void OnWizardUpdate()
    {
    }

    void OnWizardCreate()
    {
        RenameAssets();

    }

    void RenameAssets()

    {
        
        //Debug.Log(newName);
        //Debug.Log(AssetDatabase.GetAssetPath(assetToRename));
        //AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(assetToRename), newName);
        if (assetToRename.Length > 0)
        {
            foreach (Object a in assetToRename)
            {
                string newName = prefix + a.name + suffix;
                AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(a), newName);
            }
        }

        
        
    }

}
#endif