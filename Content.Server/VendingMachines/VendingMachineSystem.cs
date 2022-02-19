using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Shared.Access.Systems;
using Content.Shared.VendingMachines;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

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
    }
}
