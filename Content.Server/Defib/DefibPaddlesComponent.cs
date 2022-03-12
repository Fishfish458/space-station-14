using Content.Shared.Sound;
using System.Threading;
using Content.Shared.Damage;

namespace Content.Server.Defib.Components
{
    /// <summary>
    ///     Component for defibrillator
    /// </summary>
    [RegisterComponent]
    public sealed class DefibPaddlesComponent : Component
    {
        // [ViewVariables(VVAccess.ReadWrite)]
        // [DataField("wattage")]
        // public float Wattage { get; set; } = 3f;
                // Damage that will be healed on a success
        public CancellationTokenSource? CancelToken;
        [DataField("defibDelay")]
        [ViewVariables]
        public float defibDelay = 0.8f;

        [DataField("damage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier Damage = default!;
        // [ViewVariables(VVAccess.ReadWrite)] [DataField("turnOnSound")] public SoundSpecifier TurnOnSound = new SoundPathSpecifier("/Audio/Items/flashlight_on.ogg");
        // [ViewVariables(VVAccess.ReadWrite)] [DataField("turnOnFailSound")] public SoundSpecifier TurnOnFailSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");
        // [ViewVariables(VVAccess.ReadWrite)] [DataField("turnOffSound")] public SoundSpecifier TurnOffSound = new SoundPathSpecifier("/Audio/Items/flashlight_off.ogg");
    }
}
