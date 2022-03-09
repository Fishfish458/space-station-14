using Content.Shared.Actions.ActionTypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Defib
{
    [Serializable, NetSerializable]
    public enum DefibVisuals
    {
        Power
    }

    [Serializable, NetSerializable]
    public enum DefibPowerStates
    {
        FullPower,
        LowPower,
        Dying,
    }


}
