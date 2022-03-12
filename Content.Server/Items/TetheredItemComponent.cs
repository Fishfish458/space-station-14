namespace Content.Server.TetheredItem
{
    /// <summary>
    /// Used for items that should be linked to others
    /// </summary>
    [RegisterComponent]
    public sealed class TetheredItemComponent : Component
    {
        /// <summary>
        /// Entity this item is attached to
        /// </summary>
        public EntityUid TetherHost;
        public bool Stored = true;

        public float DistanceAllowed = 2f;
    }
}
