using Content.Goobstation.Common.Traits.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.InteractionVerbs.Events;
using Content.Shared.Popups;

namespace Content.Goobstation.Shared.Traits.Systems;

public sealed partial class SocialAnxietySystem
{
    private void OnHug(EntityUid uid, SocialAnxietyComponent component, ref InteractionSuccessEvent args)
    {
        _standingSystem.Down(uid);
        _stunSystem.TryUpdateStunDuration(uid, TimeSpan.FromSeconds(component.DownedTime));
        var mobName = Identity.Name(uid, EntityManager);
        if(_net.IsServer)
            _popupSystem.PopupEntity(Loc.GetString("social-anxiety-hugged", ("user", mobName)), uid, PopupType.MediumCaution);
    }

    private void OnVerbHug(InteractionVerbDoAfterEvent args)
    {
        // we HAVE to subscribe to every InteractionVerbDoAfterEvent that fires.
        // because I don't think absolute EE supercode allows us to do it normally.
        if (!TryComp<SocialAnxietyComponent>(args.Target, out var component))
            return;
        if (!args.Target.HasValue)
            return;

        foreach (var prototype in PrototypeHug)
        {
            if (args.VerbPrototype != prototype)
                continue;
            var uid = args.Target.Value;
            _standingSystem.Down(uid);
            _stunSystem.TryUpdateStunDuration(uid, TimeSpan.FromSeconds(component.DownedTime));
            var mobName = Identity.Name(uid, EntityManager);
            if(_net.IsServer)
            {
                _popupSystem.PopupEntity(Loc.GetString("social-anxiety-hugged", ("user", mobName)),
                    uid,
                    PopupType.MediumCaution);
            }

            break;
        }

    }
}
