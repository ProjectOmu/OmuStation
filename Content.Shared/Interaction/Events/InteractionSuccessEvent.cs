// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Interaction.Events;

/// <summary>
/// Raised on the target when successfully petting/hugging something.
/// Actor as a nullable variable to add more ways to use this event (OMU)
/// </summary>
// TODO INTERACTION
// Rename this, or move it to another namespace to make it clearer that this is specific to "petting/hugging" (InteractionPopupSystem)
[ByRefEvent]
public readonly record struct InteractionSuccessEvent(EntityUid User, EntityUid? Actor = null); // Omu, added an "Actor" variable
