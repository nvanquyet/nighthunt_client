using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LegIK : MonoBehaviour
{
    public float legLength = 4.0f;
    public Transform IKTarget;
    public Transform knee;
    public Transform ankle;
    public Vector3 kneeBasePos;
    public float kneeOffset = 0.0f;
    public float kneeOffsetFactor = 4.0f;
    public float ankleDistance = 0.0f;

    // Start is called before the first frame update
    void Start()
    {
        kneeBasePos = IKTarget.localPosition;
        legLength = Vector3.Distance(transform.position, ankle.position);
    }

    // Update is called once per frame
    void Update()
    {
        UpdateLegIK();
    }

    void UpdateLegIK()
    {
        ankleDistance = Vector3.Distance(transform.position,ankle.position);

        if (ankleDistance < legLength)
        {
            kneeOffset = legLength - ankleDistance;
            IKTarget.localPosition = new Vector3(kneeBasePos.x + (kneeOffset * kneeOffsetFactor), kneeBasePos.y, kneeBasePos.z - kneeOffset);
        }

        knee.transform.LookAt(transform.position, transform.up);
        ankle.transform.LookAt(knee.transform.position, transform.up);
    }
}
