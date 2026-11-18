using Content.Omu.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

namespace Content.Omu.Server.Speech.EntitySystems;

public sealed class NoContractionsAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NoContractionsAccentComponent, AccentGetEvent>(OnAccent);
    }

    public string Accentuate(string message)
    {
        var accentedMessage = _replacement.ApplyReplacements(message, "nocontractions");
        return accentedMessage;
    }

    private void OnAccent(EntityUid uid, NoContractionsAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}

