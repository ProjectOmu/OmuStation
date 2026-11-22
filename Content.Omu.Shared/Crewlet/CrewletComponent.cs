// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._EinsteinEngines.Language;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Crewlet;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CrewletComponent : Component
{
    [DataField]
    public EntityUid? CrewletOwner;

    [DataField]
    public string TemplateName = string.Empty;

    [DataField]
    public string CallName = string.Empty;

    [DataField, AutoNetworkedField]
    public Vector2 Scale = new(0.55f, 0.55f);

    [DataField]
    public float FixtureScale = 0.85f;

    [DataField]
    public ProtoId<DamageModifierSetPrototype> DamageModifierSet = "CrewletProtection";

    [DataField]
    public List<ProtoId<TagPrototype>> Tags = new() { "DoorBumpOpener" };

    [DataField]
    public ProtoId<LanguagePrototype> Language = "TauCetiBasic";

    [DataField]
    public TimeSpan MeltDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public float JitterAmplitude = 80f;

    [DataField]
    public float JitterFrequency = 8f;

    [DataField]
    public EntProtoId ThroesEffect = "CrewletThroes";

    [DataField]
    public EntProtoId MeltEffect = "CrewletMelt";

    [DataField]
    public float WailPitch = 0.35f;

    [DataField]
    public float WailVolume = 5f;

    [DataField]
    public ProtoId<AudioPresetPrototype> WailReverb = "CrewletWail";
}
