using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrActorUtils : MonoBehaviour
{

    public enum AT
    {
        player, enemy, friendlyAI, neutralAI, vehicle, destroyable
    }
    //[Header("Character Type")]
    [HideInInspector]
    public AT Type = AT.player;
    [HideInInspector]
    public PrCharacter character;
    [HideInInspector]
    public int team = 0;

    [Header("Basic References")]
    public Transform BurnAndFrozenVFXParent;
    public Transform WeaponR;
    public Transform WeaponL;
    [HideInInspector]
    public Renderer[] MeshRenderers;

    [Header("AI References")]
    public Transform EarAndEyesPosition;
    [HideInInspector]
    public Animator charAnimator;
    [HideInInspector]
    public bool useRootMotion;
    [HideInInspector]
    public AudioSource Audio;


    void Awake()
    {
        MeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
    }

    // Start is called before the first frame update
    void Start()
    {

        if (GetComponent<AudioSource>())
            Audio = GetComponent<AudioSource>();
        if (GetComponent<Animator>())
        {
            charAnimator = GetComponent<Animator>();
            if (charAnimator != null)
                charAnimator.applyRootMotion = useRootMotion;
        }
    }

    public void SetTagAndLayer()
    {
        if (Type == AT.player)
        {
            //Set Tag
            gameObject.tag = "Player";
            //Set Layer
            gameObject.layer = LayerMask.NameToLayer("PlayerCharacter");
        }
        else if (Type == AT.enemy)
        {
            //Set Tag
            gameObject.tag = "Enemy";
            //Set Layer
            gameObject.layer = LayerMask.NameToLayer("Character");
        }
        else if (Type == AT.friendlyAI)
        {
            //Set Tag
            gameObject.tag = "AIPlayer";
            //Set Layer
            gameObject.layer = LayerMask.NameToLayer("PlayerCharacter");
        }
        else if (Type == AT.neutralAI)
        {
            //Set Tag
            gameObject.tag = "AINeutral";
            //Set Layer
            gameObject.layer = LayerMask.NameToLayer("Character");
        }
        else if (Type == AT.destroyable)
        {
            //Set Tag
            gameObject.tag = "Destroyable";
            //Set Layer
            gameObject.layer = LayerMask.NameToLayer("Objects");
        }

        else if (Type == AT.vehicle)
        {
            //Set Tag
            gameObject.tag = "Vehicle";
            //Set Layer
            gameObject.layer = LayerMask.NameToLayer("Vehicles");
        }

        foreach (SkinnedMeshRenderer m in MeshRenderers)
        {
            m.gameObject.layer = gameObject.layer;
        }
    }


    public void SetPlayerColors(int mode, int incomingTeam, PrPlayerSettings playerSettings)
    {

        if (mode == 0)
        {
            //Singleplayer Colors
            if (playerSettings.useSinglePlayerColor)
            {
                foreach (Renderer rend in MeshRenderers)
                {
                    rend.material.SetColor("_MaskedColorA", playerSettings.singlePlayerColor);
                }
            }

        }
        else if (mode == 1)
        {
            //Deathmatch Colors
            foreach (Renderer rend in MeshRenderers)
            {
                rend.material.SetColor("_MaskedColorA", playerSettings.playerColor[incomingTeam]);
            }

            team = incomingTeam;
        }
        else if (mode == 2)
        {
            //Coop Colors
            if (playerSettings.useCoopPlayerColor)
            {
                foreach (Renderer rend in MeshRenderers)
                {
                    rend.material.SetColor("_MaskedColorA", playerSettings.coopPlayerColor[incomingTeam]);
                }
            }

            team = incomingTeam;
        }
        else if (mode == 3)
        {
            //Team DeathMatch Colors
            foreach (Renderer rend in MeshRenderers)
            {
                //Debug.Log(team);
                rend.material.SetColor("_MaskedColorA", playerSettings.teamColor[incomingTeam]);
            }

            team = incomingTeam;
        }
    }


    public void SetNewSpeed(float speedFactor)
    {
        if (Type == AT.player && character != null)
            character.characterController.m_MoveSpeedSpecialModifier = speedFactor;
    }

    public void FootStep(string stepType)
    {
        character?.FootStep(stepType);
    }

    public void RollSound(AudioClip SFX)
    {
        if (SFX != null && Audio != null)
            Audio.PlayOneShot(SFX);
    }

    public void CantRotate()
    {
        if(character != null) character.characterController.b_CanRotate = false;
    }

    public void CanJump(int Value)
    {
        if (Value == 1)
        {
            if(character != null)character.characterController.b_canJump = true;
        }
        else
        {
            if(character != null) character.characterController.b_canJump = false;
        }
    }

    public void CanAttack()
    {
        if(character != null)character.AIController.CanAttack();
    }

    public void EndRoll()
    {
        if(character != null)character.characterController.EndRoll();
    }

    void ThrowG()
    {
        if(character != null)character.characterInventory.ThrowG();

    }

    void EndThrow()
    {
        if(character != null)character.characterInventory.EndThrow();
    }

    void EndMelee()
    {
        if (Type == AT.player)
            if(character != null)character.characterController.EndMelee();
        else if (Type == AT.enemy)
            if(character != null)character.AIController.EndMelee();
    }

    void MeleeEvent()
    {
        if (Type == AT.player)
            if(character != null)character.characterController.MeleeEvent();
        else if (Type == AT.enemy)
            if(character != null)character.AIController.MeleeEvent();

    }

    public void FreezeMove(int active)
    {
        if (Type == AT.enemy || Type == AT.friendlyAI || Type == AT.neutralAI)
        {
            if(character != null)character.AIController.FreezeMove(active);
        }
    }


    public void EndPickup()
    {
        if(character != null)character.characterInventory.EndPickup();

    }

    public void PlayerTeam(int enTeam)
    {
        if(character != null)character.PlayerTeam(enTeam);
    }

    public void BulletPos(Vector3 BulletPosition)
    {
        if(character != null)character.BulletPos(BulletPosition);
    }

    void ApplyTempMod(float temperatureMod)
    {
        if(character != null)character.ApplyTempMod(temperatureMod);
    }

    void ApplyDamage(int damage)
    {
        if(character != null)character.ApplyDamagePassive(damage);
    }

    void OnTriggerEnter(Collider other)
    {
        if(character == null) return;
        if (Type == AT.player)
        {
            if (other.CompareTag("EnvZone"))
            {
                if (character.characterController.CamScript && other.GetComponent<PrEnvironmentZone>() != null)
                    character.characterController.CamScript.TargetHeight = other.GetComponent<PrEnvironmentZone>().CameraHeight;
            }
        }
        else if (Type == AT.enemy)
        {
            if (other.tag == "Noise")
            {
                ////Debug.Log("Noise active");
                character.AIController.CheckPlayerNoise(other.transform.position);
            }
        }
    }

    void OnTriggerStay(Collider other)
    {
        if(character == null) return;
        if (Type == AT.player)
        {
            if (other.CompareTag("Usable") && character.characterController.UsableObject == null)
            {
                if (other.GetComponent<PrUsableDevice>().IsEnabled)
                    character.characterController.UsableObject = other.gameObject;
            }
            else if (other.CompareTag("Pickup") && character.characterInventory.PickupObj == null)
            {
                character.characterInventory.PickupObj = other.gameObject;
                if (character.characterController.AutoPickupItems)
                {
                    character.characterInventory.PickupItem();
                }
            }
            else if (other.CompareTag("Vehicles") && character.characterController.insideVehicle == false)
            {
                character.characterController.vehicleToDrive = other.transform.parent.GetComponent<PrVehicle>();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if(character == null) return;
        if (Type == AT.player)
        {
            if (other.CompareTag("Usable") && character.characterController.UsableObject != null)
            {
                character.characterController.UsableObject = null;
                character.actualHUD.HUDUseHelper.SetActive(false);
            }
            if (other.CompareTag("Pickup") && character.characterInventory.PickupObj != null)
            {

                character.characterInventory.PickupObj = null;
            }
            else if (other.CompareTag("Vehicles") && character.characterController.insideVehicle == false)
            {
                character.characterController.vehicleToDrive = null;
            }
        }
    }
}
