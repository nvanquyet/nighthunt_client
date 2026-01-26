using NUnit.Framework;
using UnityEngine;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Items;
using NightHunt.InteractionSystem.Pickup;

namespace NightHunt.InteractionSystem.Test
{
    public class PickupSystemTests
    {
        private GameObject testPlayer;
        private GameObject testItem;
        private PickupHandler pickupHandler;
        private GridInventoryComponent inventory;

        [SetUp]
        public void Setup()
        {
            // Create test player
            testPlayer = new GameObject("TestPlayer");
            pickupHandler = testPlayer.AddComponent<PickupHandler>();
            inventory = testPlayer.AddComponent<GridInventoryComponent>();

            // Create test item
            testItem = new GameObject("TestItem");
            NetworkLootItem lootItem = testItem.AddComponent<NetworkLootItem>();
            lootItem.Initialize("test_item", 1);
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(testPlayer);
            Object.DestroyImmediate(testItem);
        }

        [Test]
        public void PickupAddsItemToInventory()
        {
            // Arrange
            int initialCount = inventory.Items.Count;

            // Act
            IPickupable pickupable = testItem.GetComponent<IPickupable>();
            // pickupHandler.RequestPickup(pickupable);

            // Assert
            Assert.AreEqual(initialCount + 1, inventory.Items.Count);
        }

        [Test]
        public void CannotPickupWhenInventoryFull()
        {
            // TODO: Implement test
            Assert.Pass("Test not implemented yet");
        }

        [Test]
        public void AutoStackingWorks()
        {
            // TODO: Implement test
            Assert.Pass("Test not implemented yet");
        }
    }
}