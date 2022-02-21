using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Robust.Server.GameObjects;
using Content.Shared.Item;
using Robust.Shared.Containers;
using Content.Server.Storage.Components;
using System.Linq;
using Content.Server.VendingMachines.Systems;
using Content.Server.VendingMachines;
using Content.Shared.VendingMachines;
using Content.Shared.Acts;

using static Content.Shared.VendingMachines.SharedVendingMachineComponent;

namespace Content.Server.VendingMachine.Systems
{
    public sealed class SmartFridgeSystem : BaseVendingMachineSystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        private uint _nextAllocatedId = 0;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SmartFridgeComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<SmartFridgeComponent, InteractUsingEvent>(OnInteractUsing);
            SubscribeLocalEvent<SmartFridgeComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<SmartFridgeComponent, ActivateInWorldEvent>(HandleActivate);
            SubscribeLocalEvent<SmartFridgeComponent, InventorySyncRequestMessage>(OnInventoryRequestMessage);
            SubscribeLocalEvent<SmartFridgeComponent, VendingMachineEjectMessage>(OnInventoryEjectMessage);
            SubscribeLocalEvent<SmartFridgeComponent, BreakageEventArgs>(OnBreak);
        }

        private void OnComponentInit(EntityUid uid, SmartFridgeComponent component, ComponentInit args)
        {
            base.Initialize();

            if (TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
            {
                TryUpdateVisualState(uid, null, component);
            }
            component.Storage = uid.EnsureContainer<Container>("fridge_entity_container");
        }

        private void OnInteractUsing(EntityUid uid, SmartFridgeComponent fridgeComponent, InteractUsingEvent args)
        {
            if (TryComp<ServerStorageComponent>(args.Used, out ServerStorageComponent? storageComponent))
            {
                TryInsertFromStorage(uid, storageComponent, fridgeComponent);
            }
            else
            {
                TryInsertVendorItem(uid, args.Used, fridgeComponent);
            }
        }

        public void TryInsertFromStorage(EntityUid uid, ServerStorageComponent storageComponent, SmartFridgeComponent fridgeComponent)
        {
            if (storageComponent.StoredEntities == null)
                return;

            var storagedEnts = storageComponent.StoredEntities.ToArray();
            uint addedCount = 0;
            foreach (var ent in storagedEnts)
            {
                bool insertSuccess = TryInsertVendorItem(uid, ent, fridgeComponent);
                if (insertSuccess)
                    addedCount++;
            }
            if (addedCount > 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("smart-fridge-component-storage-insert-success", ("count", addedCount)), uid, Filter.Pvs(uid));
            }
        }

        public bool TryInsertVendorItem(EntityUid uid, EntityUid itemUid, SmartFridgeComponent fridgeComponent)
        {
            if (fridgeComponent.Storage == null)
                return false;

            if (fridgeComponent.Whitelist != null && !fridgeComponent.Whitelist.IsValid(itemUid))
                return false;

            if (!TryComp<SharedItemComponent>(itemUid, out SharedItemComponent? item))
                return false;

            TryComp<MetaDataComponent>(itemUid, out MetaDataComponent? metaData);
            string name = metaData == null? "Unknown" : metaData.EntityName;

            bool matchedEntry = false;
            foreach (var inventoryItem in fridgeComponent.Inventory)
            {
                if (name == inventoryItem.Name)
                {
                    var listedItem = fridgeComponent.Inventory.Find(x => x.ID == inventoryItem.ID);
                    if (listedItem != null)
                    {
                        matchedEntry = true;
                        listedItem.Amount++;
                        fridgeComponent.entityReference[listedItem.ID].Enqueue(itemUid);
                        break;
                    }
                }
            }

            if (!matchedEntry)
            {
                string itemID = _nextAllocatedId++.ToString();
                VendingMachineInventoryEntry newEntry = new VendingMachineInventoryEntry(itemID, name, 1);
                fridgeComponent.entityReference.Add(itemID, new Queue<EntityUid>(new[] {itemUid}));
                fridgeComponent.Inventory.Add(newEntry);
            }

            fridgeComponent.Storage.Insert(itemUid);
            fridgeComponent.UserInterface?.SendMessage(new VendingMachineInventoryMessage(fridgeComponent.Inventory));
            return true;
        }

        public override void ToggleInterface(EntityUid uid, ActorComponent actor, SharedVendingMachineComponent component)
        {
            if (TryComp<SmartFridgeComponent>(uid, out SmartFridgeComponent? smartFridge))
                smartFridge.UserInterface?.Toggle(actor.PlayerSession);
        }

        public override void SendInventoryMessage(EntityUid uid, SharedVendingMachineComponent component)
        {
            if(TryComp<SmartFridgeComponent>(uid, out SmartFridgeComponent? smartFridge))
                smartFridge.UserInterface?.SendMessage(new VendingMachineInventoryMessage(component.Inventory));
        }

        public override void TryEjectVendorItem(EntityUid uid, string itemId, bool throwItem, SharedVendingMachineComponent? vendComponent = null)
        {
            if (!TryComp<SmartFridgeComponent>(uid, out SmartFridgeComponent? fridgeComponent))
                return;

            if (fridgeComponent.Storage == null || fridgeComponent.Ejecting || fridgeComponent.Inventory == null || fridgeComponent.Inventory.Count == 0 || fridgeComponent.Broken || !IsPowered(uid, fridgeComponent))
                return;

            VendingMachineInventoryEntry? entry = fridgeComponent.Inventory.Find(x => x.ID == itemId);

            if (entry == null)
            {
                _popupSystem.PopupEntity(Loc.GetString("smart-fridge-component-try-eject-invalid-item"), uid, Filter.Pvs(uid));
                Deny(uid, fridgeComponent);
                return;
            }

            if (entry.Amount <= 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("smart-fridge-component-try-eject-out-of-stock"), uid, Filter.Pvs(uid));
                Deny(uid, fridgeComponent);
                return;
            }

            fridgeComponent.Ejecting = true;
            entry.Amount--;
            EntityUid targetEntity = fridgeComponent.entityReference[itemId].Dequeue();

            if (entry.Amount == 0 || targetEntity == null)
            {
                fridgeComponent.Inventory.Remove(entry);
                fridgeComponent.entityReference.Remove(itemId);
            }

            fridgeComponent.UserInterface?.SendMessage(new VendingMachineInventoryMessage(fridgeComponent.Inventory));
            TryUpdateVisualState(uid, VendingMachineVisualState.Eject, fridgeComponent);
            fridgeComponent.Owner.SpawnTimer(fridgeComponent.AnimationDuration, () =>
            {
                fridgeComponent.Ejecting = false;
                TryUpdateVisualState(uid, VendingMachineVisualState.Normal, fridgeComponent);
                fridgeComponent.Storage.Remove(targetEntity);
            });
            SoundSystem.Play(Filter.Pvs(fridgeComponent.Owner), fridgeComponent.SoundVend.GetSound(), fridgeComponent.Owner, AudioParams.Default.WithVolume(-2f));
        }
    }
}
