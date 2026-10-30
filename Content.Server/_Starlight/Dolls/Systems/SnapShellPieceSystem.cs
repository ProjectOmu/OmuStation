// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Shared._StarLight.Dolls.Components;
using Content.Shared._StarLight.Dolls.Events;
using Content.Shared.Actions;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._StarLight.Dolls.Systems;

public sealed class SnapShellPieceSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SnapShellPieceEvent>(OnSnapShellPiece);
    }

    private void OnSnapShellPiece(SnapShellPieceEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;

        if (args.RequiresFreeHand && _hands.GetEmptyHandCount(user) == 0)
            return;

        var fragments = new List<EntityUid>();

        foreach (var (organId, _) in _body.GetBodyOrgans(user))
        {
            if (HasComp<OrganShellComponent>(organId))
                fragments.Add(organId);
        }

        if (fragments.Count == 0)
            return;

        var fragment = _random.Pick(fragments);

        if (!_body.TryRemoveOrgan(fragment))
            return;

        // Play the quiet snapping sound.
        _audio.PlayPvs(
            new SoundPathSpecifier("/Audio/Effects/Emotes/snap1.ogg"),
            user);

        // Place the removed shell fragment into the Doll's hand.
        _hands.TryPickupAnyHand(user, fragment);

        if (args.SelfDamage != null)
            _damage.TryChangeDamage(user, args.SelfDamage, true);

        args.Handled = true;
    }
}