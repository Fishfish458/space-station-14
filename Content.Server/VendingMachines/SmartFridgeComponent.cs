using Content.Server.UserInterface;
using Content.Shared.Sound;
using Robust.Server.GameObjects;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Content.Shared.VendingMachines;
using Content.Server.VendingMachines.Systems;

namespace Content.Server.VendingMachines
{
    [RegisterComponent]
    public sealed class SmartFridgeComponent : SharedVendingMachineComponent
    {
        [ViewVariables] public BoundUserInterface? UserInterface => Owner.GetUIOrNull(VendingMachineUiKey.Key);

        [DataField("whitelist")]
        public EntityWhitelist? Whitelist;
        public Container? Storage = default!;
        public Dictionary<string,  Queue<EntityUid>> entityReference = new();
    }
}

