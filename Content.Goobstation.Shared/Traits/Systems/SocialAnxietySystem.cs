using Content.Goobstation.Common.Traits.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Standing;
using Content.Shared.Popups;
using Content.Shared.Stunnable;

// Omu Station
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Content.Shared.InteractionVerbs;
using Content.Shared.InteractionVerbs.Events;

namespace Content.Goobstation.Shared.Traits.Systems;

public sealed partial class SocialAnxietySystem : EntitySystem
{
    [Dependency] private readonly StandingStateSystem _standingSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedStunSystem _stunSystem = default!;

    // Omu Station
    [Dependency] private readonly INetManager _net = default!;
    public ProtoId<InteractionVerbPrototype>[] PrototypeHug { get; } =
    [
        "Hug",
        "Pet",
    ];
    // END

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SocialAnxietyComponent, InteractionSuccessEvent>(OnHug);

        // Omu
        SubscribeLocalEvent<InteractionVerbDoAfterEvent>(OnVerbHug);
    }

    /*
     // Omu Station - Replaced by SocialAnxietySystem.Omu.cs
    private void OnHug(EntityUid uid, SocialAnxietyComponent component, ref InteractionSuccessEvent args)
    {
        _standingSystem.Down(uid);
        _stunSystem.TryUpdateStunDuration(uid, TimeSpan.FromSeconds(component.DownedTime));
        var mobName = Identity.Name(uid, EntityManager);
        _popupSystem.PopupEntity(Loc.GetString("social-anxiety-hugged", ("user", mobName)), uid, PopupType.MediumCaution);
    }
    */
}
