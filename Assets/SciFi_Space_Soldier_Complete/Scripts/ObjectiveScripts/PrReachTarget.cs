using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrReachTarget : MonoBehaviour {

    [Header ("Resist Zones Variables")]
    [HideInInspector]
    public bool isResistZone = false;
    [HideInInspector]
    public float actualTimer = 0.0f;
    private bool countTimer = false;
    [HideInInspector]
    public int playerCount = 0;
    [HideInInspector]
    public int totalPlayers = 1;
    private bool timeReached = false;

    public MeshRenderer zoneTarget;
    public Color activeColor = new Color(1, 1, 1, 1);
    public bool overrideInitialColor = false;
    public Color inactiveColor = new Color(1, 1, 1, 1);

    [Header("Debug")]

    public Mesh areaMesh;
    public Mesh textMesh;
    public TextMesh textInfo;
    public string textInfoContent;
    public Color color = new Color(1,1,1,1);
    



    // Use this for initialization
    void Start () {
        if (zoneTarget)
        {
            if (overrideInitialColor)
                zoneTarget.material.SetColor("_TintColor",inactiveColor);
            else
                inactiveColor = zoneTarget.sharedMaterial.color;
        }
        

		if (textInfo)
        {
            textInfo.gameObject.SetActive(false);
        }
	}
	
	// Update is called once per frame
	void Update () {
		if (isResistZone && timeReached == false)
        {
            if (actualTimer > 0.0f && countTimer)
            {
                actualTimer -= Time.deltaTime;
            }
            else if (countTimer)
            {
                timeReached = true;
                SendMessageUpwards("ObjComplete", true, SendMessageOptions.DontRequireReceiver);
            }
        }
	}

    void OnDrawGizmos()
    {
        //Color color = new Vector4(0.5f, 0.5f, 0, 0.2f)* 2;
        Gizmos.color = color;
        if (areaMesh)
        {
            Gizmos.DrawMesh(areaMesh, transform.position, Quaternion.identity, transform.localScale);
        }
        if (textInfo)
        {
            textInfo.text = transform.parent.name + "\nTarget";
            textInfo.color = color * 2;
        }
    }


    void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            if (!isResistZone)
                SendMessageUpwards("ObjComplete", true, SendMessageOptions.DontRequireReceiver);
            
            playerCount += 1;


            if (totalPlayers <= playerCount)
            {
                if (zoneTarget)
                    zoneTarget.material.SetColor("_TintColor", activeColor);
                countTimer = true;
            }
        }
    }

    private void OnTriggerExit (Collider other)
    {
        if (other.tag == "Player")
        {
            
            playerCount -= 1;
            if (totalPlayers > playerCount)
            {
                if (zoneTarget)
                    zoneTarget.material.SetColor("_TintColor", color);
                countTimer = false;
            }
        }
    }
    /*
    private void OnTriggerStay(Collider other)
    {
        if (other.tag == "Player")
        {
            countTimer = true;
        }
    }*/
}
