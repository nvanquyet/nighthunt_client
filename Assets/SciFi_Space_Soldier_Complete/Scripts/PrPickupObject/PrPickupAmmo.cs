using UnityEngine;
using System.Collections;

public class PrPickupAmmo : PrPickupObject {

    public enum Ammo
    {
        Small = 4,
        Medium = 2,
        Full = 1
    }

    [Header("Type Ammo Settings")]
    public Ammo LoadType = Ammo.Full;

    public bool custom_weapon_only = false;

    public string custom_weapon_name = "weapon name";

    // Update is called once per frame
    void Update () {
	
	}

    protected override void SetName()
    {
        if (custom_weapon_only)
        {
            itemName = LoadType.ToString() + " " + custom_weapon_name + " Ammo";
        }
        else
        {
            itemName = LoadType.ToString() + " Ammo";
        }

    }

    protected override void PickupObjectNow(int ActiveWeapon)
    {

        if (Player != null)
        {
            PrCharacterInventory PlayerInv = Player.GetComponent<PrCharacterInventory>();

            if (custom_weapon_only)
            {
                foreach (GameObject w in PlayerInv.Weapon)
                {
                    if (w.GetComponent<PrWeapon>().WeaponName == custom_weapon_name)
                    {
                        Debug.Log(w + " " + "loading ammo");
                        w.GetComponent<PrWeapon>().LoadAmmo((int)LoadType);
                    }
                }
            }
            else
            {
                if (PlayerInv.Weapon[ActiveWeapon].GetComponent<PrWeapon>() != null)
                {
                    PlayerInv.Weapon[ActiveWeapon].GetComponent<PrWeapon>().LoadAmmo((int)LoadType);
                }
            }

        }

        base.PickupObjectNow(ActiveWeapon);
    }
   

   
}
