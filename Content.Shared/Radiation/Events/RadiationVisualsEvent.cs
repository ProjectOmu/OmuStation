// SPDX-FileCopyrightText: 2026 puntsss <bex.ish.aholic@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared.Radiation.Events;

[Serializable, NetSerializable]
public sealed class RadiationVisualsEvent : EntityEventArgs
{
    public float Intensity { get; }
    public float Duration { get; }

    public RadiationVisualsEvent(float intensity, float duration)
    {
        Intensity = intensity;
        Duration = duration;
    }
}
