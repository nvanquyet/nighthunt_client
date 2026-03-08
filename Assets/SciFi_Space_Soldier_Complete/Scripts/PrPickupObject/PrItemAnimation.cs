using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrItemAnimation : MonoBehaviour
{
    // Start is called before the first frame update
    private Rigidbody body;
    public Vector2 heightForceMinMax = new Vector2(1.0f, 1.2f);
    public float directionForce = 1.0f;
    private Collider col;
    private float cooldown = 1.0f;
    void Start()
    {
        gameObject.AddComponent<Rigidbody>();
        body = GetComponent<Rigidbody>();
        body.constraints = RigidbodyConstraints.FreezeRotation;

        col = GetComponent<Collider>();
        col.isTrigger = false;
        gameObject.layer = LayerMask.NameToLayer("Items");

        Vector3 randomForce = new Vector3(
            Random.Range(directionForce, -directionForce),
            Random.Range(heightForceMinMax[0], heightForceMinMax[1]),
            Random.Range(directionForce, -directionForce)
            );

        body.AddForce(randomForce, ForceMode.Impulse);
    }

    // Update is called once per frame
    void Update()
    {
        if (gameObject.activeInHierarchy)
        {
            if (cooldown >= 0.0f)
            {
                cooldown -= Time.deltaTime;
            }
            else
            {
                cooldown = 0.0f;
            }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (cooldown <= 0.02f)
        {
            if (collision.contacts.Length > 0)
            {
                ContactPoint contact = collision.contacts[0];
                if (Vector3.Dot(contact.normal, Vector3.up) > 0.5)
                {
                    //collision was from below
                    col.isTrigger = true;
                    if (body)
                    {
                        Destroy(body);
                        gameObject.layer = LayerMask.NameToLayer("Default");
                    }
                }
            }
           
        }
    }

}
