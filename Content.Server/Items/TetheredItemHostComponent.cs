using Robust.Shared.Containers;

namespace Content.Server.TetheredItem
{

    /// <summary>
    /// Used for items that should be linked to others
    /// </summary>
    [RegisterComponent]
    public sealed class TetheredItemHostComponent : Component
    {
        /// <summary>
        /// Entity this item is attached to
        /// </summary>
        public bool Stored = true;
        [ViewVariables] public ContainerSlot GuardianContainer = default!;
        public EntityUid? HostedItem;
    }
}
