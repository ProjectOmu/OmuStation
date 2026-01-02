// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared.Throwing;
using Robust.Client.Physics;

namespace Content.Client._Trauma.Throwing;

/// <summary>
/// Lets thrown items' physics be predicted.
/// </summary>
public sealed class PredictedThrowingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThrownItemComponent, UpdateIsPredictedEvent>(OnUpdateIsPredicted);
    }

    private void OnUpdateIsPredicted(Entity<ThrownItemComponent> ent, ref UpdateIsPredictedEvent args)
    {
        args.IsPredicted = true;
    }
}
