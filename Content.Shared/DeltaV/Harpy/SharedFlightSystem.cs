
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Robust.Shared.Serialization;
using Content.Shared.DeltaV.Harpy.Events;

namespace Content.Shared.DeltaV.Harpy
{
    public abstract class SharedFlightSystem : EntitySystem
    {
        [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
        [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
        [Dependency] private readonly StaminaSystem _staminaSystem = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<FlightComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<FlightComponent, ComponentShutdown>(OnShutdown);
            // Move out to client: SubscribeLocalEvent<FlightComponent, AnimationCompletedEvent>(OnAnimationCompleted);
        }

        #region Core Functions
        private void OnStartup(EntityUid uid, FlightComponent component, ComponentStartup args)
        {
            _actionsSystem.AddAction(uid, ref component.ToggleActionEntity, component.ToggleAction);
        }

        private void OnShutdown(EntityUid uid, FlightComponent component, ComponentShutdown args)
        {
            _actionsSystem.RemoveAction(uid, component.ToggleActionEntity);
        }

        public void ToggleActive(EntityUid uid, bool active, FlightComponent component)
        {
            component.On = active;
            component.TimeUntilFlap = 0f;
            _actionsSystem.SetToggled(component.ToggleActionEntity, component.On);
            // Triggers the flight animation
            RaiseNetworkEvent(new FlightEvent(GetNetEntity(uid), component.On, component.IsAnimated, component.IsLayerAnimated, component.Layer ?? string.Empty, component.AnimationKey));
            _staminaSystem.ToggleStaminaDrain(uid, component.StaminaDrainRate, active);
            UpdateHands(uid, active);
            Dirty(uid, component);
        }

        private void UpdateHands(EntityUid uid, bool flying)
        {
            if (!TryComp<HandsComponent>(uid, out var handsComponent))
                return;

            if (flying)
                BlockHands(uid, handsComponent);
            else
                FreeHands(uid);
        }

        private void BlockHands(EntityUid uid, HandsComponent handsComponent)
        {
            var freeHands = 0;
            foreach (var hand in _hands.EnumerateHands(uid, handsComponent))
            {
                if (hand.HeldEntity == null)
                {
                    freeHands++;
                    continue;
                }

                // Is this entity removable? (they might have handcuffs on)
                if (HasComp<UnremoveableComponent>(hand.HeldEntity) && hand.HeldEntity != uid)
                    continue;

                _hands.DoDrop(uid, hand, true, handsComponent);
                freeHands++;
                if (freeHands == 2)
                    break;
            }
            if (_virtualItem.TrySpawnVirtualItemInHand(uid, uid, out var virtItem1))
                EnsureComp<UnremoveableComponent>(virtItem1.Value);

            if (_virtualItem.TrySpawnVirtualItemInHand(uid, uid, out var virtItem2))
                EnsureComp<UnremoveableComponent>(virtItem2.Value);
        }

        private void FreeHands(EntityUid uid)
        {
            _virtualItem.DeleteInHandsMatching(uid, uid);
        }
        #endregion
    }
    public sealed partial class ToggleFlightEvent : InstantActionEvent
    {
    }
}

