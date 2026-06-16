using Content.Omu.Common.CCVar;
using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Audio.Components;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;


namespace Content.Goobstation.Shared.StationRadio.Systems;

public sealed class StationRadioReceiverSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly IConfigurationManager _cfgMan = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    
    // Omustation - volume controller for station radio
    private float _volume = 0f;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioLocalAudioChangeEvent>(OnAudioChanged);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaPlayedEvent>(OnMediaPlayed);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaStoppedEvent>(OnMediaStopped);
        SubscribeLocalEvent<StationRadioReceiverComponent, ActivateInWorldEvent>(OnRadioToggle);
        SubscribeLocalEvent<StationRadioReceiverComponent, PowerChangedEvent>(OnPowerChanged);
        // SubscribeLocalEvent<StationRadioReceiverComponent, ComponentInit>(OnSpawn);

        Subs.CVar(_cfgMan, OmuCVars.StationRadioVolume, StationRadioCVarChanged, true);

        _volume = _cfgMan.GetCVar(OmuCVars.StationRadioVolume);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();

        while (query.MoveNext(out var uid, out var mind)) {

            StationRadioReceiverComponent? localComp = null;
            if (!Resolve(uid, ref localComp))
                continue;
            if (localComp.NetSyncEnabled == true)
            {
                audioUpdate(localComp);
            } else {
                continue;
            }
            TransformComponent? xform = null;
            var sourcePos = _transform.GetMapCoordinates(uid, xform);

            var mindThings = new HashSet<Entity<MindComponent>>();
            if (xform != null)
                _entityLookup.GetEntitiesInRange(xform.Coordinates, 8f, mindThings);

                if (mindThings.Count > 0)
                {
                    localComp.NetSyncEnabled = true;
                }
        }
    }
    // private void OnSpawn(EntityUid uid, StationRadioReceiverComponent component, ComponentInit args)
    // {
    //     component.volume = _cfgMan.GetCVar(OmuCVars.StationRadioVolume);
    //     audioUpdate(component);
    // }
    private void StationRadioCVarChanged(float obj) 
    {
        _volume = obj * 100.0f;

        var query = AllEntityQuery<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var receiverComponent))
        {
            if (receiverComponent.SoundEntity != null && _power.IsPowered(receiver))
                {
                    receiverComponent.volume = _volume;
                    RaiseLocalEvent(receiver, new StationRadioLocalAudioChangeEvent());
                }
        }
    }

    private void audioUpdate(StationRadioReceiverComponent comp)
    {
        if(comp.SoundEntity != null) {
            AudioComponent? component = null;
            if (!Resolve(comp.SoundEntity.Value, ref component))
                return;
            if (component != null) 
                component.NetSyncEnabled = false;

            _audio.SetGain(comp.SoundEntity, comp.Active ? comp.volume : 0f);
        }
    }
    private void OnAudioChanged(EntityUid uid, StationRadioReceiverComponent comp, StationRadioLocalAudioChangeEvent args)
    {
        audioUpdate(comp);
    }

    private void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args)
    {
        if(comp.SoundEntity != null && args.Powered)
            audioUpdate(comp);
        else if(comp.SoundEntity != null)
            _audio.SetGain(comp.SoundEntity, 0);
    }

    private void OnRadioToggle(EntityUid uid, StationRadioReceiverComponent comp, ActivateInWorldEvent args)
    {
        comp.Active = !comp.Active;
        if (comp.SoundEntity != null && _power.IsPowered(uid))
            audioUpdate(comp);
    }

    private void OnMediaPlayed(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaPlayedEvent args)
    {
        AudioParams newAudioSet;
        // Update comp with local _volume
        if (comp.volume > 0) 
        {
            newAudioSet = new AudioParams().WithVolume(comp.volume);
        } else {
        
            newAudioSet = comp.DefaultParams;
        }
        

        var audio = _audio.PlayPredicted(args.MediaPlayed, uid, uid, newAudioSet);
        if (audio != null && _power.IsPowered(uid) && comp.Active) {
            comp.SoundEntity = audio.Value.Entity;
            audioUpdate(comp);
        } else if (audio != null && !_power.IsPowered(uid) || !comp.Active && audio != null)
        {
            comp.SoundEntity = audio.Value.Entity;
            _audio.SetGain(comp.SoundEntity, 0);
        }
        Dirty(uid, comp);
    }

    private void OnMediaStopped(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaStoppedEvent args)
    {
        if (comp.SoundEntity == null)
            return;

        comp.SoundEntity = _audio.Stop(comp.SoundEntity);
    }
}
