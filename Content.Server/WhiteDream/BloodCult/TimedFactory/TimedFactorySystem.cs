﻿using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Shared.RadialSelector;
using Content.Shared.UserInterface;
using Content.Shared.WhiteDream.BloodCult;
using Robust.Server.GameObjects;

namespace Content.Server.WhiteDream.BloodCult.TimedFactory;

public sealed class TimedFactorySystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimedFactoryComponent, ActivatableUIOpenAttemptEvent>(OnTryOpenMenu);
        SubscribeLocalEvent<TimedFactoryComponent, RadialSelectorSelectedMessage>(OnPrototypeSelected);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var factoryQuery = EntityQueryEnumerator<TimedFactoryComponent>();
        while (factoryQuery.MoveNext(out var uid, out var factory))
        {
            if (factory.CooldownRemaining > 0)
                factory.CooldownRemaining -= frameTime;
            else
                _appearance.SetData(uid, TimedFactoryVisuals.Ready, true);
        }
    }

    private void OnTryOpenMenu(Entity<TimedFactoryComponent> factory, ref ActivatableUIOpenAttemptEvent args)
    {
        var cooldown = MathF.Ceiling(factory.Comp.CooldownRemaining);
        if (cooldown > 0)
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("timed-factory-cooldown", ("cooldown", cooldown)), factory, args.User);
        }

        if (!_ui.TryGetUi(factory, RadialSelectorUiKey.Key, out var bui) ||
            _ui.IsUiOpen(factory, RadialSelectorUiKey.Key))
        {
            return;
        }

        _ui.SetUiState(bui, new RadialSelectorState(factory.Comp.Prototypes));
    }

    private void OnPrototypeSelected(Entity<TimedFactoryComponent> factory, ref RadialSelectorSelectedMessage args)
    {
        if (args.Session.AttachedEntity is not { } user)
            return;

        var product = Spawn(args.SelectedItem, Transform(user).Coordinates);
        _hands.TryPickupAnyHand(user, product);
        factory.Comp.CooldownRemaining = factory.Comp.Cooldown;
        _appearance.SetData(factory, TimedFactoryVisuals.Ready, false);
    }
}
