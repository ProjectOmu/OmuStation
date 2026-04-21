// SPDX-FileCopyrightText: 2026 Raze500
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.GameTicking.EventDirector;

// this prototype defines the full pacing schedule for a single director configuration.
// everything here is data-driven so maintainers can tune timing and tables without touching c# code.
//
// loop structure:
//   minor fires immediately at roundstart.
//   then every MinorDelayMin-MaxMinutes: roll timer table, then immediately roll minor table.
//   midround fires once independently at FirstMidroundRollMin-MaxMinutes.
[Prototype("eventDirectorConfig")]
public sealed partial class EventDirectorConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // rolled once at roundstart to pick the main antagonist for the round
    [DataField(required: true)]
    public ProtoId<EventDirectorTablePrototype> RoundStartTable;

    // rolled every few minutes throughout the round for lighter antagonist roles (thieves, floor goblins, etc.)
    [DataField(required: true)]
    public ProtoId<EventDirectorTablePrototype> MinorTable;

    // rolled once during the round for a heavier mid-round event (dragon, ninja, blob, etc.)
    // the exact trigger time is randomized between FirstMidroundRollMinMinutes and FirstMidroundRollMaxMinutes
    [DataField(required: true)]
    public ProtoId<EventDirectorTablePrototype> MidroundTable;

    // rolled for environmental and world events (gas leaks, ion storms, solar flares, etc.)
    // timer rolls and minor rolls share the same loop - see EventDirectorSystem for the sequence.
    [DataField(required: true)]
    public ProtoId<EventDirectorTablePrototype> TimerTable;

    // how long to wait between loop iterations (timer roll + minor roll).
    // picked randomly in [min, max] each time the loop resets.
    [DataField]
    public int MinorDelayMinMinutes = 5;

    [DataField]
    public int MinorDelayMaxMinutes = 20;

    // the midround event fires at a random time in this window, counted from roundstart.
    // a wider window (e.g. 40-80) keeps rounds from feeling scripted.
    [DataField]
    public int FirstMidroundRollMinMinutes = 40;

    [DataField]
    public int FirstMidroundRollMaxMinutes = 80;
}
