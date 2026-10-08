// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Decals;

[Serializable, NetSerializable]
public sealed class DecalRotateRequestEvent : EntityEventArgs;
