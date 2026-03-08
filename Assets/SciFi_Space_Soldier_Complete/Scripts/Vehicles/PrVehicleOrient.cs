using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrVehicleOrient : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        RaycastHit hit;

        if (Physics.Raycast(transform.position + Vector3.one, Vector3.down, out hit, 2.0f, 0))
        {
            Debug.DrawRay(hit.point, hit.normal * 3.0f);
            transform.rotation = Quaternion.LookRotation(transform.forward * 2, hit.normal) ;
        }

    }
}
