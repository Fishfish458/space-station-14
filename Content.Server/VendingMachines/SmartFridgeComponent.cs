using Content.Server.UserInterface;
using Robust.Server.GameObjects;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Content.Shared.VendingMachines;

namespace Content.Server.VendingMachines
{
    [RegisterComponent]
    public sealed class SmartFridgeComponent : SharedVendingMachineComponent
    {
        [ViewVariables] public BoundUserInterface? UserInterface => Owner.GetUIOrNull(VendingMachineUiKey.Key);

        [DataField("whitelist")]
        [ViewVariables] public EntityWhitelist? Whitelist;
        [ViewVariables] public Container Storage = default!;
        public Dictionary<string,  Queue<EntityUid>> entityReference = new();
    }
}

