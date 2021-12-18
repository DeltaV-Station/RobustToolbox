using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Robust.Client.UserInterface
{
    public static class TextLayout
    {
        /// <summary>
        /// An Offset is a simplified instruction for rendering a text block.
        /// </summary>
        ///
        /// <remarks>
        /// Pseudocode for rendering:
        /// <code>
        ///     (int x, int y) topLeft = (10, 20);
        ///     var libn = style.FontLib.StartFont(defaultFontID, defaultFontStyle, defaultFontSize);
        ///     foreach (var r in returnedWords)
        ///     {
        ///          var section = Message.Sections[section];
        ///          var font = libn.Update(section.Style, section.Size);
        ///          font.DrawAt(
        ///              text=section.Content.Substring(charOffs, length),
        ///              x=topLeft.x + r.x,
        ///              y=topLeft.y + r.y,
        ///              color=new Color(section.Color)
        ///          )
        ///     }
        /// </code>
        /// </remarks>
        ///
        /// <param name="section">
        /// The index of the backing store (usually a <see cref="Robust.Shared.Utility.Section"/>) in the
        /// container (usually a <see cref="Robust.Shared.Utility.FormattedMessage"/>) to which the Offset belongs.
        /// </param>
        /// <param name="charOffs">The byte offset in to the <see cref="Robust.Shared.Utility.Section.Content"/> to render.</param>
        /// <param name="length">The number of bytes after <paramref name="charOffs"/> to render.</param>
        /// <param name="x">The offset from the base position's x coordinate to render this chunk of text.</param>
        /// <param name="y">The offset from the base position's y coordinate to render this chunk of text.</param>
        /// <param name="w">The width the word (i.e. the sum of all its <c>Advance</c>'s).</param>
        /// <param name="h">The height of the tallest character's <c>BearingY</c>.</param>
        /// <param name="ln">The line number that the word is assigned to.</param>
        /// <param name="spw">The width allocated to this word.</param>
        /// <param name="wt">The detected word type.</param>
        public record struct Offset
        {
            public int section;
            public int charOffs;
            public int length;
            public int x;
            public int y;
            public int h;
            public int w;
            public int ln;
            public int spw;
            public WordType wt;
        }

        public enum WordType : byte
        {
            Normal,
            Space,
            LineBreak,
        }

        public static ImmutableArray<Offset> Layout(
                ISectionable text,
                int w,
                IFontLibrary fonts,
                float scale = 1.0f,
                int lineSpacing = 0,
                int wordSpacing = 0,
                int runeSpacing = 0,
                FontClass? fclass = default,
                LayoutOptions options = LayoutOptions.Default
        ) => Layout(
            text,
            Split(text, fonts, scale, wordSpacing, runeSpacing, fclass, options),
            w,
            fonts,
            scale,
            lineSpacing, wordSpacing,
            fclass,
            options
        );

        // Actually produce the layout data.
        // The algorithm is basically ripped from CSS Flexbox.
        //
        // 1. Add up all the space each word takes
        // 2. Subtract that from the line width (w)
        // 3. Save that as the free space (fs)
        // 4. Add up each gap's priority value (Σpri)
        // 5. Assign each gap a final priority (fp) of ((priMax - pri) / Σpri)
        // 6. That space has (fp*fs) pixels.
        public static ImmutableArray<Offset> Layout(
                ISectionable src,
                ImmutableArray<Offset> text,
                int w,
                IFontLibrary fonts,
                float scale = 1.0f,
                int lineSpacing = 0,
                int wordSpacing = 0,
                FontClass? fclass = default,
                LayoutOptions options = LayoutOptions.Default
        )
        {
            // how about no
            if (w == 0)
                return ImmutableArray<Offset>.Empty;

            var lw = new WorkQueue<(
                    List<Offset> wds,
                    List<int> gaps,
                    int lnrem,
                    int sptot,
                    int maxPri,
                    int tPri,
                    int lnh
            )>(postcreate: i => i with
            {
                wds = new List<Offset>(),
                gaps = new List<int>()
            });

            var flib = fonts.StartFont(fclass);
            var lastAlign = TextAlign.Left;
            var wdq = new List<Offset>(text);

            // Calculate line boundaries
            for (var i = 0; i < wdq.Count; i++)
            {
                var wd = wdq[i];
                var sec = src[wd.section];
                var hz = sec.Alignment.Horizontal();
                var sf = flib.Update(sec.Style, sec.Size);
                (int gW, int adv) = TransitionWeights(lastAlign, hz);
                lastAlign = hz;

                lw.Work.gaps.Add(gW+lw.Work.maxPri);
                lw.Work.tPri += gW+lw.Work.maxPri;
                lw.Work.maxPri += adv;
                lw.Work.lnh = Math.Max(lw.Work.lnh, wd.h);

                if (wd.wt == WordType.LineBreak)
                {
                    lw.Flush();
                    lw.Work.lnrem = w;
                    lw.Work.maxPri = 1;
                }
                else if (lw.Work.lnrem < wd.w)
                {
                    lw.Flush();
                    if (!options.HasFlag(LayoutOptions.NoWordSplit))
                    {

                        // Chop the current word in half (or more...)
                        var o = SubWordSplit(
                            src: src,
                            text: wd,
                            maxw: w,
                            w: lw.Work.lnrem,
                            font: flib.Current,
                            scale: scale,
                            options: options
                        );

                        // Swap out the Offset we're working on for whatever it spits out
                        wdq[i] = wd = o[0];

                        // and add any remaining ones.
                        if (o.Length > 1)
                            wdq.InsertRange(i+1, o.Skip(1));

                    }
                    lw.Work.lnrem = w;
                    lw.Work.maxPri = 1;
                }

                lw.Work.sptot += wd.spw;
                lw.Work.lnrem -= wd.w + wd.spw;
                lw.Work.wds.Add(wd);
            }
            lw.Flush(true);

            flib = fonts.StartFont(fclass);
            int py = flib.Current.GetAscent(scale);
            int lnnum = 0;
            foreach (var (ln, gaps, lnrem, sptot, maxPri, tPri, lnh) in lw.Done)
            {
                int px=0, maxlh=0;

                var spDist = new int[gaps.Count];
                for (int i = 0; i < gaps.Count; i++)
                    spDist[i] = (int) (((float) gaps[i] / (float) tPri) * (float) sptot);

                int prevAsc=0, prevDesc=0;
                for (int i = 0; i < ln.Count; i++)
                {
                    var ss = src[ln[i].section];
                    var sf = flib.Update(ss.Style, ss.Size);
                    var asc = sf.GetAscent(scale);
                    var desc = sf.GetDescent(scale);
                    maxlh = Math.Max(maxlh, sf.GetAscent(scale));

                    if (i - 1 > 0 && i - 1 < spDist.Length)
                    {
                        px += spDist[i - 1] / 2;
                    }

                    ln[i] = ln[i] with {
                        x = px,
                        y = py + ss.Alignment.Vertical() switch {
                            TextAlign.Baseline => 0,
                            TextAlign.Bottom => -(desc - prevDesc), // Scoot it up by the descent
                            TextAlign.Top => (asc - prevAsc),
                            TextAlign.Subscript => -ln[i].h / 8,  // Technically these should be derived from the font data,
                            TextAlign.Superscript => ln[i].h / 4, // but I'm not gonna bother figuring out how to pull it from them.
                            _ => 0,
                        },
                        ln = lnnum,
                    };

                    if (i < spDist.Length)
                    {
                        px += spDist[i] / 2 + ln[i].w;
                    }

                    prevAsc = asc;
                    prevDesc = desc;
                }
                py += options.HasFlag(LayoutOptions.UseRenderTop) ? lnh : (lineSpacing + maxlh);

                lnnum++;
            }

            return lw.Done.SelectMany(e => e.wds).ToImmutableArray();
        }

        private static (int gapPri, int adv) TransitionWeights (TextAlign l, TextAlign r)
        {
            l = l.Horizontal();
            r = r.Horizontal();

            // Technically these could be slimmed down, but it's as much to help explain the system
            // as it is to implement it.

            // p (aka gapPri) is how high up the food chain each gap should be.
            // _LOWER_ p means more (since we do first-come first-serve).

            // a (aka adv) is how much we increment the gapPri counter, meaning how much less important
            // future alignment changes are.

            // Left alignment.
            (int p, int a) la = (l, r) switch {
                (   TextAlign.Left,     TextAlign.Left) => (0, 0), // Left alignment doesn't care about inter-word spacing
                (                _,     TextAlign.Left) => (0, 0), // or anything that comes before it,
                (   TextAlign.Left,                  _) => (1, 1), // only what comes after it.
                (                _,                  _) => (0, 0)
            };

            // Right alignment
            (int p, int a) ra = (l, r) switch {
                (  TextAlign.Right,    TextAlign.Right) => (0, 0), // Right alignment also does not care about inter-word spacing,
                (                _,    TextAlign.Right) => (1, 1), // but it does care what comes before it,
                (  TextAlign.Right,                  _) => (0, 0), // but not after.
                (                _,                  _) => (0, 0)
            };

            // Centering
            (int p, int a) ca = (l, r) switch {
                ( TextAlign.Center,   TextAlign.Center) => (0, 0), // Centering still doesn't care about inter-word spacing,
                (                _,   TextAlign.Center) => (1, 0), // but it cares about both what comes before it,
                ( TextAlign.Center,                  _) => (1, 1), // and what comes after it.
                (                _,                  _) => (0, 0)
            };

            // Justifying
            (int p, int a) ja = (l, r) switch {
                (TextAlign.Justify,  TextAlign.Justify) => (1, 0), // Justification cares about inter-word spacing.
                (                _,  TextAlign.Justify) => (0, 1), // And (sort of) what comes before it.
                (                _,                  _) => (0, 0)
            };

            return new
            (
                    la.p + ra.p + ca.p + ja.p,
                    la.a + ra.a + ca.a + ja.a
            );
        }

        // Split creates a list of words broken based on their boundaries.
        // Users are encouraged to reuse this for as long as it accurately reflects
        // the content they're trying to display.
        public static ImmutableArray<Offset> Split(
                ISectionable text,
                IFontLibrary fonts,
                float scale,
                int wordSpacing,
                int runeSpacing,
                FontClass? fclass,
                LayoutOptions options = LayoutOptions.Default
        )
        {
            var nofb = options.HasFlag(LayoutOptions.NoFallback);

            var s=0;
            var lsbo=0;
            var sbo=0;
            var wq = new WorkQueue<Offset>(
                    w =>
                    {
                        var len = sbo-lsbo;
                        lsbo = sbo;
                        return w with { length=len };
                    },
                    default,
                    default,
                    w => w with { section=s, charOffs=sbo }
            );

            var flib = fonts.StartFont(fclass);
            for (s = 0; s < text.Length; s++)
            {
                var sec = text[s];

                if (sec.Meta != default)
                    throw new Exception("Section with unknown or unimplemented Meta flag");

                lsbo = 0;
                sbo = 0;
                var fnt = flib.Update(sec.Style, sec.Size);
                wq.Reset();

                foreach (var r in sec.Content.EnumerateRunes())
                {
                    if (r == (Rune) '\n')
                    {
                        wq.Flush();
                        wq.Work.wt = WordType.LineBreak;
                    }
                    else if (Rune.IsSeparator(r))
                    {
                        if (wq.Work.wt != WordType.Space)
                        {
                            wq.Work.w += wordSpacing;
                            wq.Flush();
                            wq.Work.wt = WordType.Space;
                        }
                    }
                    else if (wq.Work.wt != WordType.Normal)
                        wq.Flush();

                    #warning TODO: Check this w/ someone that knows C# string semantics
                    sbo += r.Utf16SequenceLength;
                    var cm = fnt.GetCharMetrics(r, scale, !nofb);

                    if (!cm.HasValue)
                    {
                        if (nofb)
                            continue;
                        else if (fnt is DummyFont)
                            cm = new CharMetrics();
                        else
                            throw new Exception("unable to get character metrics");
                    }

                    wq.Work.h = Math.Max(wq.Work.h, cm.Value.Height);
                    wq.Work.w += cm.Value.Advance;
                    if (wq.Work.wt == WordType.Normal)
                        wq.Work.spw = runeSpacing;
                }
                wq.Flush(true);
            }


            return wq.Done.ToImmutableArray();
        }

        /// <summary>
        /// SubWordSplit takes the output of <see cref="Split(ISectionable, IFontLibrary, float, int, int, FontClass?, LayoutOptions)"/>
        /// and splits one <see cref="WordType.Normal"/> <see cref="Offset"/> at the end of the line in to one or more Offsets that do
        /// not overflow the current line (of width <paramref name="w"/>), and a max line width of <paramref name="maxw"/>.
        /// </summary>
        /// <remarks>
        /// This will spectacularly fail to obey the rules for splitting <see cref="WordType.LineBreak"/> or <see cref="WordType.Space"/>.
        /// </remarks>
        public static ImmutableArray<Offset> SubWordSplit(
                ISectionable src,
                Offset text,
                int maxw,
                int w,
                Font font,
                float scale = 1.0f,
                LayoutOptions options = default
        )
        {
            var sws = ImmutableArray.CreateBuilder<Offset>();
            var nofb = options.HasFlag(LayoutOptions.NoFallback);

            // Section charOffs & length
            var sco = text.charOffs;
            var scl = 0;

            // Starting line width
            var slw = w;

            foreach (var r in src[text.section]
                    .Content.Substring(text.charOffs, text.length)
                    .EnumerateRunes())
            {
                // Get rune data
                var cm = font.GetCharMetrics(r, scale, !nofb);
                var u16l = r.Utf16SequenceLength;

                if (!cm.HasValue)
                {
                    // No characer? Ignore it and move on.
                    if (nofb)
                    {
                        scl += u16l;
                        continue;
                    }
                    else if (font is DummyFont)
                        cm = new CharMetrics();
                    else
                        throw new Exception("unable to get character metrics");
                }

                // Do we overflow the current line?
                if (w + cm.Value.Advance > maxw)
                {
                    // Is there anything we need to save?
                    if (scl > 0)
                    {
                        // Yep, save it and reset the section length.
                        sws.Add(text with { charOffs=sco, length=scl, w=w-slw });
                        sco += scl;
                        scl=0;
                    }

                    // Reset the line metrics.
                    slw=0;
                    w=0;
                }

                // Include the character in the section
                scl += u16l;

                // and scoot the X cursor forward
                w += cm.Value.Advance;
            }

            // Make sure to add any left-over stuff.
            if (scl > 0)
                sws.Add(text with { charOffs=sco, length=scl, w=w-slw });

            return sws.ToImmutableArray();
        }

        [Flags]
        public enum LayoutOptions : byte
        {
            Default      = 0b0000_0000,

            // Measure the actual height of runes to space lines.
            UseRenderTop = 0b0000_0001,

            // NoFallback disables the use of the Fallback character.
            NoFallback   = 0b0000_0010,

            // NoWordSplit disable splitting words that run over the line boundary.
            NoWordSplit  = 0b0000_0100,
        }

        // WorkQueue is probably a misnomer. All it does is streamline a pattern I ended up using
        // repeatedly where I'd have a list of something and a WIP, then I'd flush the WIP in to
        // the list.
        private class WorkQueue<TIn, TOut>
            where TIn : new()
        {
            // _blank creates a new T if _refresh says it needs to.
            private Func<TIn> _blank = () => new TIn();
            private Func<TIn, TIn>? _postcr;

            private Func<TIn, bool> _check = _ => true;

            private Func<TIn, TOut> _conv;

            public List<TOut> Done = new();
            public TIn Work;

            public WorkQueue(
                    Func<TIn, TOut> conv,
                    Func<TIn>? blank = default,
                    Func<TIn, bool>? check = default,
                    Func<TIn, TIn>? postcreate = default
            )
            {
                _conv = conv;

                if (blank is not null)
                    _blank = blank;

                if (check is not null)
                    _check = check;

                if (postcreate is not null)
                    _postcr = postcreate;

                Work = _blank.Invoke();

                if (_postcr is not null)
                    Work = _postcr.Invoke(Work);
            }

            public void Reset()
            {
                Work = _blank.Invoke();
                if (_postcr is not null)
                    Work = _postcr.Invoke(Work);
            }

            public void Flush(bool force = false)
            {
                if (_check.Invoke(Work) || force)
                {
                    Done.Add(_conv(Work));
                    Work = _blank.Invoke();
                    if (_postcr is not null)
                        Work = _postcr.Invoke(Work);
                }
            }
        }

        private class WorkQueue<T> : WorkQueue<T, T>
            where T : new()
        {
            private static Func<T, T> __conv = i => i;
            public WorkQueue(
                    Func<T, T>? conv = default,
                    Func<T>? blank = default,
                    Func<T, bool>? check = default,
                    Func<T, T>? postcreate = default
            ) : base(conv ?? __conv, blank, check, postcreate)
            {
            }
        }
    }
}
