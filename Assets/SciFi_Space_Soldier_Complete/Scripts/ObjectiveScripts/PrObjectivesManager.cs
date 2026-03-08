using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PrObjectivesManager : MonoBehaviour {

    [Header("Objective Manager")]
    public bool ObjectiveManagerActive = false;

    public float delayTimer = 3.0f; //Time between missions
    private float actualTimer = 0.0f; 
    private bool delayTimerActive = false;
    public float timeToShowCompleteMissionHUD = 2.0f;
    private float complMissionHUDTimer = 0.0f;
    private bool complMissionHUDVisible = false;
    public int startingObjective = 0; //What´s the first mission
    [HideInInspector]
    public int actualObjective = -1;
    [HideInInspector]
    public bool allObjectivesClear = false;

    public PrObjective[] Objectives;
    //HuD Vars
    [Header("HUD")]
    public GameObject objectivesHUD;
    public GameObject objectiveCompleteHUD;
    public GameObject actualObjectivesHUD;
    public GameObject actualObjectivesTimerHUD;
    public GameObject actualTimerHUD;
    public GameObject actualObjectiveCompleteHUD;
    private string OriginalObjectiveCompleteText;
    public GameObject missionTitle;
    public GameObject missionDescription;

    // Use this for initialization
    void Start () {

        if (objectivesHUD && !actualObjectivesHUD)
        {
            actualObjectivesHUD = Instantiate(objectivesHUD, Vector3.zero, Quaternion.identity);
            actualObjectivesHUD.transform.SetParent(transform);
            actualObjectivesHUD.name = "ObjectivesHUD";
            actualObjectivesHUD.SetActive(false);

            Transform temp = actualObjectivesHUD.transform.Find("WeaponBackground"); 
            missionTitle = temp.Find("MissionTitle").gameObject;
            missionDescription = temp.Find("MissionDescription").gameObject;

            actualObjectivesTimerHUD = actualObjectivesHUD.transform.Find("TimerBlackBox").gameObject;
            actualTimerHUD = actualObjectivesTimerHUD.transform.Find("Timer").gameObject;
            actualObjectivesTimerHUD.SetActive(false);
           
        }

        if (objectiveCompleteHUD && !actualObjectiveCompleteHUD)
        {
            actualObjectiveCompleteHUD = Instantiate(objectiveCompleteHUD, Vector3.zero, Quaternion.identity);
            actualObjectiveCompleteHUD.transform.SetParent(transform);
            actualObjectiveCompleteHUD.name = "ObjectivesCompleteHUD";
            OriginalObjectiveCompleteText = actualObjectiveCompleteHUD.GetComponentInChildren<Text>().text;
            actualObjectiveCompleteHUD.SetActive(false);
        }

        actualObjective = startingObjective - 1; 

        if (Objectives.Length > 0)
        {
            foreach (PrObjective a in Objectives)
            {
                a.gameObject.SetActive(false);
            }
        }
	}
    
    void ActivateObjectiveCompleteHUD(bool active)
    {
        if (active)
        {
            complMissionHUDVisible = true;
            complMissionHUDTimer = timeToShowCompleteMissionHUD;
        }
        if (actualObjectiveCompleteHUD && actualObjective >= 0)
        {
            actualObjectiveCompleteHUD.SetActive(active);
            if (Objectives[actualObjective].missionCompleteText != "")
            {
                actualObjectiveCompleteHUD.GetComponentInChildren<Text>().text = Objectives[actualObjective].missionCompleteText;
            }
            else
            {
                actualObjectiveCompleteHUD.GetComponentInChildren<Text>().text = OriginalObjectiveCompleteText;
            }
        }
            
    }

	// Update is called once per frame
	void Update () {
		
        if (delayTimerActive && actualTimer > 0.0f)
        {
            actualTimer -= Time.deltaTime;
        }
        else if (!ObjectiveManagerActive && actualTimer <= 0.0f)
        {
            delayTimerActive = false;
            NextObjective();
        }
        if (complMissionHUDVisible && complMissionHUDTimer > 0.0f)
        {
            complMissionHUDTimer -= Time.deltaTime;
        }
        else if (complMissionHUDVisible && complMissionHUDTimer <= 0.0f)
        {
            complMissionHUDVisible = false;
            ActivateObjectiveCompleteHUD(false);
        }
	}

    public void ActivateTimer(bool active)
    {
        actualObjectivesTimerHUD.SetActive(active);
       // actualTimerHUD.GetComponent<Text>().text = string(actualTimer)
    }

    public void ActivateHUD(bool active)
    {
        missionTitle.GetComponent<Text>().text = Objectives[actualObjective].missionTitle;
        missionDescription.GetComponent<Text>().text = Objectives[actualObjective].missionDescription;
        actualObjectivesHUD.SetActive(active);
        if (Objectives[actualObjective].useTimeLimit && actualObjectivesTimerHUD)
            ActivateTimer(active);
        else
            ActivateTimer(false);
    }

    public void InitializeManager()
    {
        //ActivateHUD(false);
        ObjectiveManagerActive = false;
        delayTimerActive = true;
        actualTimer = delayTimer;
        ////Debug.Log("Reset timer");

    }

    public void NextObjective()
    {
        ActivateObjectiveCompleteHUD(false);
        ObjectiveManagerActive = true;
        actualObjective += 1;
        
        if (Objectives.Length > actualObjective)
        {
            Objectives[actualObjective].gameObject.SetActive(true);
            Objectives[actualObjective].ActivateObjective();
            if (actualObjectivesHUD && missionDescription && missionTitle)
            {
                ActivateHUD(true);
            }
        }
    }

    /*public void ObjectiveFailed(string Obj)
    {
        ActivateHUD(false);
        //Debug.Log("Objective FAILED " + Obj);
        Objectives[actualObjective].gameObject.SetActive(false);
        if (Objectives[actualObjective].isImportant)
        {
            //GameOver
            ActivateHUD(false);
        }
        else
        {
            InitNextObjective();
        }
    }*/

    void InitNextObjective()
    {
        if (Objectives.Length > (actualObjective + 1))
        {
            InitializeManager();
        }
        else
        {
            //Debug.Log("All Objectives Compeleted");
            allObjectivesClear = true;
        }
    }

    public void ObjectiveComplete(string Obj, bool isComplete)
    {
        
        ActivateHUD(false);
        Objectives[actualObjective].gameObject.SetActive(false);
        SendMessageUpwards("UpdateLastPlayerPosObjective", 0, SendMessageOptions.DontRequireReceiver);

        if (isComplete || Objectives[actualObjective].isImportant == false)
        {
            //Debug.Log("Objective Complete " + Obj);
            ActivateObjectiveCompleteHUD(isComplete);
            InitNextObjective();
        }
        else
        {
            //Debug.Log("Objective failed " + Obj);
            SendMessageUpwards("GameOver", SendMessageOptions.DontRequireReceiver);
        }

    }

}
