using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PrObjective : MonoBehaviour {

    //Objectives can be Kill, reach, collect item or wait x seconds. 
    
    public enum objectives
    {
        Reach,
        Kill,
        Destroy, 
        Collect, 
        Wait
    }

    //HuD Vars
    [Header("Title and Description")]
    public string missionTitle;
    public string missionDescription;
    public string missionCompleteText = "";

    [Header("General Settings")]
    public bool isImportant = true;
    public bool objectiveActive = false;
    public float timeBeforeStarts = 3.0f;
    public objectives ObjectiveType;
    public bool useTimeLimit = false;
    private bool reachedTimeLimit = false;
    public float timeLimit = 10.0f;
    private float actualTimeLimit = 0.0f;
    public bool usePlayerCompass = true;
    private PrObjectivesManager objectivesManager;

    [Header("Reach Settings")]
       
    public GameObject reachTarget;

    [Header("Kill Settings")]

    public PrEnemySpawner[] enemySpawners;
    public PrAIController[] enemies;
    private int enemiesToKill = 0;
    private int actualEnemiesKilled = 0;

    [Header("Destroy Settings")]

    public PrDestroyableActor[] detroyableTargets;
    private int actorsToDestroy = 0;
    private int actualActorsDestroyed = 0;

    [Header("Collect Settings")]

    public PrPickupObject[] pickupableObjects;
    private int actorsToPickup = 0;
    private int actualPickups = 0;

    [Header("Wait / Resist Settings")]

    public GameObject resistZone;

    //PlayerVariables
    //public PrActorHUD[] characterHUD

    [Header("Debug")]
    public Color color = new Vector4(1,1,1,1);
    public Mesh targetMesh;

    // Use this for initialization
    void Start () {

        objectivesManager = FindObjectOfType<PrObjectivesManager>();
        if (objectivesManager)
        {
            transform.parent = objectivesManager.transform;
        }
       
        /*if (ObjectiveType == objectives.Destroy)
        {
            foreach (PrDestroyableActor d in detroyableTargets)
            {
                d.Destroyable = false;
            }
        }*/
    }
	
	// Update is called once per frame
	void Update () {
		if (useTimeLimit && !reachedTimeLimit)
        {
            if (ObjectiveType != objectives.Wait)
            {
                if (actualTimeLimit > 0.0f)
                {
                    actualTimeLimit -= Time.deltaTime;

                }
                else
                {
                    reachedTimeLimit = true;
                    actualTimeLimit = 0.0f;
                    ObjComplete(false);
                }
                objectivesManager.actualTimerHUD.GetComponent<Text>().text = floatToTimerString(Mathf.Abs(actualTimeLimit));
            }
            else
            {
                objectivesManager.actualTimerHUD.GetComponent<Text>().text = floatToTimerString(Mathf.Abs(resistZone.GetComponent<PrReachTarget>().actualTimer));
            }
                
        }
	}

    string floatToTimerString(float timer)
    {
        string minString = Mathf.Floor(timer / 60).ToString("00");
        string secString = Mathf.Floor(timer % 60).ToString("00");

        string finalTimer = minString + ":" + secString;
        return finalTimer;
    }

    public void ActivateCompass()
    {
        List<GameObject> play = new List<GameObject>(GameObject.FindGameObjectsWithTag("Player"));

        foreach (GameObject p in play)
        {
            if (ObjectiveType == objectives.Reach)
            {
                ////Debug.Log(p.name);
                if (p.GetComponent<PrActorUtils>())
                    p.GetComponent<PrActorUtils>().character.actualHUD.ActivateCompass(reachTarget);
                else if (p.GetComponent<PrVehicleController>())
                {
                    p.GetComponent<PrVehicleController>().driverController.characterHUD.ActivateCompass(reachTarget);
                }

            }
            /*else if (ObjectiveType == objectives.Kill && enemies.Length > 0)
            {
                p.GetComponent<PrTopDownCharInventory>().ActivateCompass(enemies[0].gameObject);
            }*/

            else if (ObjectiveType == objectives.Destroy && detroyableTargets.Length == 1)
            {
                foreach (PrDestroyableActor a in detroyableTargets)
                {
                    if (a != null)
                        p.GetComponent<PrActorUtils>().character.actualHUD.ActivateCompass(a.gameObject);
                }

            }
            else if (ObjectiveType == objectives.Collect && pickupableObjects.Length >= 1)
            {
                foreach (PrPickupObject a in pickupableObjects)
                {
                    ////Debug.Log("A name " + a);
                    if (a != null)
                    {
                        p.GetComponent<PrActorUtils>().character.actualHUD.ActivateCompass(a.gameObject);
                        ////Debug.Log(a);
                    }
                        
                }
            }
        }
    }

    public void ActivateObjective()
    {
        ////Debug.Log("Objective activated " + gameObject.name);
        objectiveActive = true;
       
        if (usePlayerCompass)
        {
            ActivateCompass();
        }

        int missingTargets = 0;

        if (ObjectiveType == objectives.Kill)
        {
            if (enemySpawners.Length > 0)
            {
                foreach (PrEnemySpawner spawner in enemySpawners)
                {
                    if (spawner != null)
                    {
                        spawner.transform.parent = transform;
                        enemiesToKill += spawner.MaxCount;
                        enemiesToKill -= spawner.TotalSpawnedDead;
                    }
                }
            }
            if (enemies.Length > 0)
            {
                foreach (PrAIController enemy in enemies)
                {
                    if (enemy == null || enemy.character.isDead == false )
                    {
                        ////Debug.Log(enemy);
                        enemy.transform.parent = transform;
                        enemiesToKill += 1;
                    }
                }
            }
            ////Debug.Log(enemiesToKill);
            if (enemiesToKill == 0)
            {
                Debug.LogWarning("No Enemies assigned to Kill");
                ObjComplete(true);
            }
        }

        else if (ObjectiveType == objectives.Destroy)
        {
            if (detroyableTargets.Length > 0)
            {
                foreach (PrDestroyableActor target in detroyableTargets)
                {
                    if (target != null)
                    {
                        target.Destroyable = true;
                        target.transform.parent = transform;
                        actorsToDestroy += 1;
                    }
                    else
                    {
                        missingTargets += 1;
                    }
                    if (missingTargets == detroyableTargets.Length)
                    {
                        Debug.LogWarning("All targets are null");
                        ObjComplete(true);
                    }
                }
            }
            else
            {
                Debug.LogWarning("No Targets Assigned");
                ObjComplete(true);

            }
            ////Debug.Log(actorsToDestroy);
        }

        else if (ObjectiveType == objectives.Collect)
        {
            if (pickupableObjects.Length > 0)
            {
                
                foreach (PrPickupObject target in pickupableObjects)
                {
                    if (target != null)
                    {
                        ////Debug.Log(target.name);
                        target.transform.parent = transform;
                        actorsToPickup += 1;
                    }
                    else
                    {
                        missingTargets += 1;
                    }
                    if (missingTargets == pickupableObjects.Length)
                    {
                        Debug.LogWarning("All targets are null");
                        ObjComplete(true);
                    }
                    
                }
            }
            else
            {
                Debug.LogWarning("No Targets Assigned");
                ObjComplete(true);

            }
            ////Debug.Log(actorsToPickup);
        }

        else if (ObjectiveType == objectives.Wait)
        {
            if (resistZone)
            {
                resistZone.transform.parent = transform;
                useTimeLimit = true;
                resistZone.GetComponent<PrReachTarget>().isResistZone = true;
                resistZone.GetComponent<PrReachTarget>().actualTimer = timeLimit;
                resistZone.GetComponent<PrReachTarget>().totalPlayers = objectivesManager.gameObject.GetComponent<PrGameSetup>().actualPlayerCount;

            }
            ////Debug.Log(resistZone);
        }

        if (useTimeLimit)
        {
            actualTimeLimit = timeLimit;
            reachedTimeLimit = false;
        }
    }

    public void CollectablePickup()
    {
        actualPickups += 1;
        if (actualPickups >= actorsToPickup)
        {
            ObjComplete(true);
        }
        else if (usePlayerCompass)
        {
            ////Debug.Log("actual pickups " + actualPickups);
            ActivateCompass(); 
        }
    }

    public void EnemyDead()
    {
        actualEnemiesKilled += 1;
        if (actualEnemiesKilled >= enemiesToKill)
        {
            ObjComplete(true);
        }
    }

    public void ActorDestroyed()
    {
        ////Debug.Log("Actor Detroyed");
        actualActorsDestroyed += 1;
        if (actualActorsDestroyed >= actorsToDestroy)
        {
            ObjComplete(true);
        }
        else if (usePlayerCompass)
        {
            ActivateCompass();
        }
    }

    public void DeactivateCompass()
    {
        List<GameObject> play = new List<GameObject>(GameObject.FindGameObjectsWithTag("Player"));

        foreach (GameObject p in play)
        {
            if (p.GetComponent<PrActorUtils>())
                p.GetComponent<PrActorUtils>().character.actualHUD.DeactivateCompass();
        }
    }
    public void ObjComplete(bool isComplete)
    {
        if (objectiveActive)
        {
            if (usePlayerCompass)
                DeactivateCompass();
            //SendMessageUpwards("ObjectiveComplete", gameObject.name, SendMessageOptions.DontRequireReceiver);
            objectivesManager.ObjectiveComplete(gameObject.name, isComplete);

            if (isComplete)
            {
                if (ObjectiveType == objectives.Kill)
                {
                    if (enemySpawners.Length > 0)
                    {
                        foreach (PrEnemySpawner spawner in enemySpawners)
                        {
                            if (spawner != null)
                                spawner.transform.parent = transform.parent;
                        }
                    }
                    if (enemies.Length > 0)
                    {
                        foreach (PrAIController enemy in enemies)
                        {
                            if (enemy != null)
                                enemy.transform.parent = transform.parent;
                        }
                    }
                }
                else if (ObjectiveType == objectives.Destroy)
                {
                    if (detroyableTargets.Length > 0)
                    {
                        foreach (PrDestroyableActor target in detroyableTargets)
                        {
                            target.transform.parent = transform.parent;
                        }
                    }
                }
            }
            

            objectiveActive = false;
        }
    }

    void OnDrawGizmos()
    {
        if (targetMesh)
        {
           
            Gizmos.color = color;
            if (ObjectiveType == objectives.Kill)
            {
                if (enemySpawners.Length > 0)
                {
                    foreach (PrEnemySpawner spawner in enemySpawners)
                    {
                        if (spawner != null)
                            Gizmos.DrawMesh(targetMesh, spawner.transform.position, Quaternion.identity, Vector3.one);
                    }
                }
                if (enemies.Length > 0)
                {
                    foreach (PrAIController enemy in enemies)
                    {
                        if (enemy != null)
                            Gizmos.DrawMesh(targetMesh, enemy.transform.position, Quaternion.identity, Vector3.one);
                    }
                }
            }
            else if (ObjectiveType == objectives.Destroy)
            {
                if (detroyableTargets.Length > 0)
                {
                    foreach (PrDestroyableActor target in detroyableTargets)
                    {
                        if (target != null)
                        {
                            Gizmos.DrawMesh(targetMesh, target.transform.position, Quaternion.identity, Vector3.one);
                        }
                    }
                }
            }
            else if (ObjectiveType == objectives.Collect)
            {
                if (pickupableObjects.Length > 0)
                {
                    foreach (PrPickupObject target in pickupableObjects)
                    {
                        if (target != null)
                        {
                            Gizmos.DrawMesh(targetMesh, target.transform.position, Quaternion.identity, Vector3.one);
                        }
                    }
                }
            }

        }
    }

}
