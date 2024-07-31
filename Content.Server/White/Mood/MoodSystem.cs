﻿using Content.Server.Chat.Managers;
using Content.Shared.Alert;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.White.Mood;
using Content.Shared.White.Overlays;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.White.Mood;

public sealed class MoodSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly SharedJetpackSystem _jetpack = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MoodComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MoodComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MoodComponent, MoodEffectEvent>(OnMoodEffect);
        SubscribeLocalEvent<MoodComponent, DamageChangedEvent>(OnDamageChange);
        SubscribeLocalEvent<MoodComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<MoodComponent, MoodRemoveEffectEvent>(OnRemoveEffect);
    }

    private void OnRemoveEffect(EntityUid uid, MoodComponent component, MoodRemoveEffectEvent args)
    {
        if (component.UncategorisedEffects.TryGetValue(args.EffectId, out _))
            RemoveTimedOutEffect(uid, args.EffectId);
        else
            foreach (var (category, id) in component.CategorisedEffects)
                if (id == args.EffectId)
                {
                    RemoveTimedOutEffect(uid, args.EffectId, category);
                    return;
                }
    }

    private void OnRefreshMoveSpeed(EntityUid uid, MoodComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.CurrentMoodThreshold is > MoodThreshold.VeryBad and < MoodThreshold.VeryGood or MoodThreshold.Dead
            || _jetpack.IsUserFlying(uid))
            return;

        var modifier = GetMovementThreshold(component.CurrentMoodThreshold) switch
        {
            -1 => component.SlowdownSpeedModifier,
            1 => component.IncreaseSpeedModifier,
            _ => 1
        };

        args.ModifySpeed(modifier, modifier);
    }

    private void OnMoodEffect(EntityUid uid, MoodComponent component, MoodEffectEvent args)
    {
        if (!_prototypeManager.TryIndex<MoodEffectPrototype>(args.EffectId, out var prototype))
            return;

        ApplyEffect(uid, component, prototype);
    }

    private void ApplyEffect(EntityUid uid, MoodComponent component, MoodEffectPrototype prototype)
    {
        var amount = component.CurrentMoodLevel;

        if (!component.MoodChangeValues.TryGetValue(prototype.MoodChange, out var value)
            || prototype.Category == null)
            return;

        //Apply categorised effect
        if (component.CategorisedEffects.TryGetValue(prototype.Category, out var oldPrototypeId)
            && _prototypeManager.TryIndex<MoodEffectPrototype>(oldPrototypeId, out var oldPrototype)
            && prototype.ID != oldPrototype.ID
            && component.MoodChangeValues.TryGetValue(oldPrototype.MoodChange, out var oldValue))
        {
            amount += (oldPrototype.PositiveEffect ? -oldValue : oldValue) + (prototype.PositiveEffect ? value : -value);
            component.CategorisedEffects[prototype.Category] = prototype.ID;
        }
        else
        {
            component.CategorisedEffects.Add(prototype.Category, prototype.ID);
            amount += prototype.PositiveEffect ? value : -value;
        }

        if (prototype.Timeout != 0)
            Timer.Spawn(TimeSpan.FromMinutes(prototype.Timeout), () => RemoveTimedOutEffect(uid, prototype.ID, prototype.Category));
        //Apply uncategorised effect
        else
        {
            if (component.UncategorisedEffects.TryGetValue(prototype.ID, out _))
                return;

            var effectValue = prototype.PositiveEffect ? value : -value;

            component.UncategorisedEffects.Add(prototype.ID, effectValue);
            amount += effectValue;

            if (prototype.Timeout != 0)
                Timer.Spawn(TimeSpan.FromMinutes(prototype.Timeout), () => RemoveTimedOutEffect(uid, prototype.ID));
        }

        SetMood(uid, amount, component);
    }

    private void RemoveTimedOutEffect(EntityUid uid, string prototypeId, string? category = null)
    {
        if (!TryComp<MoodComponent>(uid, out var comp))
            return;

        var amount = comp.CurrentMoodLevel;

        if (category == null)
        {
            if (!comp.UncategorisedEffects.TryGetValue(prototypeId, out var value))
                return;

            amount += -value;
            comp.UncategorisedEffects.Remove(prototypeId);
        }
        else
        {
            if (!comp.CategorisedEffects.TryGetValue(category, out var currentProtoId)
                || currentProtoId != prototypeId
                || !_prototypeManager.TryIndex<MoodEffectPrototype>(currentProtoId, out var currentProto)
                || !comp.MoodChangeValues.TryGetValue(currentProto.MoodChange, out var value))
                return;

            amount += currentProto.PositiveEffect ? -value : value;
            comp.CategorisedEffects.Remove(category);
        }

        SetMood(uid, amount, comp);
    }

    private void OnMobStateChanged(EntityUid uid, MoodComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && args.OldMobState != MobState.Dead)
            SetMood(uid, component.MoodThresholds[MoodThreshold.Dead], component, true);

        else if (args.OldMobState == MobState.Dead && args.NewMobState != MobState.Dead)
            ReapplyAllEffects(uid, component);
    }

    private void ReapplyAllEffects(EntityUid uid, MoodComponent component)
    {
        var amount = component.MoodThresholds[MoodThreshold.Neutral];

        foreach (var (_, protoId) in component.CategorisedEffects)
        {
            if (!_prototypeManager.TryIndex<MoodEffectPrototype>(protoId, out var prototype)
                || !component.MoodChangeValues.TryGetValue(prototype.MoodChange, out var value))
                return;

            amount += prototype.PositiveEffect ? value : -value;
        }

        foreach (var (_, value) in component.UncategorisedEffects)
            amount += value;

        SetMood(uid, amount, component, refresh: true);
    }

    private void OnInit(EntityUid uid, MoodComponent component, ComponentInit args)
    {
        if (TryComp<MobThresholdsComponent>(uid, out var mobThresholdsComponent)
            && _mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var critThreshold, mobThresholdsComponent))
            component.CritThresholdBeforeModify = critThreshold.Value;

        var amount = component.MoodThresholds[MoodThreshold.Neutral];
        SetMood(uid, amount, component, refresh: true);
    }

    public void SetMood(EntityUid uid, float amount, MoodComponent? component = null, bool force = false, bool refresh = false)
    {
        if (!Resolve(uid, ref component)
            || component.CurrentMoodThreshold == MoodThreshold.Dead && !refresh)
            return;

        if (!force)
        {
            component.CurrentMoodLevel = Math.Clamp(amount,
                component.MoodThresholds[MoodThreshold.Dead] + 0.1f,
                component.MoodThresholds[MoodThreshold.VeryVeryGood]);
        }
        else
            component.CurrentMoodLevel = amount;

        UpdateCurrentThreshold(uid, component);
    }

    private void UpdateCurrentThreshold(EntityUid uid, MoodComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var calculatedThreshold = GetMoodThreshold(component);
        if (calculatedThreshold == component.CurrentMoodThreshold)
            return;

        component.CurrentMoodThreshold = calculatedThreshold;

        DoMoodThresholdsEffects(uid, component);
    }

    private void DoMoodThresholdsEffects(EntityUid uid, MoodComponent? component = null, bool force = false)
    {
        if (!Resolve(uid, ref component)
            || component.CurrentMoodThreshold == component.LastThreshold && !force)
            return;

        var modifier = GetMovementThreshold(component.CurrentMoodThreshold);

        // Modify mob stats
        if (modifier != GetMovementThreshold(component.LastThreshold))
        {
            _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
            SetCritThreshold(uid, component, modifier);
            RefreshShaders(uid, modifier);
        }

        // Modify interface
        if (component.MoodThresholdsAlerts.TryGetValue(component.CurrentMoodThreshold, out var alertId))
            _alerts.ShowAlert(uid, alertId);
        else
            _alerts.ClearAlertCategory(uid, AlertCategory.Mood);

        component.LastThreshold = component.CurrentMoodThreshold;
    }

    private void RefreshShaders(EntityUid uid, int modifier)
    {
        if (modifier == -1)
            EnsureComp<SaturationScaleComponent>(uid);
        else
            RemComp<SaturationScaleComponent>(uid);
    }

    private void SetCritThreshold(EntityUid uid, MoodComponent component, int modifier)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var mobThresholds)
            || !_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var key))
            return;

        var newKey = modifier switch
        {
            1 => FixedPoint2.New(key.Value.Float() * component.IncreaseCritThreshold),
            -1 => FixedPoint2.New(key.Value.Float() * component.DecreaseCritThreshold),
            _ => component.CritThresholdBeforeModify
        };

        component.CritThresholdBeforeModify = key.Value;
        _mobThreshold.SetMobStateThreshold(uid, newKey, MobState.Critical, mobThresholds);
    }

    private MoodThreshold GetMoodThreshold(MoodComponent component, float? moodLevel = null)
    {
        moodLevel ??= component.CurrentMoodLevel;
        var result = MoodThreshold.Dead;
        var value = component.MoodThresholds[MoodThreshold.VeryVeryGood];

        foreach (var threshold in component.MoodThresholds)
            if (threshold.Value <= value && threshold.Value >= moodLevel)
            {
                result = threshold.Key;
                value = threshold.Value;
            }

        return result;
    }

    private int GetMovementThreshold(MoodThreshold threshold)
    {
        return threshold switch
        {
            >= MoodThreshold.VeryGood => 1,
            <= MoodThreshold.VeryBad => -1,
            _ => 0
        };
    }

    #region HealthStatusCheck

    private void OnDamageChange(EntityUid uid, MoodComponent component, DamageChangedEvent args)
    {
        var damage = args.Damageable.TotalDamage.Float();
        var protoId = "HealthNoDamage";
        var value = component.HealthMoodEffectsThresholds["HealthNoDamage"];

        foreach (var threshold in component.HealthMoodEffectsThresholds)
            if (threshold.Value <= damage && threshold.Value >= value)
            {
                protoId = threshold.Key;
                value = threshold.Value;
            }

        var ev = new MoodEffectEvent(protoId);
        RaiseLocalEvent(uid, ev);
    }

    #endregion
}

[UsedImplicitly]
[DataDefinition]
public sealed partial class ShowMoodEffects : IAlertClick
{
    public void AlertClicked(EntityUid uid)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        var chatManager = IoCManager.Resolve<IChatManager>();

        if (!entityManager.TryGetComponent<MoodComponent>(uid, out var comp)
            || comp.CurrentMoodThreshold == MoodThreshold.Dead
            || !entityManager.TryGetComponent<MindComponent>(uid, out var mindComp)
            || mindComp.Session == null)
            return;

        var msgStart = Loc.GetString("mood-show-effects-start");
        chatManager.ChatMessageToOne(ChatChannel.Emotes, msgStart, msgStart, EntityUid.Invalid, false,
            mindComp.Session.Channel);

        foreach (var (_, protoId) in comp.CategorisedEffects)
        {
            if (!prototypeManager.TryIndex<MoodEffectPrototype>(protoId, out var proto)
                || proto.Hidden)
                continue;

            SendDescToChat(proto, mindComp);
        }

        foreach (var (protoId, _) in comp.UncategorisedEffects)
        {
            if (!prototypeManager.TryIndex<MoodEffectPrototype>(protoId, out var proto)
                || proto.Hidden)
                continue;

            SendDescToChat(proto, mindComp);
        }
    }

    private void SendDescToChat(MoodEffectPrototype proto, MindComponent comp)
    {
        if (comp.Session == null)
            return;

        var chatManager = IoCManager.Resolve<IChatManager>();

        var color = proto.PositiveEffect ? "#008000" : "#BA0000";
        var msg = $"[font size=10][color={color}]{proto.Description}[/color][/font]";

        chatManager.ChatMessageToOne(ChatChannel.Emotes, msg, msg, EntityUid.Invalid, false,
            comp.Session.Channel);
    }
}
