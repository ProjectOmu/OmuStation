// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared.GameTicking.Components;

/// <summary>
///     Added to game rules before <see cref="GameRuleStartedEvent"/> and removed before <see cref="GameRuleEndedEvent"/>.
///     Mutually exclusive with <seealso cref="EndedGameRuleComponent"/>.
/// </summary>
/// <remarks>
///     This component is networked specifically so that shared/client code can test whether a rule is
///     currently active (pair it with the rule's own networked component, e.g.
///     <c>EntityQueryEnumerator&lt;MyRuleComponent, ActiveGameRuleComponent&gt;()</c>).
///     <see cref="GameRuleComponent"/> is deliberately NOT networked - it is server-only bookkeeping -
///     so it must never appear in a query that shared or client code runs, or that query can never match
///     on the client and will silently diverge from the server.
/// </remarks>
[RegisterComponent, NetworkedComponent] // Goob edit
public sealed partial class ActiveGameRuleComponent : Component;