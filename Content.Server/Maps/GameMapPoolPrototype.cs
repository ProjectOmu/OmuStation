// SPDX-License-Identifier: MIT

using Content.Shared.Maps;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes; // Omu - inheritable pools
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array; // Omu - inheritable pools
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

namespace Content.Server.Maps;

/// <summary>
/// Prototype that holds a pool of maps that can be indexed based on the map pool CCVar.
/// </summary>
[Prototype, PublicAPI]
public sealed partial class GameMapPoolPrototype : IPrototype, IInheritingPrototype // Omu - inheritable pools
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    // Omu start - pools can inherit maps from parent pools
    [ParentDataField(typeof(PrototypeIdArraySerializer<GameMapPoolPrototype>))]
    public string[]? Parents { get; private set; }

    // Omu - whether this pool can be in
    [AbstractDataField]
    [NeverPushInheritance]
    public bool Abstract { get; private set; }
    // Omu end

    /// <summary>
    ///     Which maps are in this pool.
    /// 
    ///     Omu - maps from parent pools can now be inherited
    /// </summary>
    [DataField("maps", customTypeSerializer:typeof(PrototypeIdHashSetSerializer<GameMapPrototype>))]
    [AlwaysPushInheritance] // Omu - don't overwrite the parents maps just add to them
    public HashSet<string> Maps = new(0);
}
