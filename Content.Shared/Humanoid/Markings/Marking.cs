// SPDX-FileCopyrightText: 2022 Flipp Syder <76629141+vulppine@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 csqrb <56765288+CaptainSqrBeard@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid.Markings
{
    [DataDefinition]
    [Serializable, NetSerializable]
    public sealed partial class Marking : IEquatable<Marking>, IComparable<Marking>, IComparable<string>
    {
        [DataField("markingColor")]
        private List<Color> _markingColors = new();

        // Omu begin
        /// <summary>
        /// A bitwise index of which markingColors are glowing.
        /// </summary>
        [DataField]
        public uint GlowyBits { get; private set; }
        // Omu end

        private Marking()
        {
        }

        // Omu begin
        public Marking(string markingId,
            List<Color> markingColors,
            bool glowy) // Omu
        {
            MarkingId = markingId;
            _markingColors = markingColors;
            SetGlowing(glowy);
        }
        // Omu end

        public Marking(string markingId,
            List<Color> markingColors,
            uint glowyBits) // Omu
        {
            MarkingId = markingId;
            _markingColors = markingColors;
            GlowyBits = glowyBits; // Omu
        }

        // Omu begin
        public Marking(string markingId,
            IReadOnlyList<Color> markingColors,
            bool isGlowy)
            : this(markingId, new List<Color>(markingColors), isGlowy)
        {
        }
        // Omu end

        public Marking(string markingId,
            IReadOnlyList<Color> markingColors,
            uint glowyBits)
            : this(markingId, new List<Color>(markingColors), glowyBits) // Omu
        {
        }

        public Marking(string markingId, int colorCount)
        {
            MarkingId = markingId;
            List<Color> colors = new();
            for (int i = 0; i < colorCount; i++)
                colors.Add(Color.White);
            _markingColors = colors;
        }

        public Marking(Marking other)
        {
            MarkingId = other.MarkingId;
            _markingColors = new(other.MarkingColors);
            Visible = other.Visible;
            Forced = other.Forced;
            GlowyBits = other.GlowyBits; // Omu
        }

        /// <summary>
        ///     ID of the marking prototype.
        /// </summary>
        [DataField("markingId", required: true)]
        public string MarkingId { get; private set; } = default!;

        /// <summary>
        ///     All colors currently on this marking.
        /// </summary>
        [ViewVariables]
        public IReadOnlyList<Color> MarkingColors => _markingColors;

        /// <summary>
        ///     If this marking is currently visible.
        /// </summary>
        [DataField("visible")]
        public bool Visible = true;

        /// <summary>
        ///     If this marking should be forcefully applied, regardless of points.
        /// </summary>
        [ViewVariables]
        public bool Forced;

        public void SetColor(int colorIndex, Color color) =>
            _markingColors[colorIndex] = color;

        public void SetColor(Color color)
        {
            for (int i = 0; i < _markingColors.Count; i++)
            {
                _markingColors[i] = color;
            }
        }

        // Omu begin
        /// <summary>
        /// Sets the whole bit representation to 1 or 0, meaning either all colors in the marking are glowing or not glowing.
        /// </summary>
        /// <param name="isGlowing">If it should glow or not.</param>
        public void SetGlowing(bool isGlowing)
        {
            GlowyBits = isGlowing ? uint.MaxValue : 0;
        }

        /// <summary>
        /// Sets the glowing state of a specific color index in the marking.
        /// </summary>
        /// <param name="colorIndex">The color index of the marking to set the glowing state of.</param>
        /// <param name="isGlowing">If it should glow or not.</param>
        public void SetGlowing(int colorIndex, bool isGlowing)
        {
            if (isGlowing)
                GlowyBits |= (uint) (1 << colorIndex);
            else
                GlowyBits &= (uint) ~(1 << colorIndex);
        }

        /// <summary>
        /// Gets the glowing state of a specific color index in the marking.
        /// </summary>
        /// <param name="colorIndex">The color index of the marking to get the glowing state of.</param>
        /// <returns>If it should glow or not.</returns>
        public bool GetGlowingIndex(int colorIndex)
        {
            return (GlowyBits & (uint) (1 << colorIndex)) != 0;
        }
        // Omu End

        public int CompareTo(Marking? marking)
        {
            if (marking == null)
            {
                return 1;
            }

            return string.Compare(MarkingId, marking.MarkingId, StringComparison.Ordinal);
        }

        public int CompareTo(string? markingId)
        {
            if (markingId == null)
                return 1;

            return string.Compare(MarkingId, markingId, StringComparison.Ordinal);
        }

        public bool Equals(Marking? other)
        {
            if (other == null)
            {
                return false;
            }
            return MarkingId.Equals(other.MarkingId)
                && _markingColors.SequenceEqual(other._markingColors)
                && Visible.Equals(other.Visible)
                && Forced.Equals(other.Forced)
                && GlowyBits.Equals(other.GlowyBits); // Omu
        }

        // VERY BIG TODO: TURN THIS INTO JSONSERIALIZER IMPLEMENTATION


        // look this could be better but I don't think serializing
        // colors is the correct thing to do
        //
        // this is still janky imo but serializing a color and feeding
        // it into the default JSON serializer (which is just *fine*)
        // doesn't seem to have compatible interfaces? this 'works'
        // for now but should eventually be improved so that this can,
        // in fact just be serialized through a convenient interface
        new public string ToString()
        {
            // reserved character
            string sanitizedName = this.MarkingId.Replace('@', '_');
            List<string> colorStringList = new();
            foreach (Color color in _markingColors)
                colorStringList.Add(color.ToHex());

            return $"{sanitizedName}@{string.Join(',', colorStringList)}@{GlowyBits}"; // Omu
        }

        public static Marking? ParseFromDbString(string input)
        {
            if (input.Length == 0) return null;
            var split = input.Split('@');

            // Omu begin
            if (split.Length == 2)
            {
                List<Color> colorList = new();
                foreach (string color in split[1].Split(','))
                    colorList.Add(Color.FromHex(color));

                return new Marking(split[0], colorList, 0); // Omu
            }

            if (split.Length == 3)
            {
                var colorList = split[1].Split(',').Select(color => Color.FromHex(color)).ToList();
                var isGlowing = uint.Parse(split[2]);

                return new Marking(split[0], colorList, isGlowing);
            }

            return null;
            // Omu End
        }
    }
}
