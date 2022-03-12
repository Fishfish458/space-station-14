using System.Threading;
using Content.Server.DoAfter;
using Content.Server.Medical.Components;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.MobState.Components;
using Robust.Server.GameObjects;
using Content.Server.Defib.Components;
using Content.Server.DoAfter;
using Content.Server.Hands.Components;
using Content.Server.Hands.Systems;
using Content.Server.Weapon.Melee;
using Content.Server.Wieldable.Components;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Content.Server.TetheredItem;
using Robust.Shared.Containers;
using System.Linq;
using Content.Server.Act;
using Content.Server.Administration.Logs;
using Content.Server.Hands.Components;
using Content.Server.Popups;
using Content.Server.Stack;

using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Inventory;
using Content.Shared.Sound;

namespace Content.Server.Defib
{
    public sealed class DefibSystem : EntitySystem
    {
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly HandVirtualItemSystem _virtualItemSystem = default!;
        [Dependency] private readonly InventorySystem _inventorySystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DefibComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<DefibComponent, ActivateInWorldEvent>(HandleActivateInWorld);
            SubscribeLocalEvent<DefibPaddlesComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<TargetScanSuccessfulEvent>(OnTargetScanSuccessful);
            SubscribeLocalEvent<ScanCancelledEvent>(OnScanCancelled);
        }


        private void OnComponentInit(EntityUid uid, DefibComponent defibComponent, ComponentInit args)
        {
            if (!TryComp<TransformComponent>(defibComponent.Owner, out var transformComp))
                return;
            var paddles = EntityManager.SpawnEntity("DefibPaddles", transformComp.Coordinates);

            var item = EntityManager.EnsureComponent<TetheredItemComponent>(paddles);

            if (TryComp<TetheredItemHostComponent>(defibComponent.Owner, out var tetherHost))
            {
                tetherHost.GuardianContainer = uid.EnsureContainer<ContainerSlot>("ItemTetherContainer");
                tetherHost.GuardianContainer.Insert(paddles);
                tetherHost.HostedItem = paddles;
                item.TetherHost = uid;
            }
        }

        private void HandleActivateInWorld(EntityUid uid, DefibComponent defibComponent, ActivateInWorldEvent args)
        {
            if (!TryComp<TransformComponent>(defibComponent.Owner, out var transformComp))
                return;

            if (!TryComp<TetheredItemHostComponent>(defibComponent.Owner, out var tetherHost))
                return;




            if (!_inventorySystem.TryGetSlotEntity(args.User, "back", out var slotEntity))
            {
                // plyEnt.PopupMessage(Loc.GetString("hands-system-missing-equipment-slot", ("slotName", equipmentSlot)));
                return;
            }

            if (slotEntity.Value != uid) {
                // Loc.GetString("seed-extractor-component-interact-message")
                _popupSystem.PopupCursor("MUST BE WORN ON BACK",
                Filter.Entities(args.User));
                return;
            }

            if (tetherHost.HostedItem is not EntityUid cast)
                return;

            if (!tetherHost.GuardianContainer.Remove(cast))
                return;

            if (TryComp<HandsComponent?>(args.User, out var hands) &&
                TryComp<SharedItemComponent?>(cast, out var item))
            {
                if (hands.CanPutInHand(item))
                {
                    hands.PutInHand(item);
                    if (TryComp<TetheredItemComponent>(cast, out var tetherItem))
                    {
                        tetherItem.Stored = false;
                    }
                }
            }

            tetherHost.GuardianContainer.Remove(cast);
        }


        public void UpdateScannedUser(EntityUid uid, EntityUid user, EntityUid? target, DefibPaddlesComponent? defib)
        {
            if (!Resolve(uid, ref defib))
                return;

            if (target == null)
                return;

            if (!TryComp<DamageableComponent>(target, out var damageable))
                return;

                // body system on relay input might need to change
            // GET MIND TIME OF DEATH
            // MIND UNVISIT
            if (!TryComp<MobStateComponent>(target, out var mobState))
                return;

            _damageableSystem.TryChangeDamage(target.Value, defib.Damage, true);
            mobState.TryGetState(damageable.TotalDamage, out var newstate, out var threshold);
            if (newstate is not null)
            {
            SoundSpecifier TurnOnSound = new SoundPathSpecifier("/Audio/Items/Medical/defib_zap.ogg");
            SoundSystem.Play(Filter.Pvs(uid), TurnOnSound.GetSound(), uid);
            SoundSpecifier success = new SoundPathSpecifier("/Audio/Items/Medical/defib_success.ogg");
            SoundSystem.Play(Filter.Pvs(uid), success.GetSound(), uid);
                mobState.SetMobState(null, (newstate, threshold));
            }
        }

        private void OnAfterInteract(EntityUid uid, DefibPaddlesComponent defibComponent, AfterInteractEvent args)
        {

            // IF CHARGED
            // IF TARGET IS MOB
            // IF target is dead
            //
            if (defibComponent.CancelToken != null)
            {
                defibComponent.CancelToken.Cancel();
                defibComponent.CancelToken = null;
                return;
            }

            if (args.Target == null)
                return;

            if (!args.CanReach)
                return;

            if (defibComponent.CancelToken != null)
                return;

            if (!HasComp<MobStateComponent>(args.Target))
                return;

            defibComponent.CancelToken = new CancellationTokenSource();
            SoundSpecifier TurnOnSound = new SoundPathSpecifier("/Audio/Items/Medical/defib_charge.ogg");
            SoundSystem.Play(Filter.Pvs(uid), TurnOnSound.GetSound(), uid);
            _doAfterSystem.DoAfter(new DoAfterEventArgs(args.User, defibComponent.defibDelay, defibComponent.CancelToken.Token, target: args.Target)
            {
                BroadcastFinishedEvent = new TargetScanSuccessfulEvent(args.User, args.Target, defibComponent),
                BroadcastCancelledEvent = new ScanCancelledEvent(defibComponent),
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
                BreakOnStun = true,
                NeedHand = true
            });
        }

        private void OnTargetScanSuccessful(TargetScanSuccessfulEvent args)
        {
            args.Component.CancelToken = null;
            UpdateScannedUser(args.Component.Owner, args.User, args.Target, args.Component);
        }


        private static void OnScanCancelled(ScanCancelledEvent args)
        {
            args.Defib.CancelToken = null;
        }

        private sealed class ScanCancelledEvent : EntityEventArgs
        {
            public readonly DefibPaddlesComponent Defib;
            public ScanCancelledEvent(DefibPaddlesComponent defib)
            {
                Defib = defib;
            }
        }

        private sealed class TargetScanSuccessfulEvent : EntityEventArgs
        {
            public EntityUid User { get; }
            public EntityUid? Target { get; }
            public DefibPaddlesComponent Component { get; }

            public TargetScanSuccessfulEvent(EntityUid user, EntityUid? target, DefibPaddlesComponent component)
            {
                User = user;
                Target = target;
                Component = component;
            }
        }
    }
}
