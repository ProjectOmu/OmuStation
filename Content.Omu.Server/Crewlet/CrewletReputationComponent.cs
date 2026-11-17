// SPDX-License-Identifier: MIT

using Robust.Shared.Timing;

namespace Content.Omu.Server.Crewlet;

[RegisterComponent]
public sealed partial class CrewletReputationComponent : Component
{
    [DataField]
    public Dictionary<EntityUid, CrewletOpinion> Opinions = new();

    [DataField]
    public Dictionary<EntityUid, TimeSpan> NextHug = new();

    [DataField]
    public Dictionary<EntityUid, TimeSpan> NextFed = new();

    [DataField]
    public Dictionary<EntityUid, TimeSpan> NextMedicine = new();

    [DataField]
    public float MaxOpinion = 100f;

    [DataField]
    public float DecayPerMinute = 2f;

    [DataField]
    public float HugOpinion = 4f;

    [DataField]
    public TimeSpan HugCooldown = TimeSpan.FromSeconds(30);

    [DataField]
    public float FedOpinion = 8f;

    [DataField]
    public TimeSpan FedCooldown = TimeSpan.FromSeconds(30);

    [DataField]
    public float PoisonOpinion = -12f;

    [DataField]
    public float MedicineOpinion = 6f;

    [DataField]
    public TimeSpan MedicineCooldown = TimeSpan.FromSeconds(30);

    [DataField]
    public float HurtPerDamage = 1.5f;

    [DataField]
    public float MinHurtPenalty = 5f;

    [DataField]
    public float HealedPerDamage = 0.5f;

    [DataField]
    public float MaxHealedBonus = 10f;

    [DataField]
    public float ShovedOpinion = -8f;

    [DataField]
    public float AbandonThreshold = -40f;

    [DataField]
    public float AdoptThreshold = 15f;

    [DataField]
    public float AdoptRange = 10f;

    [DataField]
    public float FriendlyThreshold = 15f;

    [DataField]
    public float WaryThreshold = -15f;

    [DataField]
    public float StrongFeeling = 0.4f;

    [DataField]
    public Color LikeColor = Color.FromHex("#5fd35f");

    [DataField]
    public Color DislikeColor = Color.FromHex("#e05050");

    public CrewletTally Feeding;

    public CrewletTally Injection;

    public CrewletTally Hurt;

    public CrewletTally Healed;
}

[DataRecord]
public partial record struct CrewletOpinion(float Value, TimeSpan Updated);

public struct CrewletTally
{
    public EntityUid Person;
    public GameTick Tick;
    public float Total;
    public float Applied;
}
