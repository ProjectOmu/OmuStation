// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Omu.Shared.Traits;

[RegisterComponent, NetworkedComponent] //Needs NetworkedComponent to sync text to item user too.
public sealed partial class DamagedThroatStethoscopeComponent : Component;
