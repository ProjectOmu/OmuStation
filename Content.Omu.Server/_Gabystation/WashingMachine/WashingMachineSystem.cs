// SPDX-FileCopyrightText: 2025 Doctor-Cpu <77215380+Doctor-Cpu@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GabyChangelog <agentepanela2@gmail.com>
// SPDX-FileCopyrightText: 2025 Will-Oliver-Br <164823659+Will-Oliver-Br@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Forensics;
using Content.Omu.Shared._Gabystation.WashingMachine;
using Content.Shared.Forensics.Components;

namespace Content.Omu.Server._Gabystation.WashingMachine;

public sealed partial class WashingMachineSystem : SharedWashingMachineSystem
{
    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void UpdateForensics(Entity<WashingMachineComponent> ent, HashSet<EntityUid> items)
    {
        //ForensicsComponent for the washing machine
        if (!TryComp<ForensicsComponent>(ent.Owner, out var forensicsWashingMachine))
            return;

        //Remove all possible evidence from the item and add detergent residue
        foreach (var item in items)
        {
            if (!TryComp<ForensicsComponent>(item, out var forensics)) //ForensicsComponent for an item inside the washing machine
                continue;

            forensics.Fibers.Clear();
            forensics.Fingerprints.Clear();

            if(forensics.CanDnaBeCleaned)
                forensics.DNAs.Clear();

            forensics.Residues.Add(Loc.GetString("forensic-residue-colored", ("color", "residue-white"), ("adjective", "residue-powdered")));
        }

        //If the item is capable of leaving fibers, add them to the washing machine itself
        foreach (var item in items)
        {
            if (!TryComp<FiberComponent>(item, out var fiber))
                continue;

            var fiberText = fiber.FiberColor == null
                ? Loc.GetString("forensic-fibers", ("material", fiber.FiberMaterial))
                : Loc.GetString("forensic-fibers-colored", ("color", fiber.FiberColor), ("material", fiber.FiberMaterial));

            forensicsWashingMachine.Fibers.Add(fiberText);
        }
    }
}
