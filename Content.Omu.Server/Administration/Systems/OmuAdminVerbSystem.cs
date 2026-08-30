using System.Diagnostics.CodeAnalysis;
using Content.Server._Omu.Chimera.GameTicking.Rules;
using Content.Server.Antag;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Shared.Verbs;

namespace Content.Omu.Server.Administration.Systems;

public sealed partial class OmuAdminVerbSystem : EntitySystem
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(GetVerbs);
    }

    private void GetVerbs(GetVerbsEvent<Verb> args)
    {
        AddSmiteVerbs(args);
        AddAntagVerbs(args);
    }

    private void AddAntagVerbs(GetVerbsEvent<Verb> args)
    {
        if (!AntagVerbAllowed(args, out var targetPlayer))
            return;

        // Chimera Agent
        Verb initialChimera = new()
        {
            Text = Loc.GetString("admin-verb-text-make-chimera"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Goobstation/Changeling/changeling_abilities.rsi"), "transform"),
            Act = () =>
            {
                if (!HasComp<SiliconComponent>(args.Target))
                    _antag.ForceMakeAntag<ChimeraRuleComponent>(targetPlayer, "Letoferol");
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-chimera"),
        };
        if (!HasComp<SiliconComponent>(args.Target))
            args.Verbs.Add(initialChimera);
    }

    public bool AntagVerbAllowed(GetVerbsEvent<Verb> args, [NotNullWhen(true)] out ICommonSession? target)
    {
        target = null;

        if (!TryComp<ActorComponent>(args.User, out var actor))
            return false;

        var player = actor.PlayerSession;

        if (!_admin.HasAdminFlag(player, AdminFlags.Fun))
            return false;

        if (!HasComp<MindContainerComponent>(args.Target) || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return false;

        target = targetActor.PlayerSession;
        return true;
    }
}
