// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Audio;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared.Rotation;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Omu.Client.MartialArts;

public sealed class WrestlingAnimationSystem : EntitySystem
{
    private const string FatigueAnimationKey = "stamina";
    private const float BeltCheerFadeTime = 1.5f;
    private const float DdtLockTime = 0.15f;
    private const float DdtSpikeTime = 0.12f;
    private const float DropkickLeapTime = 0.12f;
    private const float DropkickFallTime = 0.2f;
    private const float ElbowDropRiseTime = 0.22f;
    private const float ElbowDropFallTime = 0.12f;
    private const float RkoLeapTime = 0.25f;
    private const float RkoHangTime = 0.15f;
    private const float RkoDropTime = 0.1f;
    private const float SlamCritChantDelay = 0.88f;
    private const float SuplexFlightTime = 0.45f;
    private const float MaxFlipStep = 80f;

    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ContentAudioSystem _contentAudio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<WrestlingSuplexAnimationEvent>(OnSuplex);
        SubscribeNetworkEvent<WrestlingRkoAnimationEvent>(OnRko);
        SubscribeNetworkEvent<WrestlingDdtAnimationEvent>(OnDdt);
        SubscribeNetworkEvent<WrestlingElbowDropAnimationEvent>(OnElbowDrop);
        SubscribeNetworkEvent<WrestlingDropkickAnimationEvent>(OnDropkick);
        SubscribeNetworkEvent<WrestlingSlamAnimationEvent>(OnSlam);
        SubscribeNetworkEvent<WrestlingBeltCheerEvent>(OnBeltCheer);
    }

    private void OnBeltCheer(WrestlingBeltCheerEvent args)
    {
        if (_audio.PlayGlobal(args.Sound, Filter.Local(), false) is { } cheer)
            _contentAudio.FadeIn(cheer.Entity, cheer.Component, BeltCheerFadeTime);
    }

    private void OnDdt(WrestlingDdtAnimationEvent args)
    {
        var target = GetEntity(args.Target);
        if (!TryComp<SpriteComponent>(target, out var sprite))
            return;

        var impact = new AnimationTrackPlaySound();
        var tracks = new List<AnimationTrack> { impact };

        if (!args.HeadFirst)
        {
            impact.KeyFrames.Add(new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(args.ImpactSound),
                0f,
                () => args.ImpactSound.Params));
            tracks.Add(Bounce(0f));
            Play(target, tracks, 0.2f);
            return;
        }

        var upright = sprite.Rotation;
        var headDown = upright + Angle.FromDegrees(150);
        var floored = args.LandedDown && TryComp<RotationVisualsComponent>(target, out var rotation)
            ? Toward(headDown, rotation.HorizontalRotation)
            : Toward(headDown, upright);

        tracks.Add(new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Offset),
            InterpolationMode = AnimationInterpolationMode.Cubic,
            KeyFrames =
            {
                new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                new AnimationTrackProperty.KeyFrame(new Vector2(0f, 0.3f), DdtLockTime),
                new AnimationTrackProperty.KeyFrame(Vector2.Zero, DdtSpikeTime),
            },
        });
        tracks.Add(new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Rotation),
            InterpolationMode = AnimationInterpolationMode.Linear,
            KeyFrames =
            {
                new AnimationTrackProperty.KeyFrame(upright, 0f),
                new AnimationTrackProperty.KeyFrame(upright + Angle.FromDegrees(75), DdtLockTime / 2),
                new AnimationTrackProperty.KeyFrame(headDown, DdtLockTime / 2),
                new AnimationTrackProperty.KeyFrame(floored, DdtSpikeTime),
            },
        });
        impact.KeyFrames.Add(new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(args.ImpactSound),
            DdtLockTime + DdtSpikeTime,
            () => args.ImpactSound.Params));

        Play(target, tracks, DdtLockTime + DdtSpikeTime + 0.05f);
    }

    private void OnElbowDrop(WrestlingElbowDropAnimationEvent args)
    {
        var performer = GetEntity(args.Performer);
        var target = GetEntity(args.Target);
        var impactTime = ElbowDropRiseTime + ElbowDropFallTime;

        if (Exists(performer))
        {
            var leap = new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Offset),
                InterpolationMode = AnimationInterpolationMode.Cubic,
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                    new AnimationTrackProperty.KeyFrame(new Vector2(0f, 0.8f), ElbowDropRiseTime),
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, ElbowDropFallTime),
                },
            };
            Play(performer, new List<AnimationTrack> { leap }, impactTime + 0.05f);
        }

        if (!Exists(target))
            return;

        var impact = new AnimationTrackPlaySound
        {
            KeyFrames =
            {
                new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(args.ImpactSound),
                    impactTime,
                    () => args.ImpactSound.Params),
            },
        };
        Play(target, new List<AnimationTrack> { impact, Bounce(impactTime) }, impactTime + 0.2f);
    }

    private void OnDropkick(WrestlingDropkickAnimationEvent args)
    {
        var performer = GetEntity(args.Performer);
        var target = GetEntity(args.Target);

        if (Exists(performer))
        {
            var leap = new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Offset),
                InterpolationMode = AnimationInterpolationMode.Cubic,
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                    new AnimationTrackProperty.KeyFrame(new Vector2(0f, 0.45f), DropkickLeapTime),
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, DropkickFallTime),
                },
            };
            Play(performer, new List<AnimationTrack> { leap }, DropkickLeapTime + DropkickFallTime + 0.05f);
        }

        if (!Exists(target))
            return;

        var impact = new AnimationTrackPlaySound
        {
            KeyFrames =
            {
                new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(args.ImpactSound),
                    DropkickLeapTime,
                    () => args.ImpactSound.Params),
            },
        };
        Play(target, new List<AnimationTrack> { impact, Bounce(DropkickLeapTime) }, DropkickLeapTime + 0.2f);
    }

    private void OnRko(WrestlingRkoAnimationEvent args)
    {
        var performer = GetEntity(args.Performer);
        var target = GetEntity(args.Target);
        var impactTime = RkoLeapTime + RkoHangTime + RkoDropTime;

        if (Exists(performer))
        {
            var leap = new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Offset),
                InterpolationMode = AnimationInterpolationMode.Cubic,
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                    new AnimationTrackProperty.KeyFrame(new Vector2(0f, 0.55f), RkoLeapTime),
                    new AnimationTrackProperty.KeyFrame(new Vector2(0f, 0.6f), RkoHangTime),
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, RkoDropTime),
                },
            };
            Play(performer, new List<AnimationTrack> { leap }, impactTime + 0.05f);
        }

        if (!TryComp<SpriteComponent>(target, out var targetSprite))
            return;

        var upright = targetSprite.Rotation;
        var floored = args.LandedDown && TryComp<RotationVisualsComponent>(target, out var targetRotation)
            ? targetRotation.HorizontalRotation
            : upright;

        var tracks = new List<AnimationTrack>
        {
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Offset),
                InterpolationMode = AnimationInterpolationMode.Cubic,
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, RkoLeapTime),
                    new AnimationTrackProperty.KeyFrame(new Vector2(0f, 0.25f), RkoHangTime),
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, RkoDropTime),
                },
            },
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Rotation),
                InterpolationMode = AnimationInterpolationMode.Linear,
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(upright, 0f),
                    new AnimationTrackProperty.KeyFrame(upright, RkoLeapTime + RkoHangTime),
                    new AnimationTrackProperty.KeyFrame(floored, RkoDropTime),
                },
            },
            new AnimationTrackPlaySound
            {
                KeyFrames =
                {
                    new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(args.ImpactSound),
                        impactTime,
                        () => args.ImpactSound.Params),
                },
            },
        };

        if (CrowdCheer(args.CrowdSound) is { } cheer)
            tracks.Add(cheer);

        Play(target, tracks, impactTime + 0.05f);
    }

    private void OnSuplex(WrestlingSuplexAnimationEvent args)
    {
        var target = GetEntity(args.Target);
        if (!TryComp<SpriteComponent>(target, out var sprite))
            return;

        var start = sprite.Rotation.Degrees;
        var landing = args.LandedDown && TryComp<RotationVisualsComponent>(target, out var rotation)
            ? rotation.HorizontalRotation.Degrees
            : 0;
        var turn = 360 + ((landing - start) % 360 + 360) % 360;
        var steps = (int) Math.Ceiling(turn / MaxFlipStep);

        var flip = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Rotation),
            InterpolationMode = AnimationInterpolationMode.Linear,
        };
        flip.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(start), 0f));
        for (var i = 1; i <= steps; i++)
        {
            var angle = Angle.FromDegrees(start + turn * i / steps);
            flip.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(angle, SuplexFlightTime / steps));
        }

        var tracks = new List<AnimationTrack>
        {
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Offset),
                InterpolationMode = AnimationInterpolationMode.Cubic,
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                    new AnimationTrackProperty.KeyFrame(new Vector2(0f, 1.1f), SuplexFlightTime / 2),
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, SuplexFlightTime / 2),
                },
            },
            flip,
            new AnimationTrackPlaySound
            {
                KeyFrames =
                {
                    new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(args.LandSound),
                        SuplexFlightTime,
                        () => args.LandSound.Params),
                },
            },
        };

        Play(target, tracks, SuplexFlightTime + 0.05f);
    }

    private void OnSlam(WrestlingSlamAnimationEvent args)
    {
        var target = GetEntity(args.Target);
        if (!Exists(target))
            return;

        var tracks = new List<AnimationTrack>
        {
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Offset),
                InterpolationMode = AnimationInterpolationMode.Cubic,
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                    new AnimationTrackProperty.KeyFrame(new Vector2(0f, 0.9f), 0.35f),
                    new AnimationTrackProperty.KeyFrame(new Vector2(0f, 0.9f), 0.15f),
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0.08f),
                },
            },
            new AnimationTrackPlaySound
            {
                KeyFrames =
                {
                    new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(args.SlamSound),
                        0.58f,
                        () => args.SlamSound.Params),
                    new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(args.BellSound),
                        0.4f,
                        () => args.BellSound.Params),
                },
            },
        };

        if (CrowdCheer(args.CrowdSound) is { } cheer)
            tracks.Add(cheer);

        if (args.CritSound is { } chant)
        {
            tracks.Add(new AnimationTrackPlaySound
            {
                KeyFrames =
                {
                    new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(chant),
                        SlamCritChantDelay,
                        () => chant.Params),
                },
            });
        }

        Play(target, tracks, 1.7f);
    }

    private AnimationTrackPlaySound? CrowdCheer(SoundSpecifier? sound)
    {
        if (sound == null)
            return null;

        return new AnimationTrackPlaySound
        {
            KeyFrames = { new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(sound), 0f, () => sound.Params) },
        };
    }

    private static AnimationTrackComponentProperty Bounce(float delay)
    {
        return new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Offset),
            InterpolationMode = AnimationInterpolationMode.Linear,
            KeyFrames =
            {
                new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                new AnimationTrackProperty.KeyFrame(Vector2.Zero, delay),
                new AnimationTrackProperty.KeyFrame(new Vector2(0f, 0.12f), 0.05f),
                new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0.08f),
            },
        };
    }

    private static Angle Toward(Angle from, Angle to)
    {
        var turn = ((to.Degrees - from.Degrees) % 360 + 540) % 360 - 180;
        return from + Angle.FromDegrees(turn);
    }

    private void Play(EntityUid uid, List<AnimationTrack> tracks, float length)
    {
        var animation = new Animation { Length = TimeSpan.FromSeconds(length) };
        animation.AnimationTracks.AddRange(tracks);

        _animation.Stop(uid, FatigueAnimationKey);
        _animation.Play(uid, animation, FatigueAnimationKey);
    }
}
