using Content.Shared.Whitelist;
using Content.Shared.Sound;

namespace Content.Shared.Dispenser
{
    [RegisterComponent]
    public class DispenserComponent : Component
    {
    /// <summary>
    ///     The prototype to be dispensed
        /// </summary>
        [ViewVariables]
        [DataField("protoTypeDispensed")]
        public string PrototypeDispensed { get; set; } = "Paper";

        /// <summary>
        ///     The prototype that can replenish item count
        /// </summary>
        [ViewVariables]
        [DataField("refillPrototype")]
        public string? RefillPrototype { get; set; }

        /// <summary>
        ///     The amount that refillPrototype will refill
        /// </summary>
        [ViewVariables]
        [DataField("refillAmount")]
        public uint? RefillAmount { get; set; }

        /// <summary>
        ///     The amount that this dispenser holds
        /// </summary>
        [ViewVariables]
        [DataField("itemCount")]
        public uint ItemCount { get; set; } = 10;

        /// <summary>
        ///     Allows item of this type to be inserted back in to increase item count
        /// </summary>
        [DataField("insertWhitelist")]
        public EntityWhitelist? InsertWhitelist { get; set; }

        /// <summary>
        ///     If the count of the items is hidden
        /// </summary>
        [DataField("hiddenCount")]
        public bool HiddenCount { get; set; } = false;

        /// <summary>
        ///     On pickup Sound
        /// </summary>
        [DataField("pickupSound")]
        public SoundSpecifier? PickupSound { get; set; }
    }
}
