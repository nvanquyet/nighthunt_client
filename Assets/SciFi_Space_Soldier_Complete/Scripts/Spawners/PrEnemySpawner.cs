using UnityEngine;
using System.Collections;

public class PrEnemySpawner : MonoBehaviour {

    [Header("Enemy Settings")]
    public GameObject[] Enemy;
    private int actualEnemy = 0;
	private GameObject EnemyParent;
    public PrWaypointsRoute EnemyPatrolRoute;
    public bool startInRandomWaypoint = false;
    public bool SearchPlayerAfterSpawn = false;

    public bool towerDefenseAI = false;
    public Transform towerDefenseTarget;

    [Header("Spawner Settings")]
	public float SpawnerRadius = 1.0f;
    public bool spawnInCircle = false;
	public int SpawnPerCycle = 1;
    public int waveCount = 1;
    private int actualWaveCount = 0;
	public int MaxCount = 0;
	public bool SpawnerEnabled = true;
	public float SpawnRate = 1.0f;
    public float SpawnRateAcceleration = 0.0f;

    public float SpawnStartDelay = 0.0f;
    [HideInInspector] public float SpawnTimer = 0.0f;
	private int TotalSpawned = 0;
    [HideInInspector] public int TotalSpawnedDead = 0;

    [Header("Display & Debug Settings")]
    public Mesh AreaMesh;
    public Mesh IconMesh;

    // Use this for initialization
    void Start () {
		EnemyParent = new GameObject();
        if (Enemy.Length > 0)
        {
            EnemyParent.name = gameObject.name + "-" + Enemy[0].name + "_ROOT";
        }
        else
        {
            EnemyParent.name = gameObject.name + "-" + "_ROOT";
        }
        EnemyParent.transform.position = transform.position;
		EnemyParent.transform.rotation = transform.rotation;
        EnemyParent.transform.SetParent(this.transform);
	}

	// Update is called once per frame
	void Update () {

		if (TotalSpawned < MaxCount && SpawnerEnabled)
		{
            if (SpawnStartDelay <= 0.0f)
            {
                SpawnTimer += Time.deltaTime;
                if (SpawnTimer >= SpawnRate)
                {
                    SpawnEnemy();
                }
            }
            else
            {
                SpawnStartDelay -= Time.deltaTime;
            }
		}
	}

    void EnemyDead()
    {
        TotalSpawnedDead += 1;
        actualWaveCount -= 1;
        if (TotalSpawnedDead == MaxCount)
        {
            SpawnerEnabled = false;
            SendMessageUpwards("SpawnerCompleted", SendMessageOptions.DontRequireReceiver);
        }
    }

    Vector3 RandomCircle(Vector3 center, float radius)
    {
        float ang = Random.value * 360;
        Vector3 pos;
        pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
        pos.y = center.y ;
        pos.z = center.z + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
        return pos;
    }

    public void SetRandomEnemy()
    {
        actualEnemy = Random.Range(0, Enemy.Length);        
    }

    public void SurvivalSpawnEnemy(GameObject enemyToSpawn)
    {
        
        float RandomRadius = Random.Range(-SpawnerRadius, SpawnerRadius);
        float RandomRadius2 = Random.Range(-SpawnerRadius, SpawnerRadius);

        Vector3 FinalSpawnPosition = transform.position + new Vector3(RandomRadius, 0.1f, RandomRadius2);
        Quaternion rot = transform.rotation;

        if (spawnInCircle)
        {
            FinalSpawnPosition = RandomCircle(transform.position, SpawnerRadius);
            rot = Quaternion.FromToRotation(Vector3.forward, transform.position - FinalSpawnPosition);
        }

        GameObject EnemySpawned = Instantiate(enemyToSpawn, FinalSpawnPosition, rot) as GameObject;
        EnemySpawned.name = enemyToSpawn.name + "_" + TotalSpawned;

        if (EnemyPatrolRoute)
        {
            EnemySpawned.GetComponent<PrAIController>().waypointRoute = EnemyPatrolRoute;
        }

        if (SearchPlayerAfterSpawn)
        {
            if (EnemySpawned.GetComponent<PrAIController>() != null)
            {
                EnemySpawned.GetComponent<PrAIController>().FindPlayers();
                EnemySpawned.GetComponent<PrAIController>().lookForPlayer = true;
            }
            
        }

        if (towerDefenseAI)
        {
            EnemySpawned.GetComponent<PrAIController>().towerDefenseAI = true;
            if (towerDefenseTarget)
                EnemySpawned.GetComponent<PrAIController>().towerDefenseTarget = towerDefenseTarget;
        }

        EnemySpawned.GetComponent<PrAIController>().SetWaypoints();

        if (EnemyPatrolRoute && startInRandomWaypoint)
        {
            int max = EnemySpawned.GetComponent<PrAIController>().waypoints.Length - 1;
            int rndm = Random.Range(0, max);
            FinalSpawnPosition = EnemySpawned.GetComponent<PrAIController>().waypoints[rndm].position;
        }

        EnemySpawned.transform.parent = EnemyParent.transform;
        EnemySpawned.transform.position = FinalSpawnPosition;

        GameObject[] AIs = GameObject.FindGameObjectsWithTag("AIPlayer");
        foreach (GameObject AI in AIs)
        {
            AI.SendMessage("FindPlayers", SendMessageOptions.DontRequireReceiver);
        }

    }

    Vector3 RandomCirclePos(Vector3 pos)
    {
        Vector3 randomPos = Vector3.zero;
        float radius1 = Random.Range(-SpawnerRadius, SpawnerRadius);
        float radius2 = Random.Range(-SpawnerRadius, SpawnerRadius);
        randomPos = pos + new Vector3(radius1, 0.0f, radius2);
        return randomPos;
    }

    void SpawnEnemy()
	{
		if (Enemy != null && actualWaveCount < waveCount)
		{
            for (int i = 0; i < SpawnPerCycle; i++)
            {

                Vector3 FinalSpawnPosition = RandomCirclePos(transform.position);
                Quaternion rot = transform.rotation;

                if (spawnInCircle)
                {
                    FinalSpawnPosition = RandomCircle(transform.position, SpawnerRadius);
                    rot = Quaternion.FromToRotation(Vector3.forward, transform.position - FinalSpawnPosition);
                }

                if (Enemy.Length > 1)
                {
                    SetRandomEnemy();
                }
                else
                {
                    actualEnemy = 0;
                }

                GameObject EnemySpawned = PrUtils.InstantiateActor(Enemy[actualEnemy], FinalSpawnPosition, rot, Enemy[actualEnemy].name + "_" + TotalSpawned, EnemyParent.transform);

                if (EnemyPatrolRoute)
                {
                    EnemySpawned.GetComponent<PrAIController>().waypointRoute = EnemyPatrolRoute;
                       
                }

                if (SearchPlayerAfterSpawn)
                {
                    EnemySpawned.GetComponent<PrAIController>().CheckPlayerVisibility(360f);
                }
                    
                if (towerDefenseAI)
                {
                    EnemySpawned.GetComponent<PrAIController>().towerDefenseAI = true;
                    if (towerDefenseTarget)
                        EnemySpawned.GetComponent<PrAIController>().towerDefenseTarget = towerDefenseTarget;
                }

                EnemySpawned.GetComponent<PrAIController>().SetWaypoints();

                if (EnemyPatrolRoute && startInRandomWaypoint)
                {
                    int max = EnemySpawned.GetComponent<PrAIController>().waypoints.Length - 1;
                    int rndm = Random.Range(0, max);
                    FinalSpawnPosition = EnemySpawned.GetComponent<PrAIController>().waypoints[rndm].position;
                }

                EnemySpawned.transform.position = FinalSpawnPosition;

                TotalSpawned += 1;
                actualWaveCount += 1;
            }    
        }

        
        SpawnRate -= SpawnRateAcceleration;
        SpawnTimer = 0.0f;
    }

	void OnDrawGizmos()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawMesh(AreaMesh, transform.position, Quaternion.identity, Vector3.one * SpawnerRadius);
        Gizmos.color = Color.grey ;
        Gizmos.DrawMesh(IconMesh, transform.position + new Vector3(0,0.2f,0), Quaternion.identity, Vector3.one * 1.5f);

        if (Enemy.Length > 0)
        {
            for (int i = 0; i < SpawnPerCycle; i++)
            {
                Vector3 randomPos = transform.position + ((Vector3.right * 2 * i) - (Vector3.right * 2 * (SpawnPerCycle / 2)));
                Gizmos.color = Color.red;

                MeshFilter[] stMeshes = Enemy[0].GetComponent<PrCharacter>().actorPrefab[0].GetComponentsInChildren<MeshFilter>();
                SkinnedMeshRenderer[] skMeshes = Enemy[0].GetComponent<PrCharacter>().actorPrefab[0].GetComponentsInChildren<SkinnedMeshRenderer>();

                if (stMeshes.Length > 0)
                {
                    foreach (MeshFilter st in stMeshes)
                    {
                        //Debug.Log(st.sharedMesh);
                        Gizmos.DrawMesh(st.sharedMesh, 0, st.transform.position + randomPos, st.transform.rotation, st.transform.lossyScale);
                    }
                }

                if (skMeshes.Length > 0)
                {

                    foreach (SkinnedMeshRenderer sk in skMeshes)
                    {
                        //Debug.Log("SKMeshes " + sk.sharedMesh );
                        Gizmos.DrawMesh(sk.sharedMesh, 0, sk.transform.position + randomPos, sk.transform.rotation);
                    }
                }
                
            }
        }
    }

}
