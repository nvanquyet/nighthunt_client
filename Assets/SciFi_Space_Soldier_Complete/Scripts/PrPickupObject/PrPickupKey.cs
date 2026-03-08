using UnityEngine;
using System.Collections;

public class PrPickupKey : PrPickupObject {

    public enum Key
    {
        Blue,
        Yellow,
        Red,
        Full
    }

    [Header("Key Settings")]
    public Key KeyType;
    
      
 	// Update is called once per frame
	void Update () {
	
	}

    protected override void SetName()
    {
        itemName = KeyType.ToString();
    }

    protected override void PickupObjectNow(int ActiveWeapon)
    {

        if (Player != null)
        {
            PrCharacterInventory PlayerInv = Player.GetComponent<PrCharacterInventory>();

            PlayerInv.PickupKey((int)KeyType);

        }

        base.PickupObjectNow(ActiveWeapon);
    }
   
   
}
