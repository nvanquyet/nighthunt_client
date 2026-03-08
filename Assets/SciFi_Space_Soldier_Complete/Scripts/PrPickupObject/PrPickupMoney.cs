using UnityEngine;
using System.Collections;

public class PrPickupMoney : PrPickupObject {

    public int ammount = 1;
     
 	// Update is called once per frame
	void Update () {
	
	}

    protected override void SetName()
    {
        itemName = "Money";
    }

    protected override void PickupObjectNow(int ActiveWeapon)
    {

        if (Player != null)
        {
            PrCharacterInventory PlayerInv = Player.GetComponent<PrCharacterInventory>();

            PlayerInv.AddMoney(ammount);

        }

        base.PickupObjectNow(ActiveWeapon);
    }
   
   
}
