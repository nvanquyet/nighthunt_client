using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrWeaponNodeHelper : MonoBehaviour {

    [Header("Display & Debug Settings")]
    public Mesh WeaponReference;
    public Mesh grenadeReference;
    public float meshScale = 1.0f;
    public bool useGrenade = false;

    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    void OnDrawGizmos()
    {
        if (!useGrenade)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawMesh(WeaponReference, transform.position, transform.rotation, Vector3.one * meshScale);
        }
            
        else
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawMesh(grenadeReference, transform.position, transform.rotation, Vector3.one * meshScale);
        }
        
    }
}
