// SPDX-FileCopyrightText: 2021 metalgearsloth <metalgearsloth@gmail.com>
// SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

namespace Content.Shared.Shuttles.Components
{
    public abstract partial class SharedDockingComponent : Component
    {
        // Yes I left this in for now because there's no overhead and we'll need a client one later anyway
        // and I was too lazy to delete it.

        public abstract bool Docked { get; }

        /// <summary>
        /// Frontier: type of dock.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite), DataField]
        public DockType DockType = DockType.Airlock;

        /// <summary>
        /// Frontier: if true, can only receive docking, cannot initialize.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite), DataField]
        public bool ReceiveOnly = false;
    }

    // Frontier: prevent mismatched dock types from docking
    [Flags]
    public enum DockType : byte
    {
        None = 0,
        Airlock = 1 << 0,
        Gas = 1 << 1,
    }
    // End Frontier
}
