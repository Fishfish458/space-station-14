using System.Collections.Generic;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Light.Components;
using Content.Shared.Audio;
using Content.Shared.Interaction;
using Content.Shared.Smoking;
using Content.Shared.Temperature;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using System;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.UserInterface;
using Content.Server.WireHacking;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Acts;
using Content.Shared.Sound;
using Content.Shared.VendingMachines;
using Robust.Server.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;
using Robust.Shared.Containers;
using Content.Shared.Whitelist;


using Content.Shared.Item;


using static Content.Shared.VendingMachines.SharedVendingMachineComponent;
namespace Content.Server.VendingMachines
{
    public sealed class CustomVendingMachineSystem : EntitySystem
    {

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<CustomVendingMachineComponent, InteractUsingEvent>(OnInteractUsing);
            SubscribeLocalEvent<CustomVendingMachineComponent, ActivateInWorldEvent>(HandleActivate);
            SubscribeLocalEvent<CustomVendingMachineComponent, PowerChangedEvent>(OnPowerChanged);

        }

        private void OnComponentInit(EntityUid uid, CustomVendingMachineComponent component, ComponentInit args)
        {
            if (component.UserInterface != null)
            {
                component.UserInterface.OnReceiveMessage += component.OnUiReceiveMessage;
            }

            if (component._entMan.TryGetComponent(component.Owner, out ApcPowerReceiverComponent? receiver))
            {
                component.TrySetVisualState(receiver.Powered ? VendingMachineVisualState.Normal : VendingMachineVisualState.Off);
            }

            InitializeFromPrototype(component);
        }
        private void HandleActivate(EntityUid uid, CustomVendingMachineComponent component, ActivateInWorldEvent args)
        {
            if (!component.Powered ||
                !EntityManager.TryGetComponent(args.User, out ActorComponent? actor))
            {
                return;
            }

            var wires = component._entMan.GetComponent<WiresComponent>(component.Owner);
            if (wires.IsPanelOpen)
            {
                wires.OpenInterface(actor.PlayerSession);
            } else
            {
                component.UserInterface?.Toggle(actor.PlayerSession);
            }
        }
        private void OnPowerChanged(EntityUid uid, CustomVendingMachineComponent component, PowerChangedEvent args)
        {
            var state = args.Powered ? VendingMachineVisualState.Normal : VendingMachineVisualState.Off;
            component.TrySetVisualState(state);
        }
        private void OnInteractUsing(EntityUid uid, CustomVendingMachineComponent component, InteractUsingEvent args)
        {
            if (component._storage == null || component.Whitelist == null) {
                return;
            }
            if (!component.IsAuthorized(args.User))
            {
                return;
            }
            if (!EntityManager.TryGetComponent(args.Used, out SharedItemComponent? itemComponent))
                return;

            if (!component.Whitelist.IsValid(args.Used))
            {
                return;
            }

            EntityManager.TryGetComponent(itemComponent.Owner, out MetaDataComponent? metaData);

            EntityUid ent = args.Used; //Get the entity of the ItemComponent.
            string id = ent.ToString();
            string name = metaData != null ? metaData.EntityName : ent.ToString();

            component._storage.Insert(ent);
            VendingMachineInventoryEntry newEntry = new VendingMachineInventoryEntry(ent.ToString(), name, ent, 1);
            component.Inventory.Add(newEntry);
            component.UserInterface?.SendMessage(new VendingMachineInventoryMessage(component.Inventory));
            SoundSystem.Play(Filter.Pvs(component.Owner), component.soundVend.GetSound(), component.Owner, AudioParams.Default.WithVolume(-2f));
            return;
        }

        public void InitializeFromPrototype(CustomVendingMachineComponent component)
        {
            if (string.IsNullOrEmpty(component._packPrototypeId)) { return; }
            if (!component._prototypeManager.TryIndex(component._packPrototypeId, out VendingMachineInventoryPrototype? packPrototype))
            {
                return;
            }

            component._entMan.GetComponent<MetaDataComponent>(component.Owner).EntityName = packPrototype.Name;
            component._animationDuration = TimeSpan.FromSeconds(packPrototype.AnimationDuration);
            component._spriteName = packPrototype.SpriteName;
            if (!string.IsNullOrEmpty(component._spriteName))
            {
                var spriteComponent = component._entMan.GetComponent<SpriteComponent>(component.Owner);
                const string vendingMachineRSIPath = "Structures/Machines/VendingMachines/{0}.rsi";
                spriteComponent.BaseRSIPath = string.Format(vendingMachineRSIPath, component._spriteName);
            }

            var inventory = new List<VendingMachineInventoryEntry>();
            foreach(var (id, amount) in packPrototype.StartingInventory)
            {
                if(!component._prototypeManager.TryIndex(id, out EntityPrototype? prototype))
                {
                    continue;
                }
                inventory.Add(new VendingMachineInventoryEntry(id, prototype.Name, null, amount));
            }
            component.Inventory = inventory;
        }

        public void Deny(CustomVendingMachineComponent component)
        {
            SoundSystem.Play(Filter.Pvs(component.Owner), component._soundDeny.GetSound(), component.Owner, AudioParams.Default.WithVolume(-2f));

            // Play the Deny animation
            TrySetVisualState(component, VendingMachineVisualState.Deny);
            //TODO: This duration should be a distinct value specific to the deny animation
            component.Owner.SpawnTimer(component._animationDuration, () =>
            {
                TrySetVisualState(component, VendingMachineVisualState.Normal);
            });
        }


        public bool IsAuthorized(CustomVendingMachineComponent component, EntityUid? sender)
        {
            if (component._entMan.TryGetComponent<AccessReaderComponent?>(component.Owner, out var accessReader))
            {
                var accessSystem = EntitySystem.Get<AccessReaderSystem>();
                if (sender == null || !accessSystem.IsAllowed(accessReader, sender.Value))
                {
                    component.Owner.PopupMessageEveryone(Loc.GetString("vending-machine-component-try-eject-access-denied"));
                    Deny(component);
                    return false;
                }
            }
            return true;
        }

        private void TryDispense(CustomVendingMachineComponent component, string id)
        {
            if (component._ejecting || component._broken)
            {
                return;
            }

            var entry = component.Inventory.Find(x => x.ID == id);
            if (entry == null)
            {
                component.Owner.PopupMessageEveryone(Loc.GetString("vending-machine-component-try-eject-invalid-item"));
                Deny(component);
                return;
            }

            if (entry.Amount <= 0)
            {
                component.Owner.PopupMessageEveryone(Loc.GetString("vending-machine-component-try-eject-out-of-stock"));
                Deny(component);
                return;
            }
            if (entry.ID != null) { // If this item is not a stored entity, eject as a new entity of type
                TryEjectVendorItem(component, entry);
                return;
            }
            return;
        }


        private void TryEjectVendorItem(CustomVendingMachineComponent component, VendingMachineInventoryEntry entry)
        {
            component._ejecting = true;
            entry.Amount--;
            component.UserInterface?.SendMessage(new VendingMachineInventoryMessage(component.Inventory));
            TrySetVisualState(component, VendingMachineVisualState.Eject);

            component.Owner.SpawnTimer(component._animationDuration, () =>
            {
                component._ejecting = false;
                TrySetVisualState(component, VendingMachineVisualState.Normal);
                component._entMan.SpawnEntity(entry.ID, component._entMan.GetComponent<TransformComponent>(component.Owner).Coordinates);
            });

            SoundSystem.Play(Filter.Pvs(component.Owner), component.soundVend.GetSound(), component.Owner, AudioParams.Default.WithVolume(-2f));
        }
        private void AuthorizedVend(CustomVendingMachineComponent component, string id, EntityUid? sender)
        {
            if (IsAuthorized(component, sender))
            {
                TryDispense(component, id);
            }
            return;
        }
        private void TrySetVisualState(CustomVendingMachineComponent component, VendingMachineVisualState state)
        {
            var finalState = state;
            if (component._broken)
            {
                finalState = VendingMachineVisualState.Broken;
            }
            else if (component._ejecting)
            {
                finalState = VendingMachineVisualState.Eject;
            }
            else if (!component.Powered)
            {
                finalState = VendingMachineVisualState.Off;
            }

            if (component._entMan.TryGetComponent(component.Owner, out AppearanceComponent? appearance))
            {
                appearance.SetData(VendingMachineVisuals.VisualState, finalState);
            }
        }



        // private void TryEjectStorageItem(VendingMachineInventoryEntry entry)
        // {
        //     if (entry.EntityID == null || _storage == null)
        //     {
        //         return;
        //     }
        //     var entity = entry.EntityID.Value;
        //     if (_entMan.EntityExists(entity))
        //     {
        //         _ejecting = true;
        //         Inventory.Remove(entry); // remove entry completely
        //         UserInterface?.SendMessage(new VendingMachineInventoryMessage(Inventory));
        //         TrySetVisualState(VendingMachineVisualState.Eject);
        //         Owner.SpawnTimer(_animationDuration, () =>
        //         {
        //             _ejecting = false;
        //             TrySetVisualState(VendingMachineVisualState.Normal);
        //             _storage.Remove(entity);
        //         });
        //         SoundSystem.Play(Filter.Pvs(Owner), soundVend.GetSound(), Owner, AudioParams.Default.WithVolume(-2f));
        //     }
        // }
    }
}
