// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Shared.Billiards;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, fieldDeltas: true)]
[AutoGenerateComponentPause]
public sealed partial class BilliardsTableComponent : Component
{
    [DataField, AutoNetworkedField]
    public BilliardsMode Mode = BilliardsMode.Unselected;

    [DataField, AutoNetworkedField]
    public List<BilliardsBall> Balls = [];

    [ViewVariables, AutoNetworkedField]
    public List<BilliardsPlayer> Players = [];

    [DataField, AutoNetworkedField]
    public int Turn;

    [DataField, AutoNetworkedField]
    public bool BallInHand;

    [DataField, AutoNetworkedField]
    public int SolidsPlayer = -1;

    [DataField, AutoNetworkedField]
    public bool Finished;

    [DataField, AutoNetworkedField]
    public int Winner = -1;

    [DataField, AutoNetworkedField]
    public EntityUid? Aimer;

    [DataField, AutoNetworkedField]
    public Vector2 AimDirection = Vector2.UnitX;

    [DataField, AutoNetworkedField]
    public float AimPower;

    [DataField, AutoNetworkedField]
    public int Shots;

    [DataField, AutoNetworkedField]
    public Vector2 ShotVelocity;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan? ShotStart;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan? ShotEnd;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan LastActivity;

    [DataField]
    public TimeSpan IdleReset = TimeSpan.FromMinutes(2);

    [DataField]
    public bool AwaitingBreak;

    [DataField]
    public int KillerLives = 3;

    [DataField]
    public float AbsentRange = 4f;

    [DataField]
    public SoundSpecifier StrikeSound = new SoundCollectionSpecifier("Dice");

    [DataField]
    public SoundSpecifier ContactSound = new SoundCollectionSpecifier("Dice");
}
