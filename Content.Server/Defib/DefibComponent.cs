using Content.Shared.Sound;
using System.Threading;

namespace Content.Server.Defib.Components
{
    /// <summary>
    ///     Component for defibrillator
    /// </summary>
    [RegisterComponent]
    public sealed class DefibComponent : Component
    {
        // [ViewVariables(VVAccess.ReadWrite)]
        // [DataField("wattage")]
        // public float Wattage { get; set; } = 3f;
        [DataField("defibDelay")]
        [ViewVariables]
        public float defibDelay = 0.8f;
        [DataField("rechargeTime")]
        [ViewVariables]
        public float rechargeTime = 0.8f;
        public CancellationTokenSource? CancelToken;
        // [ViewVariables(VVAccess.ReadWrite)] [DataField("turnOnSound")] public SoundSpecifier TurnOnSound = new SoundPathSpecifier("/Audio/Items/flashlight_on.ogg");
        // [ViewVariables(VVAccess.ReadWrite)] [DataField("turnOnFailSound")] public SoundSpecifier TurnOnFailSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");
        // [ViewVariables(VVAccess.ReadWrite)] [DataField("turnOffSound")] public SoundSpecifier TurnOffSound = new SoundPathSpecifier("/Audio/Items/flashlight_off.ogg");
    }
}
