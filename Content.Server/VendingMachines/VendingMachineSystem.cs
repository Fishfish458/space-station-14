using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Shared.Access.Systems;
using Content.Shared.VendingMachines;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Shared.Interaction;
using Content.Shared.Acts;

using Content.Server.VendingMachines.Systems;
using static Content.Shared.VendingMachines.SharedVendingMachineComponent;
using Content.Shared.Throwing;

namespace Content.Server.VendingMachines.systems
{
    public sealed class VendingMachineSystem : BaseVendingMachineSystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly AccessReaderSystem _accessReader = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly ThrowingSystem _throwingSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<VendingMachineComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<VendingMachineComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<VendingMachineComponent, ActivateInWorldEvent>(HandleActivate);
            SubscribeLocalEvent<VendingMachineComponent, InventorySyncRequestMessage>(OnInventoryRequestMessage);
            SubscribeLocalEvent<VendingMachineComponent, VendingMachineEjectMessage>(OnInventoryEjectMessage);
            SubscribeLocalEvent<VendingMachineComponent, BreakageEventArgs>(OnBreak);
        }

        private void OnComponentInit(EntityUid uid, VendingMachineComponent component, ComponentInit args)
        {
            base.Initialize();

            if (TryComp<ApcPowerReceiverComponent>(component.Owner, out var receiver))
            {
                TryUpdateVisualState(uid, null, component);
            }

            InitializeFromPrototype(uid, component);
        }

        public void InitializeFromPrototype(EntityUid uid, VendingMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            if (string.IsNullOrEmpty(vendComponent.PackPrototypeId)) { return; }

            if (!_prototypeManager.TryIndex(vendComponent.PackPrototypeId, out VendingMachineInventoryPrototype? packPrototype))
            {
                return;
            }

            MetaData(uid).EntityName = packPrototype.Name;
            vendComponent.AnimationDuration = TimeSpan.FromSeconds(packPrototype.AnimationDuration);
            vendComponent.SpriteName = packPrototype.SpriteName;
            if (!string.IsNullOrEmpty(vendComponent.SpriteName))
            {
                if (TryComp<SpriteComponent>(vendComponent.Owner, out var spriteComp)) {
                    const string vendingMachineRSIPath = "Structures/Machines/VendingMachines/{0}.rsi";
                    spriteComp.BaseRSIPath = string.Format(vendingMachineRSIPath, vendComponent.SpriteName);
                }
            }
            var inventory = new List<VendingMachineInventoryEntry>();
            foreach (var (id, amount) in packPrototype.StartingInventory)
            {
                if (!_prototypeManager.TryIndex(id, out EntityPrototype? prototype))
                {
                    continue;
                }
                inventory.Add(new VendingMachineInventoryEntry(id, prototype.Name, amount));
            }
            vendComponent.Inventory = inventory;
        }

        public override void TryEjectVendorItem(EntityUid uid, string itemId, bool throwItem, SharedVendingMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            if (vendComponent.Ejecting || vendComponent.Broken || !IsPowered(uid, vendComponent))
            {
                return;
            }

            var entry = vendComponent.Inventory.Find(x => x.ID == itemId);
            if (entry == null)
            {
                _popupSystem.PopupEntity(Loc.GetString("vending-machine-component-try-eject-invalid-item"), uid, Filter.Pvs(uid));
                Deny(uid, vendComponent);
                return;
            }

            if (entry.Amount <= 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("vending-machine-component-try-eject-out-of-stock"), uid, Filter.Pvs(uid));
                Deny(uid, vendComponent);
                return;
            }

            if (entry.ID == null)
                return;

            if (!TryComp<TransformComponent>(vendComponent.Owner, out var transformComp))
                return;

            // Start Ejecting, and prevent users from ordering while anim playing
            vendComponent.Ejecting = true;
            entry.Amount--;
            SendInventoryMessage(uid, vendComponent);
            TryUpdateVisualState(uid, VendingMachineVisualState.Eject, vendComponent);
            vendComponent.Owner.SpawnTimer(vendComponent.AnimationDuration, () =>
            {
                vendComponent.Ejecting = false;
                TryUpdateVisualState(uid, VendingMachineVisualState.Normal, vendComponent);
                var ent = EntityManager.SpawnEntity(entry.ID, transformComp.Coordinates);
                if (throwItem)
                {
                    float range = vendComponent.NonLimitedEjectRange;
                    Vector2 direction = new Vector2(_random.NextFloat(-range, range), _random.NextFloat(-range, range));
                    _throwingSystem.TryThrow(ent, direction, vendComponent.NonLimitedEjectForce);
                }
            });
            SoundSystem.Play(Filter.Pvs(vendComponent.Owner), vendComponent.SoundVend.GetSound(), vendComponent.Owner, AudioParams.Default.WithVolume(-2f));
        }


        public override void SendInventoryMessage(EntityUid uid, SharedVendingMachineComponent component)
        {

        }

        public override void ToggleInterface(EntityUid uid, ActorComponent actor, SharedVendingMachineComponent component)
        {

        }
    }
}
