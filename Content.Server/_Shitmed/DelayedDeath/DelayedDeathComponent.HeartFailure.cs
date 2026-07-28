// SPDX-FileCopyrightText: 2025 ThanosDeGraf <richardgirgindontstop@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Shitmed.DelayedDeath;

// Omu Station
public sealed partial class DelayedDeathComponent : Component
{

    /// <summary>
    /// Set to true if death caused by heart failure, duh.
    /// Important so we don't accidentally let a debrained guy walk away because of a fixed heart attack
    /// </summary>
    [DataField]
    public bool FromHeartFailure = false;
}
