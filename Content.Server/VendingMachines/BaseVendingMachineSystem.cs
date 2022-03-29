using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using System.Linq;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.WireHacking;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.VendingMachines;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Content.Shared.Acts;
using static Content.Shared.VendingMachines.SharedVendingMachineComponent;

namespace Content.Server.VendingMachines.Systems
{
    public abstract class BaseVendingMachineSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AccessReaderSystem _accessReader = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;

        public void OnInventoryRequestMessage(EntityUid uid, SharedVendingMachineComponent component, InventorySyncRequestMessage args)
        {
            if (!IsPowered(uid, component))
                return;

            SendInventoryMessage(uid, component);
        }

        public void OnInventoryEjectMessage(EntityUid uid, SharedVendingMachineComponent component, VendingMachineEjectMessage args)
        {
            if (!IsPowered(uid, component))
                return;

            if (args.Session.AttachedEntity is not { Valid: true } entity || Deleted(entity))
                return;

            AuthorizedVend(uid, entity, args.ID, component);
        }

        public void HandleActivate(EntityUid uid, SharedVendingMachineComponent component, ActivateInWorldEvent args)
        {
            if (!TryComp<ActorComponent>(args.User, out var actor))
                return;

            if (!IsPowered(uid, component))
                return;

            if (TryComp<WiresComponent>(uid, out var wires))
            {
                if (wires.IsPanelOpen)
                {
                    wires.OpenInterface(actor.PlayerSession);
                    return;
                }
            }

        }

        public void OnPowerChanged(EntityUid uid, SharedVendingMachineComponent component, PowerChangedEvent args)
        {
            TryUpdateVisualState(uid, null, component);
        }

        public void OnBreak(EntityUid uid, SharedVendingMachineComponent vendComponent, BreakageEventArgs eventArgs)
        {
            vendComponent.Broken = true;
            TryUpdateVisualState(uid, VendingMachineVisualState.Broken, vendComponent);
        }

        public abstract void SendInventoryMessage(EntityUid uid, SharedVendingMachineComponent component);


        public bool IsPowered(EntityUid uid, SharedVendingMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return false;

            if (!TryComp<ApcPowerReceiverComponent>(vendComponent.Owner, out var receiver))
                return false;

            return receiver.Powered;
        }

        public void Deny(EntityUid uid, SharedVendingMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            SoundSystem.Play(Filter.Pvs(vendComponent.Owner), vendComponent.SoundDeny.GetSound(), vendComponent.Owner, AudioParams.Default.WithVolume(-2f));
            // Play the Deny animation
            TryUpdateVisualState(uid, VendingMachineVisualState.Deny, vendComponent);
            //TODO: This duration should be a distinct value specific to the deny animation
            vendComponent.Owner.SpawnTimer(vendComponent.AnimationDuration, () =>
            {
                TryUpdateVisualState(uid, VendingMachineVisualState.Normal, vendComponent);
            });
        }

        public bool IsAuthorized(EntityUid uid, EntityUid? sender, SharedVendingMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return false;

            if (TryComp<AccessReaderComponent?>(vendComponent.Owner, out var accessReader))
            {
                if (sender == null || !_accessReader.IsAllowed(accessReader, sender.Value))
                {
                    _popupSystem.PopupEntity(Loc.GetString("vending-machine-component-try-eject-access-denied"), uid, Filter.Pvs(uid));
                    Deny(uid, vendComponent);
                    return false;
                }
            }
            return true;
        }

        public void AuthorizedVend(EntityUid uid, EntityUid sender, string itemId, SharedVendingMachineComponent component)
        {
            if (IsAuthorized(uid, sender, component))
                TryEjectVendorItem(uid, itemId, component.CanShoot, component);

            return;
        }

        public abstract void TryEjectVendorItem(EntityUid uid, string itemId, bool throwItem, SharedVendingMachineComponent? vendComponent = null);

        public void TryUpdateVisualState(EntityUid uid, VendingMachineVisualState? state = VendingMachineVisualState.Normal, SharedVendingMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            var finalState = state == null ? VendingMachineVisualState.Normal : state;
            if (vendComponent.Broken)
            {
                finalState = VendingMachineVisualState.Broken;
            }
            else if (vendComponent.Ejecting)
            {
                finalState = VendingMachineVisualState.Eject;
            }
            else if (!IsPowered(uid, vendComponent))
            {
                finalState = VendingMachineVisualState.Off;
            }

            if (TryComp<AppearanceComponent>(vendComponent.Owner, out var appearance))
                appearance.SetData(VendingMachineVisuals.VisualState, finalState);

        }

        public void EjectRandom(EntityUid uid, bool throwItem, SharedVendingMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            var availableItems = vendComponent.Inventory.Where(x => x.Amount > 0).ToList();
            if (availableItems.Count <= 0)
                return;

            TryEjectVendorItem(uid, _random.Pick(availableItems).ID, throwItem, vendComponent);
        }
    }
}
