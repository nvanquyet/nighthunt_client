using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutomaticFootIK : MonoBehaviour
{

    public AnimationCurve torsoLoop;
    public float torsoYOrig = 0.0f;
    public float characterHeight = 0.0f;
    public float heightOffset = 0.2f;
    public float torsoAnimSpeed = 1.0f;
    public float averageAnkleHeight = 0.0f;

    //Legs
    public Transform[] legs;
    public int activeLeg = 0;
    public float legTimer = 0.5f;
    public float actualLegTimer = 0.0f;
    public float legsSpeed = 1.0f;
    public float stepHeight = 0.2f;

    public Transform[] footPos;
    public Transform[] actualFootPos;
    public float maxDistance = 0.5f;

    //IK
    public Transform[] ankles;
    public bool[] anklePassing;
    public AnimationCurve ankleAnimCurve;

    public float speed = 0.0f;
    public Vector3 lastPos = Vector3.zero;
    public Vector3 actualPos = Vector3.zero;
       
    //Debug


    // Start is called before the first frame update
    void Start()
    {

        if (footPos.Length > 0)
        {
            foreach (Transform f in footPos)
            {
                f.SetParent(null);
            }
            
        }
        if (ankles.Length > 0)
        {
            anklePassing = new bool[ankles.Length];
  
        }
        torsoYOrig = transform.localPosition.y;
        RaycastHit hit;
        int objectInt = 8;
        int objectLayer = 1 << objectInt;
        int finalMask = objectLayer;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 2.0f, finalMask))
        {
            characterHeight = Vector3.Distance(hit.point, transform.position) - heightOffset;
        }
        

    }

    // Update is called once per frame
    void Update()
    {
        //UpdatePos and Latest pos to know if it´s going forward or Back. So you can move Foot target forward or Back
        
        //Smooth movement saving rotation

        //Rotate to Zero all the other axes

        UpdateLegs();

        UpdateTorsoAnim();
    }

    void UpdateTorsoAnim()
    {

        //RayCheck
        RaycastHit hit;
        int objectInt = 8;
        int objectLayer = 1 << objectInt;
        int finalMask = objectLayer;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 2.0f, finalMask))
        {
            float dist = Vector3.Distance(hit.point, transform.position);
            Debug.DrawLine(hit.point, transform.position, Color.red);
            averageAnkleHeight = characterHeight - dist;
            if (averageAnkleHeight > characterHeight)
            {
                averageAnkleHeight = 0.0f;
            }
            ////Debug.Log(averageAnkleHeight + " " + dist);

        }
        Vector3 animPos = new Vector3(transform.localPosition.x, torsoYOrig  + torsoLoop.Evaluate(Time.time * torsoAnimSpeed), transform.localPosition.z);
        animPos.y += averageAnkleHeight;
        transform.localPosition = Vector3.Lerp(transform.localPosition , animPos, Time.deltaTime * 1.0f);
       
       
    }

    public void InitiateRagdoll()
    {
       foreach (Transform l in legs)
       {
           if (l.gameObject.GetComponent<Collider>() == null)
            {
                l.gameObject.AddComponent<BoxCollider>();
            }
           l.gameObject.AddComponent<Rigidbody>();
           l.gameObject.AddComponent<CharacterJoint>();
           l.gameObject.GetComponent<CharacterJoint>().connectedBody = l.parent.GetComponent<Rigidbody>();
       }
       this.enabled = false;
    }

    void UpdateLegs()
    {
        lastPos = actualPos;
        actualPos = transform.position;

        speed = Vector3.Distance(actualPos, lastPos);


        if (legs.Length > 0 && footPos.Length > 0)
        {
            int x = 0;
            foreach (Transform l in legs)
            {
                if (x % 2 == activeLeg)
                {
                    if (Vector3.Distance(footPos[x].position, actualFootPos[x].position) > maxDistance && Time.time > actualLegTimer + legTimer)
                    {
                        footPos[x].position = actualFootPos[x].position + ((actualFootPos[x].position - footPos[x].position).normalized * (speed * 10));
                        actualLegTimer = Time.time;

                        //RayCheck
                        RaycastHit hit;
                        int objectInt = 8;
                        int objectLayer = 1 << objectInt;
                        int finalMask = objectLayer;
                        if (Physics.Raycast(footPos[x].position + (Vector3.up), Vector3.down, out hit, 1.5f, finalMask))
                        {
                            footPos[x].position = new Vector3(footPos[x].position.x, hit.point.y + stepHeight, footPos[x].position.z);
                        }
                    }
                }
                else
                {
                    ankles[x].position = new Vector3(ankles[x].position.x, footPos[x].position.y , ankles[x].position.z);
                }
                
                Quaternion startRot = l.transform.rotation;
                l.transform.LookAt(footPos[x], transform.forward);
                Quaternion targetRot = l.transform.rotation;
                l.transform.rotation = Quaternion.Slerp(startRot, targetRot, Time.deltaTime * legsSpeed);
                //l.transform.localEulerAngles = new Vector3(l.transform.localEulerAngles.x, l.transform.localEulerAngles.y, 0.0f);
                x += 1;
            }

            activeLeg += 1;
            if (activeLeg >= 2)
                activeLeg = 0;
        }

    }

    void LateUpdate()
    {

    }

    void OnDrawGizmos()
    {
        if (actualFootPos.Length > 0)
        {
            foreach (Transform g in actualFootPos)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(g.position, 0.2f);
            }
        }
        if (footPos.Length > 0)
        {
            foreach (Transform g in footPos)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(g.position, 0.2f);
            }
        }
    }
}
