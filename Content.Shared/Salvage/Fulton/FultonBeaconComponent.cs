// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.Radio; // Omu
using Robust.Shared.Prototypes; // Omu

namespace Content.Shared.Salvage.Fulton;

/// <summary>
/// Receives <see cref="FultonedComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FultonBeaconComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("soundLink"), AutoNetworkedField]
    public SoundSpecifier? LinkSound = new SoundPathSpecifier("/Audio/Items/beep.ogg");

    /// <summary>
    /// Omu: Send announcement over radio when a fulton is sent?
    /// </summary>
    [DataField]
    public bool AnnounceOnFulton = false;

    /// <summary>
    /// Omu: The radio channel that the fulton announcements are broadcast to.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> AnnouncementChannel = "Supply";

    /// <summary>
    /// Omu: The radio channel that the fulton announcements are broadcast to.
    /// </summary>
    [DataField]
    public string AnnouncementMessage = "fulton-radio-message";
}
