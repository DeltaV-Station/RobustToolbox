using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Robust.Shared.Utility
{
    [Serializable, NetSerializable]
    public struct Section
    {
        public FontStyle Style;
        public FontSize Size;
        public TextAlign Alignment;
        public int Color;
        public MetaFlags Meta;
        public string Content;
    }

    [Flags]
    public enum MetaFlags : byte
    {
        Default   = 0,
        Localized = 1,
        // All other values are reserved.
    }

    [Flags]
    public enum FontStyle : byte
    {
        // Single-font styles
        Normal     = 0b0000_0000,
        Bold       = 0b0000_0001,
        Italic     = 0b0000_0010,
        Monospace  = 0b0000_0100,
        BoldItalic = Bold | Italic,

        // Escape value
        Special    = 0b1000_0000,

        // The lower four bits are available for styles to specify.
        Standard   = 0b0100_0000 | Special,

        // All values not otherwise specified are reserved.
    }

    [Flags]
    public enum FontSize : ushort
    {
        // Format (Standard): 0bSNNN_NNNN_NNNN_NNNN
        // S: Special flag.
        // N (where S == 0): Font size. Unsigned.
        // N (where S == 1): Special operation; see below.

        // Flag to indicate the TagFontSize is "special".
        // All values not specified are reserved.
        // General Format: 0b1PPP_AAAA_AAAA_AAAA
        // P: Operation.
        // A: Arguments.
        Special  = 0b1000_0000_0000_0000,

        // RELative Plus.
        // Format: 0b1100_NNNN_NNNN_NNNN
        // N: Addend to the previous font size. Unsigned.
        RelPlus  = 0b0100_0000_0000_0000 | Special,

        // RELative Minus.
        // Format: 0b1010_NNNN_NNNN_NNNN
        // N: Subtrahend to the previous font size. Unsigned.
        RelMinus = 0b0010_0000_0000_0000 | Special,

        // Selects a font size from the stylesheet.
        // Format: 0b1110_NNNN_NNNN_NNNN
        // N: The identifier of the preset font size.
        Standard = 0b0110_0000_0000_0000 | Special,
    }

    public enum TextAlign : byte
    {
        // Format: 0bHHHH_VVVV
        // H: Horizontal alignment
        // V: Vertical alignment.
        // All values not specified are reserved.

        // This seems dumb to point out, but ok
        Default     = Baseline | Left,

        // Vertical alignment
        Baseline    = 0x00,
        Top         = 0x01,
        Bottom      = 0x02,
        Superscript = 0x03,
        Subscript   = 0x04,

        // Horizontal alignment
        Left        = 0x00,
        Right       = 0x10,
        Center      = 0x20,
        Justify     = 0x30,
    }

    public static partial class Extensions
    {
        public static TextAlign Vertical (this TextAlign value) => (TextAlign)((byte) value & 0x0F);
        public static TextAlign Horizontal (this TextAlign value) => (TextAlign)((byte) value & 0xF0);
    }

    [PublicAPI]
    [Serializable, NetSerializable]
    public sealed record FormattedMessage(Section[] Sections)
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var i in Sections)
                sb.Append(i.Content);

            return sb.ToString();
        }

        // I don't wanna fix the serializer yet.
        public string ToMarkup()
        {
            var sb = new StringBuilder();
            foreach (var i in Sections)
            {
                sb.AppendFormat("[color=#{0:X}]", i.Color);
                sb.Append(i.Content);
                sb.Append("[/color]");
            }

            return sb.ToString();
        }

        // are you a construction worker?
        // cuz you buildin
        public class Builder
        {
            private bool _dirty = false;
            private int _idx = 0;
            private StringBuilder _sb = new();
            private List<Section> _work = new() {
                new Section()
            };

            public static Builder FromFormattedText(FormattedMessage orig) => new ()
            {
                _idx = orig.Sections.Length < 0 ? orig.Sections.Length - 1 : 0,
                _work = new List<Section>(orig.Sections),
            };

            public void Clear()
            {
                _idx = 0;
                _work = new() {
                    new Section()
                };
                _sb = _sb.Clear();
            }

            public void AddText(string text)
            {
                _dirty = true;
                _sb.Append(text);
            }

            public void PushColor(Color color)
            {
                flushWork();
                _idx++;
                var lidx = _work.Count > 0 ? _work.Count - 1 : 0;
                var last = _work[lidx];
                last.Color = color.ToArgb();
                _work[lidx] = last;
            }

            public void AddMessage(FormattedMessage other) =>
                _work.AddRange(other.Sections);

            public void AddMessage(FormattedMessage.Builder other) =>
                _work.AddRange(other._work);

            public void PushNewline()
            {
                _dirty = true;
                _sb.Append('\n');
            }

            public void Pop()
            {
                flushWork();
                _idx = _idx < 0 ? _idx-1 : 0;
            }

            public void flushWork()
            {
                if (!_dirty)
                    return;

                var lidx = _work.Count > 0 ? _work.Count - 1 : 0;
                var last = _work[lidx];
                last.Content = _sb.ToString();
                _sb = _sb.Clear();
                _work.Add(last);
            }

            public FormattedMessage Build() => new FormattedMessage(_work.ToArray());
        }
    }
}
