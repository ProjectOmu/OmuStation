// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.GameObjects;
using Robust.Shared.Toolshed;

namespace Content.Omu.Server.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Admin)]
public sealed class EntityRenameCommand : ToolshedCommand
{
    private MetaDataSystem? _metaData;

    [CommandImplementation]
    public IEnumerable<EntityUid> Rename(
        [PipedArgument] IEnumerable<EntityUid> entities,
        string name,
        string description)
    {
        _metaData ??= GetSys<MetaDataSystem>();

        foreach (var ent in entities)
        {
            _metaData.SetEntityName(ent, name);
            _metaData.SetEntityDescription(ent, description);
        }

        return entities;
    }
}
