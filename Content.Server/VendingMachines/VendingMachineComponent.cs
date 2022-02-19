using System;
using Content.Server.UserInterface;
using Content.Server.WireHacking;
using Content.Shared.Sound;
using Content.Shared.VendingMachines;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;
using Content.Server.VendingMachines.systems;
using static Content.Shared.Wires.SharedWiresComponent;

namespace Content.Server.VendingMachines
{
    [RegisterComponent]
    public sealed class VendingMachineComponent : SharedVendingMachineComponent, IWires
    {
        [DataField("pack")]
        public string PackPrototypeId = string.Empty;
        public string SpriteName = "";

        public enum Wires
        {
            /// <summary>
            /// Shoots a random item when pulsed.
            /// </summary>
            Limiter
        }
        public void RegisterWires(WiresComponent.WiresBuilder builder)
        {
            builder.CreateWire(Wires.Limiter);
        }

        public void WiresUpdate(WiresUpdateEventArgs args)
        {
            var identifier = (Wires) args.Identifier;
            if (identifier == Wires.Limiter && args.Action == WiresAction.Pulse)
            {
                EntitySystem.Get<VendingMachineSystem>().EjectRandom(this.Owner, true, this);
            }
        }
    }

    public sealed class WiresUpdateEventArgs : EventArgs
    {
        public readonly object Identifier;
        public readonly WiresAction Action;

        public WiresUpdateEventArgs(object identifier, WiresAction action)
        {
            Identifier = identifier;
            Action = action;
        }
    }

    public interface IWires
    {
        void RegisterWires(WiresComponent.WiresBuilder builder);
        void WiresUpdate(WiresUpdateEventArgs args);
    }
}

