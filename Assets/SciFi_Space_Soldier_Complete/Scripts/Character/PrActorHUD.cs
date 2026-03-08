using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PrActorHUD : MonoBehaviour
{
    //HuD Vars
    [Header("3D HUD")]
    public Transform followPlayerHUD;

    [Header("HUD")]
    public GameObject Compass;
    public TextMesh CompassDistance;
    public bool CompassActive = false;
    public Transform CompassTarget;

    public GameObject HUDHealthBar;
    public GameObject HUDStaminaBar;
    public GameObject HUDDamageFullScreen;

    [HideInInspector]
    public float HUDDamage;

    public GameObject HUDWeaponPicture;
    public GameObject HUDWeaponBullets;
    public GameObject HUDWeaponBulletsBar;
    public GameObject HUDWeaponClips;
    public GameObject HUDUseHelper;
    public GameObject HUDColorBar;

    public GameObject HUDGrenadesIcon;
    public GameObject HUDGrenadesCount;

    [Header("Quick Reload HUD")]
    public GameObject quickReloadPanel;
    public GameObject quickReloadMarker;
    public GameObject quickReloadZone;

    [Header("Multiplayer HUD")]
    public int playerNmb = 1;
    public float multiplayerHUDOffset = 70.0f;//70.0f
    private bool splitScreen = false;
    [HideInInspector]
    public int totalPlayers = 1;
    private Vector2 splitOff = new Vector2(200, 112);
    private Vector2 splitMargins = new Vector2(10, 0);
    private float splitScaleFactor = 0.9f;
    
    private bool useXP = false;

    [Header("XP HUD")]
    public Text HUDXpText;
    public Text HUDLvlText;
    public Image HUDXpBar;
    private string lvlText = "Lvl";

    [Header("Vehicle")]
    //public Image vehicleIcon;
    public Image SpeedBar;
    public Text speedText;

    [HideInInspector]
    public GameObject[] Canvases;

    [HideInInspector]
    public PrCharacterController charController;
    [HideInInspector]
    public PrCharacter character;
    [HideInInspector]
    public PrCharacterInventory characterInventory;

    [HideInInspector]
    public PrMinimap minimap;

    [Header("Third Person Camera")]
    public Image weaponAimTarget;

    private bool show_help = true;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void InitializeHUD()
    {

        DeactivateCompass();

        HUDHealthBar.GetComponent<RectTransform>().localScale = new Vector3(1.0f, 1.0f, 1.0f);

        if (HUDXpBar && HUDLvlText && HUDXpText)
        {
            useXP = true;
            HUDXpBar.GetComponent<RectTransform>().localScale = new Vector3(0.0f, 1.0f, 1.0f);
            HUDXpText.text = "0/0";
            HUDLvlText.text = lvlText + "01";
        }

        if (followPlayerHUD)
        {
            followPlayerHUD.gameObject.AddComponent<PrCopyPosition>();
            followPlayerHUD.GetComponent<PrCopyPosition>().targetObject = character.actualActorPrefab.transform;

        }

        InitializeHUDComponents();

        if (characterInventory && characterInventory.useQuickReload && quickReloadPanel && quickReloadMarker && quickReloadZone)
        {
            QuickReloadActive(false);
        }

        //Update grenades HUD
        if (HUDGrenadesCount)
            HUDGrenadesCount.GetComponent<Text>().text = characterInventory.grenadesCount.ToString();

    }


    public void UpdateXP(int Lvl, int Xp, int XpMax)
    {
        ////Debug.Log("HUD Adding XP");
        if (useXP)
        {
            ////Debug.Log("HUD Adding XP Complete"  + " " + Lvl + " " + Xp + " " + XpMax );
            HUDXpBar.GetComponent<RectTransform>().localScale = new Vector3(Mathf.Clamp((1.0f / XpMax) * Xp, 0.0f, 1.0f), 1.0f, 1.0f);
            HUDXpText.text = Xp + "/" + XpMax;
            HUDLvlText.text = lvlText + Lvl;
        }
    }

    public void QuickReloadActive(bool state)
    {
        ////Debug.Log("QuickReloading " + state );
        quickReloadPanel.SetActive(state);
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    void HideHelp(bool enable)
    {
        if (transform.Find("_HELP"))
        {
            transform.Find("_HELP").gameObject.SetActive(enable);
            show_help = enable;
        }
    }

    void LateUpdate()
    {
        if (CompassActive && !character.isDead)
        {
            if (CompassTarget)
            {
                Compass.transform.LookAt(CompassTarget.position);
                int d = Mathf.RoundToInt(Vector3.Distance(CompassTarget.position, Compass.transform.position));
                ////Debug.Log("Distance :" + d);
                CompassDistance.text = "" + d + " Mts";

                if (Vector3.Distance(CompassTarget.position, Compass.transform.position) <= 2.0f)
                {
                    CompassTarget = null;
                    DeactivateCompass();
                }
            }
            else
            {
                DeactivateCompass();
            }
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            if (show_help)
            {
                HideHelp(false);
            }
            else
            {
                HideHelp(true);
            }
        }
    }

    public void UnparentTransforms(Transform Target)
    {
        Target.SetParent(null);
    }

    public void ActivateCompass(GameObject Target)
    {
       ////Debug.Log("Compass activated " + Target.name);

        Compass.SetActive(true);
        CompassActive = true;
        CompassTarget = Target.transform;

    }

    public void DeactivateCompass()
    {
        CompassActive = false;
        Compass.SetActive(false);

    }

    public void SetHealthBar(float value, float total)
    {
        ////Debug.Log(value + "_" + total);
        HUDHealthBar.GetComponent<RectTransform>().localScale = new Vector3(Mathf.Clamp((1.0f / total) * value, 0.1f, 1.0f), 1.0f, 1.0f);
    }

    void InitializeHUDComponents()
    {
        if (HUDDamageFullScreen)
            HUDDamageFullScreen.GetComponent<UnityEngine.UI.Image>().color = new Vector4(1, 1, 1, 0);

        if (charController && playerNmb > 0)
        {
            RectTransform HUDHealtRect = HUDHealthBar.transform.parent.GetComponent<RectTransform>();
            RectTransform HUDWeaponRect = HUDWeaponBulletsBar.transform.parent.GetComponent<RectTransform>();

            //FOR MULTIPLAYER PURPOSES
            float XPos = HUDHealtRect.localPosition.x;
            float YPos = HUDHealtRect.localPosition.y;

            float XWeapPos = HUDWeaponRect.localPosition.x;
            float YWeapPos = HUDWeaponRect.localPosition.y;

            ////Debug.Log(XPos);

            if (splitScreen)
            {
                //Scale HUDS
                HUDWeaponRect.localScale *= splitScaleFactor;

                //Apply Split Screen Margins
                XWeapPos = XWeapPos - splitMargins.x;
                YWeapPos = YWeapPos - splitMargins.y;

                //Damage Rect
                Vector3 damageScale = HUDDamageFullScreen.GetComponent<RectTransform>().localScale * 1.01f;
                Vector3 damagePos = HUDDamageFullScreen.GetComponent<RectTransform>().localPosition;

                if (totalPlayers == 2)
                {
                    //Set Damage HUD size
                    HUDDamageFullScreen.GetComponent<RectTransform>().localScale =
                        new Vector3(damageScale.x * 0.5f, damageScale.y, damageScale.z);

                    weaponAimTarget.GetComponent<RectTransform>().localScale =
                        new Vector3( 0.5f, 0.5f, 0.5f);

                    if (playerNmb == 1)
                    {
                        HUDWeaponRect.localPosition = new Vector3(XWeapPos, YWeapPos, 0);
                        weaponAimTarget.GetComponent<RectTransform>().localPosition = new Vector3(-(splitOff.x * 0.5f), 0.0f, 0);
                        //Damage Position
                        HUDDamageFullScreen.GetComponent<RectTransform>().localPosition = damagePos - new Vector3((splitOff.x * 0.5f), 0, 0);

                    }
                    else if (playerNmb == 2)
                    {
                        charController.CamScript.GetComponentInChildren<AudioListener>().enabled = false;
                        HUDWeaponRect.localPosition = new Vector3(XWeapPos + splitOff.x, YWeapPos, 0);
                        weaponAimTarget.GetComponent<RectTransform>().localPosition = new Vector3((splitOff.x * 0.5f), 0.0f, 0);
                        //Damage Position
                        HUDDamageFullScreen.GetComponent<RectTransform>().localPosition = damagePos + new Vector3((splitOff.x * 0.5f), 0, 0);

                    }
                }
                else if (totalPlayers == 3)
                {
                    //Set Damage HUD size
                    HUDDamageFullScreen.GetComponent<RectTransform>().localScale =
                        new Vector3(damageScale.x * 0.5f, damageScale.y * 0.5f, damageScale.z);
                    weaponAimTarget.GetComponent<RectTransform>().localScale =
                        new Vector3(0.5f, 0.5f, 0.5f);

                    if (playerNmb == 1)
                    {
                        HUDDamageFullScreen.GetComponent<RectTransform>().localScale = new Vector3(damageScale.x, damageScale.y * 0.5f, damageScale.z);
                        HUDWeaponRect.localPosition = new Vector3(XWeapPos , YWeapPos + splitOff.y, 0);
                        weaponAimTarget.GetComponent<RectTransform>().localPosition = new Vector3(0.0f, splitOff.y * 0.5f, 0);
                        //Damage Position
                        HUDDamageFullScreen.GetComponent<RectTransform>().localPosition = damagePos + new Vector3(0, splitOff.y * 0.5f, 0);
                    }
                    else if (playerNmb == 2)
                    {
                        charController.CamScript.GetComponentInChildren<AudioListener>().enabled = false;
                        weaponAimTarget.GetComponent<RectTransform>().localPosition = new Vector3(-(splitOff.x * 0.5f),-(splitOff.y * 0.5f), 0);
                        HUDWeaponRect.localPosition = new Vector3(XWeapPos, YWeapPos, 0);
                        //Damage Position
                        HUDDamageFullScreen.GetComponent<RectTransform>().localPosition = damagePos - new Vector3((splitOff.x * 0.5f), splitOff.y * 0.5f, 0);
                    }
                    else if (playerNmb == 3)
                    {
                        charController.CamScript.GetComponentInChildren<AudioListener>().enabled = false;
                        weaponAimTarget.GetComponent<RectTransform>().localPosition = new Vector3((splitOff.x * 0.5f), -(splitOff.y * 0.5f), 0);
                        HUDWeaponRect.localPosition = new Vector3(XWeapPos + splitOff.x, YWeapPos, 0);
                        //Damage Position
                        HUDDamageFullScreen.GetComponent<RectTransform>().localPosition = damagePos + new Vector3((splitOff.x * 0.5f), -splitOff.y * 0.5f, 0);
                    }
                }
                else if (totalPlayers == 4)
                {
                    //Set Damage HUD size
                    HUDDamageFullScreen.GetComponent<RectTransform>().localScale =
                        new Vector3(damageScale.x * 0.5f, damageScale.y * 0.5f, damageScale.z);
                    weaponAimTarget.GetComponent<RectTransform>().localScale =
                        new Vector3(0.5f, 0.5f, 0.5f);

                    if (playerNmb == 1)
                    {
                        HUDWeaponRect.localPosition = new Vector3(XWeapPos, YWeapPos + splitOff.y, 0);
                        weaponAimTarget.GetComponent<RectTransform>().localPosition = new Vector3(-(splitOff.x * 0.5f), (splitOff.y * 0.5f), 0);
                        //Damage Position
                        HUDDamageFullScreen.GetComponent<RectTransform>().localPosition = damagePos - new Vector3((splitOff.x * 0.5f), splitOff.y * 0.5f, 0);
                           
                    }

                    else if (playerNmb == 2)
                    {
                        charController.CamScript.GetComponentInChildren<AudioListener>().enabled = false;
                        weaponAimTarget.GetComponent<RectTransform>().localPosition = new Vector3((splitOff.x * 0.5f), (splitOff.y * 0.5f), 0);
                        HUDWeaponRect.localPosition = new Vector3(XWeapPos + splitOff.x, YWeapPos + splitOff.y, 0);
                        //Damage Position
                        HUDDamageFullScreen.GetComponent<RectTransform>().localPosition = damagePos + new Vector3((splitOff.x * 0.5f), splitOff.y * 0.5f, 0);
                            
                    }

                    else if (playerNmb == 3)
                    {
                        charController.CamScript.GetComponentInChildren<AudioListener>().enabled = false;
                        weaponAimTarget.GetComponent<RectTransform>().localPosition = new Vector3(-(splitOff.x * 0.5f), -(splitOff.y * 0.5f), 0);
                        HUDWeaponRect.localPosition = new Vector3(XWeapPos, YWeapPos, 0);
                        //Damage Position
                        HUDDamageFullScreen.GetComponent<RectTransform>().localPosition = damagePos - new Vector3((splitOff.x * 0.5f), splitOff.y * 0.5f, 0);
                        
                    }

                    else if (playerNmb == 4)
                    {
                        charController.CamScript.GetComponentInChildren<AudioListener>().enabled = false;
                        weaponAimTarget.GetComponent<RectTransform>().localPosition = new Vector3((splitOff.x * 0.5f), -(splitOff.y * 0.5f), 0);
                        HUDWeaponRect.localPosition = new Vector3(XWeapPos + splitOff.x, YWeapPos, 0);
                        //Damage Position
                        HUDDamageFullScreen.GetComponent<RectTransform>().localPosition = damagePos + new Vector3((splitOff.x * 0.5f), -splitOff.y * 0.5f, 0);
                        
                    }

                }
            }
            else
            {
                //Debug.Log("Offseting HUD for multiplayer");
                YPos = HUDWeaponBulletsBar.transform.parent.GetComponent<RectTransform>().localPosition.y;
                XPos = HUDWeaponBulletsBar.transform.parent.GetComponent<RectTransform>().localPosition.x;

                //Debug.Log(HUDWeaponBulletsBar.transform.parent.GetComponent<RectTransform>().localPosition);
                //Debug.Log(multiplayerHUDOffset * (playerNmb - 1));
                HUDWeaponBulletsBar.transform.parent.GetComponent<RectTransform>().localPosition = new Vector3(XPos + (multiplayerHUDOffset * (playerNmb - 1)), YPos, 0);
                //Debug.Log(HUDWeaponBulletsBar.transform.parent.GetComponent<RectTransform>().localPosition);
            }

            //SET HUD COLOR ACCORDING TO PLAYER COLOR
            if (charController && HUDColorBar)
                HUDColorBar.GetComponent<UnityEngine.UI.Image>().color = charController.playerSettings.playerColor[playerNmb - 1];
        }
    }

    public void ResizeMiminaps()
    {
        if (splitScreen && minimap)
        {
            if (totalPlayers == 2)
            {
                if (playerNmb == 1)
                {
                    minimap.ScaleAndMove(new Vector2(200, 0), 0.7f, 0);
                }
                else if (playerNmb == 2)
                {
                    minimap.ScaleAndMove(new Vector2(0, 0), 0.7f, 1);
                }
            }
            else if (totalPlayers == 3)
            {
                if (playerNmb == 1)
                {
                    minimap.ScaleAndMove(new Vector2(200, 0), 0.7f, 0);
                }
                else if (playerNmb == 2)
                {
                    minimap.ScaleAndMove(new Vector2(200, 112.5f), 0.7f, 1);
                }
                else if (playerNmb == 3)
                {
                    minimap.ScaleAndMove(new Vector2(0, 112.5f), 0.7f, 2);
                }
            }
            else if (totalPlayers == 4)
            {
                if (playerNmb == 1)
                {
                    minimap.ScaleAndMove(new Vector2(200, 0), 0.7f, 0);
                }
                else if (playerNmb == 2)
                {
                    minimap.ScaleAndMove(new Vector2(200, 112.5f), 0.7f, 1);
                }
                else if (playerNmb == 3)
                {
                    minimap.ScaleAndMove(new Vector2(0, 0), 0.7f, 2);
                }
                else if (playerNmb == 4)
                {
                    minimap.ScaleAndMove(new Vector2(0, 112.5f), 0.7f, 3);
                }
            }
            else
            {

            }

            
        }
        /*else if (!splitScreen && minimap)
        {
            if (playerNmb > 1)
            {
                minimap.gameObject.SetActive(false);
            }
        }*/

        character.SetIconColor(charController.playerSettings.playerColor[playerNmb - 1]);
    }

    public void SetVehicleHUD(Sprite vehiclePicture, string vehicleName)
    {
        HUDWeaponPicture.GetComponent<Image>().sprite = vehiclePicture;
        if (HUDWeaponPicture.GetComponentInChildren<Text>())
            HUDWeaponPicture.GetComponentInChildren<Text>().text = vehicleName;
    }

    public void SetSplitScreen(bool active, int tPlayers)
    {
        splitScreen = active;
        totalPlayers = tPlayers;
    }

    public void SetCanvasesVisible(bool visibility)
    {
        foreach (GameObject x in Canvases)
        {
            x.SetActive(visibility);
        }
    }
}
