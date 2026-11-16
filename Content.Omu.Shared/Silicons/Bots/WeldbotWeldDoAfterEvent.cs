// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Silicons.Bots;

[Serializable, NetSerializable]
public sealed partial class WeldbotWeldDoAfterEvent : SimpleDoAfterEvent;
