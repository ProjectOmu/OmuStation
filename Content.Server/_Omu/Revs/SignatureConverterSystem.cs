using Content.Goobstation.Common.Paper;
using Content.Server.Revolutionary.Components;
using Content.Shared._Omu.Revs;
using Content.Server.Popups;

namespace Content.Server._Omu.Revs;

public sealed class SignatureConverterSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SignatureConverterComponent, BeingSignedAttemptEvent>(OnSign);
    }

    public void OnSign(Entity<SignatureConverterComponent> ent, ref BeingSignedAttemptEvent args)
    {
        if (HasComp<CommandStaffComponent>(args.Signer))
        {
            _popup.PopupEntity(Loc.GetString("fail-signature-command"), args.Signer, args.Signer);
            return;
        }

        EnsureComp<MoraleComponent>(args.Signer);
        var ev = new MoraleChangedArgs();
        ev.Amount = ent.Comp.Amount;
        ev.User = args.Signer;
        ev.Forced = true;
        RaiseLocalEvent(args.Signer, ev);
    }
}
