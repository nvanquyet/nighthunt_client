#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

public class ObjectivesReOrdering : ScriptableWizard
{
    public GameObject[] Objectives; 
    public GameObject ObjectiveManager;
    public bool AssignRandomColors = true;
    public Color[] colorArray =  { Color.red,
            Color.blue,
            Color.green,
            Color.cyan,
            Color.magenta,
            Color.yellow};
    [MenuItem("PolygonR/Re Name and assign Objectives...")]
    static void CreateWizard()
    {
        ScriptableWizard.DisplayWizard<ObjectivesReOrdering>("PolygonR : Rename Objectives", "Re Order Objectives ");

    }

    void OnWizardUpdate()
    {
        /*helpString = "Set the Objectives in order and the tool will rename them and assing those to the Objectives Manager";
        if (Objectives == null || ObjectiveManager == null)
        {
            errorString = "you should assign the variables";
            isValid = false;
        }
        else
        {
            errorString = "";
            isValid = true;
        }*/
    }

    void OnWizardCreate()
    {

        ReOrderObjectives();

        
    }

    void ReOrderObjectives()
    {
        //Debug.Log("Renaming Objectives");
        int index = 1;
        List<PrObjective> newArray = new List<PrObjective>();
        
        foreach (GameObject a in Objectives)
        {
            //Debug.Log(a.name);
            string oldName = a.name;
            string newName = index + "-" + oldName.Remove(0,2);

            a.name = newName;

            if (AssignRandomColors)
            {
                //Color randomColor = new Vector4(Random.Range(0.0f, 0.4f), Random.Range(0.0f, 0.4f), Random.Range(0.0f, 0.4f), 0.25f) * 3;
                Color randomColor = colorArray[Random.Range(0, colorArray.Length - 1)] * Random.Range(0.85f,1.15f);
                a.GetComponent<PrObjective>().color = randomColor;
                a.GetComponent<PrObjective>().color.a = 1f;

                if (a.GetComponent<PrObjective>().ObjectiveType == PrObjective.objectives.Wait || a.GetComponent<PrObjective>().ObjectiveType == PrObjective.objectives.Reach)
                {
                    a.transform.GetComponentInChildren<PrReachTarget>().color = randomColor;
                    a.transform.GetComponentInChildren<PrReachTarget>().color.a = 1.0f;
                }
            }
                

            newArray.Add(a.GetComponent<PrObjective>());
            index += 1;

        }



        if (ObjectiveManager && Objectives.Length > 0)
        {
            //Debug.Log(ObjectiveManager.GetComponent<PrObjectivesManager>().Objectives);
            ObjectiveManager.GetComponent<PrObjectivesManager>().Objectives = newArray.ToArray();
        }
            
    }
    
    void ReparentObjects(Transform Target, Transform newParent)
    {
        //Debug.Log("Reparent objects Target =" + Target.name + " New Parent = " + newParent);
        Target.parent = newParent;
    }

}
#endif