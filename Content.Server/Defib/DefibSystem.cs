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

namespace Content.Server.Defib
{
    public sealed class DefibSystem : EntitySystem
    {
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly HandVirtualItemSystem _virtualItemSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DefibComponent, ActivateInWorldEvent>(HandleActivateInWorld);
            SubscribeLocalEvent<DefibComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<TargetScanSuccessfulEvent>(OnTargetScanSuccessful);
            SubscribeLocalEvent<ScanCancelledEvent>(OnScanCancelled);
        }

        private void HandleActivateInWorld(EntityUid uid, DefibComponent defibComponent, ActivateInWorldEvent args)
        {
            if (!TryComp<TransformComponent>(defibComponent.Owner, out var transformComp))
                return;

            var paddles = EntityManager.SpawnEntity("DefibPaddles", transformComp.Coordinates);
            _virtualItemSystem.TrySpawnVirtualItemInHand(paddles, args.User);
        }

        private void OnAfterInteract(EntityUid uid, DefibComponent defibComponent, AfterInteractEvent args)
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
            // UpdateScannedUser(args.Component.Owner, args.User, args.Target, args.Component);
        }


        private static void OnScanCancelled(ScanCancelledEvent args)
        {
            args.Defib.CancelToken = null;
        }

        private sealed class ScanCancelledEvent : EntityEventArgs
        {
            public readonly DefibComponent Defib;
            public ScanCancelledEvent(DefibComponent defib)
            {
                Defib = defib;
            }
        }

        private sealed class TargetScanSuccessfulEvent : EntityEventArgs
        {
            public EntityUid User { get; }
            public EntityUid? Target { get; }
            public DefibComponent Component { get; }

            public TargetScanSuccessfulEvent(EntityUid user, EntityUid? target, DefibComponent component)
            {
                User = user;
                Target = target;
                Component = component;
            }
        }
    }
}
