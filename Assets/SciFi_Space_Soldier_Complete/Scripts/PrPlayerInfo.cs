using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrPlayerInfo : MonoBehaviour {

    public static PrPlayerInfo player1;

    [Header("Player Controller Variables")]
    public int[] playerNumber ;
    public bool[] usingJoystick ;
    public Vector3[] lastPlayerPosition;
    
    [Header("Player Inventory Variables")]
    public string[] playerName;
    public int[] lives ;
    public int[] health ;
    public int[] money;
    public int[] actualHealth ;
    public int[] maxWeaponCount ;

    public int[] weaponsP1;
    public int[] weaponsAmmoP1;
    public int[] weaponsClipsP1;

    public int[] weaponsP2;
    public int[] weaponsAmmoP2;
    public int[] weaponsClipsP2;

    public int[] weaponsP3;
    public int[] weaponsAmmoP3;
    public int[] weaponsClipsP3;

    public int[] weaponsP4;
    public int[] weaponsAmmoP4;
    public int[] weaponsClipsP4;

    public GameObject[] grenadeType;
    public int[] grenades;

    [Header("Objectives Variables")]
    public bool loadPrevSettings = false;
    public int lastObjectiveActive = 0;

    void Awake()
    {
        if (player1 == null)
        {
            DontDestroyOnLoad(gameObject);
            player1 = this;
        }
        else if (player1 != this)
        {
            Destroy(gameObject);
        }

        //Initialize Variables
        playerNumber = new int[4];
        usingJoystick = new bool[4];
        lastPlayerPosition = new Vector3[4];

        playerName = new string[4]; 
        lives = new int[4];
        health = new int[4];
        money = new int[4];
        actualHealth = new int[4];
        maxWeaponCount = new int[4];
        grenades = new int[4];

        weaponsP1 = new int[4];
        weaponsAmmoP1 = new int[4];
        weaponsClipsP1 = new int[4];

        weaponsP2 = new int[4];
        weaponsAmmoP2 = new int[4];
        weaponsClipsP2 = new int[4];

        weaponsP3 = new int[4];
        weaponsAmmoP3 = new int[4];
        weaponsClipsP3 = new int[4];

        weaponsP4 = new int[4];
        weaponsAmmoP4 = new int[4];
        weaponsClipsP4 = new int[4];
    }
    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
