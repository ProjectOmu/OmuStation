// SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto <6766154+Zumorica@users.noreply.github.com>
// SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto <gradientvera@outlook.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Slava0135 <40753025+Slava0135@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

namespace Content.Shared.Throwing
{
    /// <summary>
    ///     Base class for all throw events.
    /// </summary>
    public abstract class ThrowEvent : HandledEntityEventArgs
    {
        ///Nyano - Summary: Allows us to tell who threw the item. It matters!
        /// <summary>
        ///     The entity that threw <see cref="Thrown"/>.
        /// </summary>
        public EntityUid? User { get; }
        // End Nyano code.
        public readonly EntityUid Thrown;
        public readonly EntityUid Target;
        public ThrownItemComponent Component;

        public ThrowEvent(EntityUid? user, EntityUid thrown, EntityUid target, ThrownItemComponent component)
        {
            User = user; //Nyano - Summary: User added.
            Thrown = thrown;
            Target = target;
            Component = component;
        }
    }

    /// <summary>
    ///     Raised directed on the target entity being hit by the thrown entity.
    /// </summary>
    public sealed class ThrowHitByEvent : ThrowEvent
    {
        public ThrowHitByEvent(EntityUid? user, EntityUid thrown, EntityUid target, ThrownItemComponent component) : base(user, thrown, target, component)
        {
        }
    }

    /// <summary>
    ///     Raised directed on the thrown entity that hits another.
    /// </summary>
    public sealed class ThrowDoHitEvent : ThrowEvent
    {
        public ThrowDoHitEvent(EntityUid? user, EntityUid thrown, EntityUid target, ThrownItemComponent component) : base(user, thrown, target, component)
        {
        }
    }
}
