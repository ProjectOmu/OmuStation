// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.CollectiveMind
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class CollectiveMindComponent : Component
    {
        [DataField("minds")]
        public Dictionary<string, int> Minds = new();

        [DataField, AutoNetworkedField]
        public ProtoId<CollectiveMindPrototype>? DefaultChannel = null;

        [DataField, AutoNetworkedField]
        public HashSet<ProtoId<CollectiveMindPrototype>> Channels = new();

        [DataField]
        public bool HearAll = false;

        [DataField]
        public bool SeeAllNames = false;

        [DataField]
        public bool RespectAccents = false;

        // Goobstation
        /// <summary>
        /// Whether the collective mind can be used while in critical condition
        /// </summary>
        [DataField]
        public bool CanUseInCrit = false;
    }
}
