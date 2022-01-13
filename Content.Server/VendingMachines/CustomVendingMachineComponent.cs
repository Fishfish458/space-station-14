using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using Robust.Shared.Containers;
using Content.Shared.Whitelist;
using static Content.Shared.Wires.SharedWiresComponent;

namespace Content.Server.VendingMachines
{
    [RegisterComponent]
    [ComponentReference(typeof(IActivate))]
    public class CustomVendingMachineComponent : VendingMachineComponent
    {
        [DataField("insertWhitelist")]
        public EntityWhitelist? Whitelist;
        public Container _storage = default!;

        // public bool Broken => _broken;

        private void TryEjectStorageItem(VendingMachineInventoryEntry entry)
        {
            if (entry.EntityID == null || _storage == null)
            {
                return;
            }
            var entity = entry.EntityID.Value;
            if (_entMan.EntityExists(entity))
            {
                _ejecting = true;
                Inventory.Remove(entry); // remove entry completely
                UserInterface?.SendMessage(new VendingMachineInventoryMessage(Inventory));
                TrySetVisualState(VendingMachineVisualState.Eject);
                Owner.SpawnTimer(_animationDuration, () =>
                {
                    _ejecting = false;
                    TrySetVisualState(VendingMachineVisualState.Normal);
                    _storage.Remove(entity);
                });
                SoundSystem.Play(Filter.Pvs(Owner), soundVend.GetSound(), Owner, AudioParams.Default.WithVolume(-2f));
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
            if (entry.EntityID != null) { // If this item is a stored item, use storage eject
                TryEjectStorageItem(entry);
                return;
            }
            return;
        }

        public void TrySetVisualState(VendingMachineVisualState state)
        {
            var finalState = state;
            if (_broken)
            {
                finalState = VendingMachineVisualState.Broken;
            }
            else if (_ejecting)
            {
                finalState = VendingMachineVisualState.Eject;
            }
            else if (!Powered)
            {
                finalState = VendingMachineVisualState.Off;
            }

            if (_entMan.TryGetComponent(Owner, out AppearanceComponent? appearance))
            {
                appearance.SetData(VendingMachineVisuals.VisualState, finalState);
            }
        }

        public void OnBreak(BreakageEventArgs eventArgs)
        {
            _broken = true;
            TrySetVisualState(VendingMachineVisualState.Broken);
        }

        // private void InsertItem(EntityUid item)
        // {
        //     _storage.Insert(item);
        //     VendingMachineInventoryEntry newEntry = new VendingMachineInventoryEntry(item.ToString(), metaData.EntityName, item, 1);
        //     Inventory.Add(newEntry);
        //     UserInterface?.SendMessage(new VendingMachineInventoryMessage(Inventory));
        //     SoundSystem.Play(Filter.Pvs(Owner), soundVend.GetSound(), Owner, AudioParams.Default.WithVolume(-2f));
        //     return;
        // }

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
            TryDispense(_random.Pick(availableItems).ID);
        }
    }
}

