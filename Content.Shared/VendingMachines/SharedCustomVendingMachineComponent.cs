using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.VendingMachines
{
    [NetworkedComponent()]
    public class SharedCustomVendingMachineComponent : Component
    {
        public override string Name => "CustomVendingMachine";

        [ViewVariables]
        public List<VendingMachineInventoryEntry> Inventory = new();

        [Serializable, NetSerializable]
        public enum VendingMachineVisuals
        {
            VisualState,
        }

        [Serializable, NetSerializable]
        public enum VendingMachineVisualState
        {
            Normal,
            Off,
            Broken,
            Eject,
            Deny,
        }

        [Serializable, NetSerializable]
        public class VendingMachineEjectMessage : BoundUserInterfaceMessage
        {
            public readonly string ID;
            public VendingMachineEjectMessage(string id)
            {
                ID = id;
            }
        }

        [Serializable, NetSerializable]
        public enum VendingMachineUiKey
        {
            Key,
        }

        [Serializable, NetSerializable]
        public class InventorySyncRequestMessage : BoundUserInterfaceMessage
        {
        }

        [Serializable, NetSerializable]
        public class VendingMachineInventoryMessage : BoundUserInterfaceMessage
        {
            public readonly List<VendingMachineInventoryEntry> Inventory;
            public VendingMachineInventoryMessage(List<VendingMachineInventoryEntry> inventory)
            {
                Inventory = inventory;
            }
        }

        [Serializable, NetSerializable]
        public class VendingMachineInventoryEntry
        {
            [ViewVariables(VVAccess.ReadWrite)]
            public string ID;
            [DataField("name")]
            public string Name;
            public EntityUid? EntityID;
            public uint Amount;
            public VendingMachineInventoryEntry(string id, string name, EntityUid? entityID, uint amount)
            {
                ID = id;
                Name = name;
                EntityID = entityID;
                Amount = amount;
            }
        }
    }
}
