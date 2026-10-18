// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Entities.Voodoo;

[Serializable, NetSerializable]
public sealed partial class VoodooBindDoAfterEvent : DoAfterEvent
{
    public NetEntity Victim;

    public VoodooBindDoAfterEvent(NetEntity victim)
    {
        Victim = victim;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}
