using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.UserInterface;
using Content.Server.WireHacking;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Acts;
using Content.Shared.Interaction;
using Content.Shared.Sound;
using Content.Shared.VendingMachines;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;
using System;
using System.Threading;
using System.Collections.Generic;
using Content.Server.Atmos.Monitor.Systems;
using Content.Server.DeviceNetwork.Components;
using Content.Server.Power.Components;
using Content.Server.VendingMachines; // TODO: Move this out of vending machines???
using Content.Server.WireHacking;
using Content.Shared.Atmos.Monitor.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.ViewVariables;

using static Content.Shared.Wires.SharedWiresComponent;
using static Content.Shared.Wires.SharedWiresComponent.WiresAction;

namespace Content.Server.VendingMachines
{
    [RegisterComponent]
    [ComponentReference(typeof(IActivate))]
    public class VendingMachineComponent : SharedVendingMachineComponent, IBreakAct, IWires
    {
        [Dependency] public readonly IEntityManager _entMan = default!;
        [Dependency] public readonly IRobustRandom _random = default!;
        [Dependency] public readonly IPrototypeManager _prototypeManager = default!;

        public bool _ejecting;
        public TimeSpan _animationDuration = TimeSpan.Zero;
        [DataField("pack")]
        public string _packPrototypeId = string.Empty;
        public string _spriteName = "";

        public bool Powered => !_entMan.TryGetComponent(Owner, out ApcPowerReceiverComponent? receiver) || receiver.Powered;
        public bool _broken;

        [DataField("soundVend")]
        // Grabbed from: https://github.com/discordia-space/CEV-Eris/blob/f702afa271136d093ddeb415423240a2ceb212f0/sound/machines/vending_drop.ogg
        public SoundSpecifier soundVend = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");
        [DataField("soundDeny")]
        // Yoinked from: https://github.com/discordia-space/CEV-Eris/blob/35bbad6764b14e15c03a816e3e89aa1751660ba9/sound/machines/Custom_deny.ogg
        public SoundSpecifier _soundDeny = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

        [ViewVariables] public BoundUserInterface? UserInterface => Owner.GetUIOrNull(VendingMachineUiKey.Key);

        public bool Broken => _broken;

        public void OnUiReceiveMessage(ServerBoundUserInterfaceMessage serverMsg)
        {
            if (!Powered)
                return;

            var message = serverMsg.Message;
            switch (message)
            {
                case VendingMachineEjectMessage msg:
                    TryEject(msg.ID, serverMsg.Session.AttachedEntity);
                    break;
                case InventorySyncRequestMessage _:
                    UserInterface?.SendMessage(new VendingMachineInventoryMessage(Inventory));
                    break;
            }
        }

        private void TryDispense(string id)
        {
            if (_ejecting || _broken)
            {
                return;
            }

            var entry = Inventory.Find(x => x.ID == id);
            if (entry == null)
            {
                Owner.PopupMessageEveryone(Loc.GetString("vending-machine-component-try-eject-invalid-item"));
                Deny();
                return;
            }

            if (entry.Amount <= 0)
            {
                Owner.PopupMessageEveryone(Loc.GetString("vending-machine-component-try-eject-out-of-stock"));
                Deny();
                return;
            }
            if (entry.ID != null) { // If this item is not a stored entity, eject as a new entity of type
                TryEjectVendorItem(entry);
                return;
            }
            return;
        }

        // public bool IsAuthorized(EntityUid? sender)
        // {
        //     if (_entMan.TryGetComponent<AccessReaderComponent?>(Owner, out var accessReader))
        //     {
        //         var accessSystem = EntitySystem.Get<AccessReaderSystem>();
        //         if (sender == null || !accessSystem.IsAllowed(accessReader, sender.Value))
        //         {
        //             Owner.PopupMessageEveryone(Loc.GetString("vending-machine-component-try-eject-access-denied"));
        //             Deny();
        //             return;
        //         }
        //     }
        //     return true;
        // }

        // private void AuthorizedVend(string id, EntityUid? sender)
        // {
        //     if (IsAuthorized(sender))
        //     {
        //         TryDispense(id);
        //     }
        //     return;
        // }


        private enum Wires
        {
            // Cutting this kills power.
            // Pulsing it disrupts power.
            Power,
            // Cutting this allows full access.
            // Pulsing this does nothing.
            Access,
            /// Shoots a random item when pulsed.
            Shoot,
            // // Cutting this clears sync'd devices, and makes
            // // the alarm unable to resync.
            // // Pulsing this resyncs all devices (ofc current
            // // implementation just auto-does this anyways)
            // DeviceSync,
            // Cutting stops ads from playing
            // Pulsing causes to play immediately
            Advertisement
        }

        public void RegisterWires(WiresComponent.WiresBuilder builder)
        {
            foreach (var wire in Enum.GetValues<Wires>())
                builder.CreateWire(wire);

            UpdateWires();
        }

        public void UpdateWires()
        {
            if (_airAlarmSystem == null)
                _airAlarmSystem = EntitySystem.Get<AirAlarmSystem>();

            if (WiresComponent == null) return;

            var pwrLightState = (PowerPulsed, PowerCut) switch {
                (true, false) => StatusLightState.BlinkingFast,
                (_, true) => StatusLightState.Off,
                (_, _) => StatusLightState.On
            };

            var powerLight = new StatusLightData(Color.Yellow, pwrLightState, "POWR");

            var accessLight = new StatusLightData(
                Color.Green,
                WiresComponent.IsWireCut(Wires.Access) ? StatusLightState.Off : StatusLightState.On,
                "ACC"
            );

            var panicLight = new StatusLightData(
                Color.Red,
                CurrentMode == AirAlarmMode.Panic ? StatusLightState.On : StatusLightState.Off,
                "PAN"
            );

            var syncLightState = StatusLightState.BlinkingSlow;

            if (AtmosMonitorComponent != null && !AtmosMonitorComponent.NetEnabled)
                syncLightState = StatusLightState.Off;
            else if (DeviceData.Count != 0)
                syncLightState = StatusLightState.On;

            var syncLight = new StatusLightData(Color.Orange, syncLightState, "NET");

            WiresComponent.SetStatus(AirAlarmWireStatus.Power, powerLight);
            WiresComponent.SetStatus(AirAlarmWireStatus.Access, accessLight);
            WiresComponent.SetStatus(AirAlarmWireStatus.Panic, panicLight);
            WiresComponent.SetStatus(AirAlarmWireStatus.DeviceSync, syncLight);
        }

        private bool _powerCut;
        private bool PowerCut
        {
            get => _powerCut;
            set
            {
                _powerCut = value;
                SetPower();
            }
        }

        private bool _powerPulsed;
        private bool PowerPulsed
        {
            get => _powerPulsed && !_powerCut;
            set
            {
                _powerPulsed = value;
                SetPower();
            }
        }

        private void SetPower()
        {
            if (DeviceRecvComponent != null
                && WiresComponent != null)
                DeviceRecvComponent.PowerDisabled = PowerPulsed || PowerCut;
        }

        public void WiresUpdate(WiresUpdateEventArgs args)
        {
            if (DeviceNetComponent == null) return;

            if (_airAlarmSystem == null)
                _airAlarmSystem = EntitySystem.Get<AirAlarmSystem>();

            switch (args.Action)
            {
                case Pulse:
                    switch (args.Identifier)
                    {
                        case Wires.Power:
                            PowerPulsed = true;
                            _powerPulsedCancel.Cancel();
                            _powerPulsedCancel = new CancellationTokenSource();
                            Owner.SpawnTimer(TimeSpan.FromSeconds(PowerPulsedTimeout),
                                () => PowerPulsed = false,
                                _powerPulsedCancel.Token);
                            break;
                        case Wires.Panic:
                            if (CurrentMode != AirAlarmMode.Panic)
                                _airAlarmSystem.SetMode(Owner, DeviceNetComponent.Address, AirAlarmMode.Panic, true, false);
                            break;
                        case Wires.DeviceSync:
                            _airAlarmSystem.SyncAllDevices(Owner);
                            break;
                    }
                    break;
                case Mend:
                    switch (args.Identifier)
                    {
                        case Wires.Power:
                            _powerPulsedCancel.Cancel();
                            PowerPulsed = false;
                            PowerCut = false;
                            break;
                        case Wires.Panic:
                            if (CurrentMode == AirAlarmMode.Panic)
                                _airAlarmSystem.SetMode(Owner, DeviceNetComponent.Address, AirAlarmMode.Filtering, true, false);
                            break;
                        case Wires.Access:
                            FullAccess = false;
                            break;
                        case Wires.DeviceSync:
                            if (AtmosMonitorComponent != null)
                                AtmosMonitorComponent.NetEnabled = true;

                            break;
                    }
                    break;
                case Cut:
                    switch (args.Identifier)
                    {
                        case Wires.DeviceSync:
                            DeviceData.Clear();
                            if (AtmosMonitorComponent != null)
                            {
                                AtmosMonitorComponent.NetworkAlarmStates.Clear();
                                AtmosMonitorComponent.NetEnabled = false;
                            }

                            break;
                        case Wires.Power:
                            PowerCut = true;
                            break;
                        case Wires.Access:
                            FullAccess = true;
                            break;
                    }
                    break;
            }

            UpdateWires();
        }

        public enum Wires
        {
            /// <summary>
            /// Shoots a random item when pulsed.
            /// </summary>
            Shoot
        }

        void IWires.RegisterWires(WiresComponent.WiresBuilder builder)
        {
            builder.CreateWire(Wires.Shoot);
        }

        void IWires.WiresUpdate(WiresUpdateEventArgs args)
        {
            var identifier = (Wires) args.Identifier;
            if (identifier == Wires.Shoot && args.Action == WiresAction.Pulse)
            {
                EjectRandom();
            }
        }

        /// <summary>
        /// Ejects a random item if present.
        /// </summary>
        private void EjectRandom()
        {
            var availableItems = Inventory.Where(x => x.Amount > 0).ToList();
            if (availableItems.Count <= 0)
            {
                return;
            }
            TryEject(_random.Pick(availableItems).ID);
        }
    }

    public class WiresUpdateEventArgs : EventArgs
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

