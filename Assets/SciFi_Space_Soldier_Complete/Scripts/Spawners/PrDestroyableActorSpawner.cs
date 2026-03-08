using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrDestroyableActorSpawner : MonoBehaviour
{
    [Header("Basic Settings")]
    public GameObject[] actorsToSpawn;
    public int quantity = 1;
    public Transform actorsParent;

    [Header("Transforms Settings")]
    public Vector3 randomRotation = Vector3.zero;
    [Range(0.1f, 3.0f)]
    public float randomScaleFactor = 1.0f;

    [Header("Spawner Settings")]
    public float spawnerRadius = 1.0f;
    public bool spawnInCircle = false;
    public int spawnPerCycle = 1;
    public int waveCount = 1;

    private int actualWaveCount = 0;

    public int maxCount = 0;
    public bool spawnerEnabled = true;
    public float spawnRate = 1.0f;
    public float spawnRateAcceleration = 0.0f;
    public float spawnStartDelay = 0.0f;

    [HideInInspector] public float spawnTimer = 0.0f;
    private int totalSpawned = 0;
    [HideInInspector] public int totalSpawnedDead = 0;

    [Header("Display & Debug Settings")]
    public Mesh areaMesh;
    public Mesh iconMesh;

    // Start is called before the first frame update
    void Start()
    {

    }
    // Update is called once per frame
    void Update()
    {

        if (totalSpawned < maxCount && spawnerEnabled)
        {
            if (spawnStartDelay <= 0.0f)
            {
                spawnTimer += Time.deltaTime;
                if (spawnTimer >= spawnRate)
                {
                    SpawnActor();
                }
            }
            else
            {
                spawnStartDelay -= Time.deltaTime;
            }
        }
    }

    public int GetRandomActor()
    {
        return Random.Range(0, actorsToSpawn.Length);
    }

    public void ActorDestroyed()
    {
        totalSpawnedDead += 1;
        actualWaveCount -= 1;
        if (totalSpawnedDead == maxCount)
        {
            spawnerEnabled = false;
        }
    }

    Vector3 RandomCircle(Vector3 center, float radius)
    {
        float ang = Random.value * 360;
        Vector3 pos;
        pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
        pos.y = center.y;
        pos.z = center.z + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
        return pos;
    }

    Vector3 RandomCirclePos(Vector3 pos)
    {
        Vector3 randomPos = Vector3.zero;
        float radius1 = Random.Range(-spawnerRadius, spawnerRadius);
        float radius2 = Random.Range(-spawnerRadius, spawnerRadius);
        randomPos = pos + new Vector3(radius1, 0.0f, radius2);
        return randomPos;
    }

    Vector3 GetRandomRotation()
    {
        float x = Random.Range(-randomRotation.x, randomRotation.x);
        float y = Random.Range(-randomRotation.y, randomRotation.y);
        float z = Random.Range(-randomRotation.z, randomRotation.z);

        Vector3 randomRot = new Vector3(x, y, z);

        return randomRot;
    }

    float GetRandomScale()
    {
        float factor = 1 - randomScaleFactor;
        float randomScale = Random.Range(1 - factor, 1 + factor);
        return randomScale;
    }

    void SpawnActor()
    {
        if (actorsToSpawn != null && actualWaveCount < waveCount)
        {
            for (int i = 0; i < spawnPerCycle; i++)
            {

                Vector3 finalSpawnPosition = RandomCirclePos(transform.position);
                Quaternion rot = transform.rotation;

                if (spawnInCircle)
                {
                    finalSpawnPosition = RandomCircle(transform.position, spawnerRadius);
                    rot = Quaternion.FromToRotation(Vector3.forward, transform.position - finalSpawnPosition);
                }

                int selectedActor = 0;
                if (actorsToSpawn.Length > 1)
                {
                    selectedActor = GetRandomActor();
                }

                GameObject spawnedActor = PrUtils.InstantiateActor(actorsToSpawn[selectedActor], finalSpawnPosition, rot, actorsToSpawn[selectedActor].name + "_" + totalSpawned, transform);

                spawnedActor.transform.Rotate(GetRandomRotation());

                spawnedActor.transform.localScale *= GetRandomScale();

                if (actorsParent)
                {
                    spawnedActor.transform.SetParent(actorsParent);
                }

                spawnedActor.transform.position = finalSpawnPosition;

                totalSpawned += 1;
                actualWaveCount += 1;
            }
        }

        spawnRate -= spawnRateAcceleration;
        spawnTimer = 0.0f;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawMesh(areaMesh, transform.position, Quaternion.identity, Vector3.one * spawnerRadius);
        Gizmos.color = Color.grey;
        Gizmos.DrawMesh(iconMesh, transform.position + new Vector3(0, 0.2f, 0), Quaternion.identity, Vector3.one * 1.5f);

        if (actorsToSpawn.Length > 0)
        {
            for (int i = 0; i < spawnPerCycle; i++)
            {
                Vector3 randomPos = transform.position + ((Vector3.right * 2 * i) - (Vector3.right * 2 * (spawnPerCycle / 2)));
                Gizmos.color = Color.red;

                MeshFilter[] stMeshes = actorsToSpawn[0].GetComponentsInChildren<MeshFilter>();
                SkinnedMeshRenderer[] skMeshes = actorsToSpawn[0].GetComponentsInChildren<SkinnedMeshRenderer>();

                if (stMeshes.Length > 0)
                {
                    foreach (MeshFilter st in stMeshes)
                    {
                        Gizmos.DrawMesh(
                            st.sharedMesh,
                            0,
                            st.transform.position + randomPos,
                            transform.rotation * st.transform.rotation,
                            st.transform.lossyScale
                            );
                    }
                }

                if (skMeshes.Length > 0)
                {
                    foreach (SkinnedMeshRenderer sk in skMeshes)
                    {
                        Gizmos.DrawMesh(sk.sharedMesh, 0, sk.transform.position + randomPos, transform.rotation * sk.transform.rotation, sk.transform.lossyScale);
                    }
                }

            }
        }
    }
}
