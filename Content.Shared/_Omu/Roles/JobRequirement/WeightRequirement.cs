using System.Diagnostics.CodeAnalysis;
using System.Text;
using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Content.Shared.Roles;

namespace Content.Shared._Omu.Roles;

/// <summary>
/// Requires a character to have a certain fixture mass
/// </summary>
[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class WeightRequirement : JobRequirement
{
    [DataField(required: true)]
    public float MinimumWeight = 0;

    public override bool Check(
        IEntityManager entManager,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = new FormattedMessage();

        //the profile could be null if the player is a ghost. In this case we don't need to block the role selection for ghostrole
        if (profile is null)
            return true;

        // get the fixure component belonging to the player's species
        var species = protoManager.Index(profile.Species);
        protoManager.Index(species.Prototype).TryGetComponent<FixturesComponent>(out var fixture, entManager.ComponentFactory);

        if (fixture == null)
        {
            return false;
        }

        // "fix1" is used for all collisions except for getting set on fire iirc.
        var avg = (profile.Width + profile.Height) / 2;
        var radius = fixture.Fixtures["fix1"].Shape.Radius; 
        var density = fixture.Fixtures["fix1"].Density;
        var weight = MathF.Round(MathF.PI * MathF.Pow(radius * avg, 2) * density);

        if (!Inverted)
        {
            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString("role-timer-below-weight", ("weight", MinimumWeight)));
            return weight >= MinimumWeight;
        }
        else
        {
            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString("role-timer-above-weight", ("weight", MinimumWeight)));
            return weight <= MinimumWeight;
        }
    }

}
