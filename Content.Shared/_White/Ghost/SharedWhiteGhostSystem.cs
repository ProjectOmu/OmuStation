
using Robust.Shared.Serialization;

namespace Content.Shared._White.Ghost;

[Serializable, NetSerializable]
public sealed class GhostReturnToRoundRequest : EntityEventArgs; // Omustation - didn't put this into the SharedGhostSystem because why