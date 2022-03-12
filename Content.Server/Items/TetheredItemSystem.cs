using Content.Server.DoAfter;
using Content.Server.Hands.Components;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Interaction;

using Robust.Shared.Player;
using Robust.Shared.Utility;

using Content.Shared.Throwing;



namespace Content.Server.TetheredItem
{
    /// <summary>
    /// A guardian has a host it's attached to that it fights for. A fighting spirit.
    /// </summary>
    public sealed class TetheredItemSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<TetheredItemComponent, MoveEvent>(OnGuardianMove);
            SubscribeLocalEvent<TetheredItemHostComponent, MoveEvent>(OnHostMove);
            SubscribeLocalEvent<TetheredItemComponent, ThrowAttemptEvent>(OnThrowAttempt);
            // SubscribeLocalEvent<TetheredItemComponent, MobStateChangedEvent>()
            SubscribeLocalEvent<TetheredItemComponent, DroppedEvent>(OnDroppedEvent);

        }


        /// <summary>
        /// Called every time the host moves, to make sure the distance between the host and the guardian isn't too far
        /// </summary>
        private void OnHostMove(EntityUid uid, TetheredItemHostComponent component, ref MoveEvent args)
        {
            if (component.HostedItem == null ||
                !TryComp(component.HostedItem, out TetheredItemComponent? tetheredItem) ||
                tetheredItem.Stored) return;

            CheckGuardianMove(uid, component.HostedItem.Value, component);
        }

        /// <summary>
        /// Called every time the guardian moves: makes sure it's not out of it's allowed distance
        /// </summary>
        private void OnGuardianMove(EntityUid uid, TetheredItemComponent component, ref MoveEvent args)
        {
            if (component.Stored) return;

            CheckGuardianMove(component.TetherHost, uid, tetheredItemComponent: component);
        }

         /// <summary>
        /// Called every time the guardian moves: makes sure it's not out of it's allowed distance
        /// </summary>
        private void OnThrowAttempt(EntityUid uid, TetheredItemComponent component, ThrowAttemptEvent args)
        {
            if (component.Stored) return;

            if (!TryComp<TetheredItemHostComponent>(component.TetherHost, out var tetherHost))
                return;

            RetractGuardian(tetherHost, component);
        }

        /// <summary>
        /// Called every time the guardian moves: makes sure it's not out of it's allowed distance
        /// </summary>
        private void OnDroppedEvent(EntityUid uid, TetheredItemComponent component, DroppedEvent args)
        {
            if (component.Stored) return;

            if (!TryComp<TetheredItemHostComponent>(component.TetherHost, out var tetherHost))
                return;

            RetractGuardian(tetherHost, component);
        }

        /// <summary>
        /// Retract the guardian if either the host or the guardian move away from each other.
        /// </summary>
        private void CheckGuardianMove(
            EntityUid hostUid,
            EntityUid guardianUid,
            TetheredItemHostComponent? tetherHost = null,
            TetheredItemComponent? tetheredItemComponent = null,
            TransformComponent? hostXform = null,
            TransformComponent? guardianXform = null)
        {
            if (!Resolve(hostUid, ref hostXform, ref tetherHost) ||
                !Resolve(guardianUid, ref tetheredItemComponent, ref guardianXform))
            {
                return;
            }

            if (tetheredItemComponent.Stored) return;

            if (!guardianXform.Coordinates.InRange(EntityManager, hostXform.Coordinates, tetheredItemComponent.DistanceAllowed))
            {
                // RETRACT
                RetractGuardian(tetherHost, tetheredItemComponent);
            }
        }

        private void RetractGuardian(TetheredItemHostComponent hostComponent, TetheredItemComponent tetheredItemComponent)
        {
            if (tetheredItemComponent.Stored)
            {
                DebugTools.Assert(hostComponent.GuardianContainer.Contains(tetheredItemComponent.Owner));
                return;
            }

            hostComponent.GuardianContainer.Insert(tetheredItemComponent.Owner);
            DebugTools.Assert(hostComponent.GuardianContainer.Contains(tetheredItemComponent.Owner));
            _popupSystem.PopupEntity(Loc.GetString("tethered-entity-recall"), hostComponent.Owner, Filter.Pvs(hostComponent.Owner));
            tetheredItemComponent.Stored = true;
        }

        // private sealed class GuardianCreatorInjectCancelledEvent : EntityEventArgs
        // {
        //     public EntityUid Target { get; }
        //     public GuardianCreatorComponent Component { get; }

        //     public GuardianCreatorInjectCancelledEvent(EntityUid target, GuardianCreatorComponent component)
        //     {
        //         Target = target;
        //         Component = component;
        //     }
        // }
    }
}
