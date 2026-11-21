// SPDX-License-Identifier: MIT

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Crewlet;

[Serializable, NetSerializable]
public sealed partial class CrewletMergeDoAfterEvent : SimpleDoAfterEvent;
