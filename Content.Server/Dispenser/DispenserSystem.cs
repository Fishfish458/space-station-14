using Content.Server.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Content.Shared.Dispenser;
using Content.Server.Popups;
using Content.Shared.Examine;
using Robust.Shared.Player;
using Content.Shared.Item;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Dispenser.EntitySystems
{
    [UsedImplicitly]
    public sealed partial class DispenserSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<DispenserComponent, ActivateInWorldEvent>(OnActivate);
            SubscribeLocalEvent<DispenserComponent, InteractUsingEvent>(OnInteractUsing);
            SubscribeLocalEvent<DispenserComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<DispenserComponent, GetVerbsEvent<AlternativeVerb>>(AddAltVerb);
        }

        private void OnActivate(EntityUid uid, DispenserComponent dispenseComp, ActivateInWorldEvent args)
        {
            DispenseItem(uid, args.User, dispenseComp);
        }

        private void OnInteractUsing(EntityUid uid, DispenserComponent dispenseComp, InteractUsingEvent args)
        {
            if (dispenseComp.RefillAmount != null && TryPrototype(args.Used, out EntityPrototype? proto) && proto.ID == dispenseComp.RefillPrototype)
            {
                _handsSystem.TryDrop(args.User);
                 EntityManager.DeleteEntity(args.Used);
                RefillDispenser(uid, dispenseComp.RefillAmount.Value, dispenseComp);
            }

            if (dispenseComp.InsertWhitelist != null && dispenseComp.InsertWhitelist.IsValid(args.Used))
                RefillDispenser(uid, 1, dispenseComp);
        }

        private void OnExamined(EntityUid uid, DispenserComponent dispenseComp, ExaminedEvent args)
        {
            if (dispenseComp.ItemCount <= 0)
                args.PushText(Loc.GetString("dispenser-component-examine-count-empty", ("dispenser", uid)));
            else
                args.PushText(Loc.GetString("dispenser-component-examine-count-multiple", ("dispenser", uid), ("count", dispenseComp.ItemCount), ("item", dispenseComp.PrototypeDispensed)));
        }

        private void AddAltVerb(EntityUid uid, DispenserComponent dispenseComp, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanInteract)
                return;

            AlternativeVerb verb = new()
            {
                Act = () =>
                {
                    DispenseItem(uid, args.User, dispenseComp);
                },
                Text = Loc.GetString("dispenser-component-verb-pickup", ("item", dispenseComp.PrototypeDispensed)),
                Priority = 2
            };
            args.Verbs.Add(verb);
        }

        public bool DispenseItem(EntityUid uid, EntityUid user, DispenserComponent dispenseComp)
        {
            if (dispenseComp.PrototypeDispensed == null)
                return false;

            if (dispenseComp.ItemCount <= 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("dispenser-component-examine-count"),
                    uid, Filter.Entities(user));
                return false;
            }

            if (!TryComp(uid, out TransformComponent? transformComp))
            return false;

            if (!TryComp<HandsComponent>(user, out var hands))
            {
                _popupSystem.PopupEntity(Loc.GetString("playing-card-deck-component-pickup-card-full-hand-fail"),
                uid, Filter.Entities(user));
                return false;
            }

            EntityUid dispensedItem = EntityManager.SpawnEntity(dispenseComp.PrototypeDispensed, transformComp.Coordinates);

            // what the heck did you just create?
            if (!TryComp(dispensedItem, out SharedItemComponent? item))
                return false;

            if (!_handsSystem.TryPickup(user, dispensedItem, hands.ActiveHand?.Name))
            {
                EntityManager.DeleteEntity(dispensedItem);
                return false;
            }
            dispenseComp.ItemCount--;

            if (dispenseComp.PickupSound != null)
                SoundSystem.Play(Filter.Pvs(uid), dispenseComp.PickupSound.GetSound(), uid);

            return true;
        }
        public void RefillDispenser(EntityUid uid, uint amount, DispenserComponent dispenseComp)
        {
            dispenseComp.ItemCount += amount;
        }
    }
}
