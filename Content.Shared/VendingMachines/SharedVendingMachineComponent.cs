using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.Sound;


namespace Content.Shared.VendingMachines
{
    [NetworkedComponent]
    public class SharedVendingMachineComponent : Component
    {
        [ViewVariables]
        public List<VendingMachineInventoryEntry> Inventory = new();
        public bool Ejecting;
        public TimeSpan AnimationDuration = TimeSpan.Zero;
        [DataField("isBroken")]
        public bool Broken;
        /// <summary>
        /// When true, will forcefully throw any object it dispenses
        /// </summary>
        [DataField("speedLimiter")]
        public bool CanShoot = false;
        [DataField("soundVend")]
        // Grabbed from: https://github.com/discordia-space/CEV-Eris/blob/f702afa271136d093ddeb415423240a2ceb212f0/sound/machines/vending_drop.ogg
        public SoundSpecifier SoundVend = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");
        [DataField("soundDeny")]
        // Yoinked from: https://github.com/discordia-space/CEV-Eris/blob/35bbad6764b14e15c03a816e3e89aa1751660ba9/sound/machines/Custom_deny.ogg
        public SoundSpecifier SoundDeny = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
        [DataField("canShootEjectForce")]
        public float NonLimitedEjectForce = 7.5f;
        [DataField("canShootEjectRange")]
        public float NonLimitedEjectRange = 5f;


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
        public sealed class VendingMachineEjectMessage : BoundUserInterfaceMessage
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
        public sealed class InventorySyncRequestMessage : BoundUserInterfaceMessage
        {
        }

        [Serializable, NetSerializable]
        public sealed class VendingMachineInventoryMessage : BoundUserInterfaceMessage
        {
            public readonly List<VendingMachineInventoryEntry> Inventory;
            public VendingMachineInventoryMessage(List<VendingMachineInventoryEntry> inventory)
            {
                Inventory = inventory;
            }
        }

        [Serializable, NetSerializable]
        public sealed class VendingMachineInventoryEntry
        {
            [ViewVariables(VVAccess.ReadWrite)]
            public string ID;
            [ViewVariables(VVAccess.ReadWrite)]
            public string Name;
            [ViewVariables(VVAccess.ReadWrite)]
            public uint Amount;
            public VendingMachineInventoryEntry(string id, string name, uint amount)
            {
                ID = id;
                Name = name;
                Amount = amount;
            }
        }
        [Serializable, NetSerializable]
        public enum VendingMachineWireStatus : byte
        {
            Power,
            Access,
            Advertisement,
            Limiter
        }
    }
}
