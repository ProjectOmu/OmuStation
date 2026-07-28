using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Mindshield.Components;
using Content.Shared._Impstation.Thaven.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Impstation.Thaven;

public abstract class SharedThavenMoodSystem : EntitySystem
{
    [Dependency] private readonly EmagSystem _emag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThavenMoodsComponent, GotEmaggedEvent>(OnEmagged);
    }

    protected virtual void OnEmagged(Entity<ThavenMoodsComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        // Mindshield blocks emag
        if (HasComp<MindShieldComponent>(ent))
            return;

        // Counter-based limit; MaxWildMoodEmags can be modified by traits
        if (ent.Comp.WildMoodEmagCount >= ent.Comp.MaxWildMoodEmags)
            return;

        ent.Comp.WildMoodEmagCount++;
        // Don't add EmaggedComponent so they can be re-emagged up to the counter limit
        args.Repeatable = true;
        args.Handled = true;
    }
}

[Serializable, NetSerializable]
public sealed partial class ThavenEmagDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}
