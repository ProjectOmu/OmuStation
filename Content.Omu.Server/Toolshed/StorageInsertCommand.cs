// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Administration;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Omu.Server.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Admin)]
public sealed class StorageInsertCommand : ToolshedCommand
{
    private StorageSystem? _storage;

    [CommandImplementation]
    public IEnumerable<EntityUid> Insert(
        [PipedArgument] IEnumerable<EntityUid> containers,
        ProtoId<EntityPrototype> protoId)
    {
        _storage ??= GetSys<StorageSystem>();

        foreach (var container in containers)
        {
            if (!HasComp<StorageComponent>(container))
                continue;

            var spawned = Spawn(protoId, Transform(container).Coordinates);

            if (!_storage.Insert(container, spawned, out _, playSound: false))
                Del(spawned);
        }

        return containers;
    }
}
