// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Camera;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.Server.Damage.Systems;

// Trauma - moved here everything to shared
public sealed class DamageOtherOnHitSystem : SharedDamageOtherOnHitSystem;
