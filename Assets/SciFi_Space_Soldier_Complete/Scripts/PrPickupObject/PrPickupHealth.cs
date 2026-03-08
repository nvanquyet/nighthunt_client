using UnityEngine;
using System.Collections;

public class PrPickupHealth : PrPickupObject {
   
     
 	// Update is called once per frame
	void Update () {
	
	}

    protected override void SetName()
    {
        itemName = "Health Pack";
    }

    protected override void PickupObjectNow(int ActiveWeapon)
    {

        if (Player != null)
        {
            PrCharacterInventory PlayerInv = Player.GetComponent<PrCharacterInventory>();

            PlayerInv.character.SetHealth(PlayerInv.character.Health);

        }

        base.PickupObjectNow(ActiveWeapon);
    }
   
   
}
