using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Graphics.Drawing;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Robust.Client.UserInterface
{
    /// <summary>
    ///     Used by <see cref="OutputPanel"/> and <see cref="RichTextLabel"/> to handle rich text layout.
    /// </summary>
    internal struct RichTextEntry
    {
        private static readonly FormattedMessage.TagColor TagWhite = new FormattedMessage.TagColor(Color.White);

        public readonly FormattedMessage Message;

        /// <summary>
        ///     The vertical size of this entry, in pixels.
        /// </summary>
        public int Height;

        /// <summary>
        ///     The combined text indices in the message's text tags to put line breaks.
        /// </summary>
        public readonly List<int> LineBreaks;

        public RichTextEntry(FormattedMessage message)
        {
            Message = message;
            Height = 0;
            LineBreaks = new List<int>();
        }

        /// <summary>
        ///     Recalculate line dimensions and where it has line breaks for word wrapping.
        /// </summary>
        /// <param name="font">The font being used for display.</param>
        /// <param name="sizeX">The horizontal size of the container of this entry.</param>
        public void Update(Font font, float sizeX)
        {
            DebugTools.Assert(!GameController.OnGodot);
            // This method is gonna suck due to complexity.
            // Bear with me here.
            // I am so deeply sorry for the person adding stuff to this in the future.
            Height = font.Height;
            LineBreaks.Clear();

            // Index we put into the LineBreaks list when a line break should occur.
            var breakIndexCounter = 0;
            // If the CURRENT processing word ends up too long, this is the index to put a line break.
            int? wordStartBreakIndex = null;
            // Word size in pixels.
            var wordSizePixels = 0;
            // The horizontal position of the text cursor.
            var posX = 0;
            var lastChar = 'A';
            // If a word is larger than sizeX, we split it.
            // We need to keep track of some data to split it into two words.
            (int breakIndex, int wordSizePixels)? forceSplitData = null;
            // Go over every text tag.
            // We treat multiple text tags as one continuous one.
            // So changing color inside a single word doesn't create a word break boundary.
            foreach (var tag in Message.Tags)
            {
                // For now we can ignore every entry that isn't a text tag because those are only color related.
                // For now.
                if (!(tag is FormattedMessage.TagText tagText))
                {
                    continue;
                }

                var text = tagText.Text;
                // And go over every character.
                for (var i = 0; i < text.Length; i++, breakIndexCounter++)
                {
                    var chr = text[i];

                    if (IsWordBoundary(lastChar, chr) || chr == '\n')
                    {
                        // Word boundary means we know where the word ends.
                        if (posX > sizeX)
                        {
                            DebugTools.Assert(wordStartBreakIndex.HasValue,
                                "wordStartBreakIndex can only be null if the word begins at a new line, in which case this branch shouldn't be reached as the word would be split due to being longer than a single line.");
                            // We ran into a word boundary and the word is too big to fit the previous line.
                            // So we insert the line break BEFORE the last word.
                            LineBreaks.Add(wordStartBreakIndex.Value);
                            Height += font.LineHeight;
                            posX = wordSizePixels;
                        }

                        // Start a new word since we hit a word boundary.
                        //wordSize = 0;
                        wordSizePixels = 0;
                        wordStartBreakIndex = breakIndexCounter;
                        forceSplitData = null;

                        // Just manually handle newlines.
                        if (chr == '\n')
                        {
                            LineBreaks.Add(breakIndexCounter);
                            Height += font.LineHeight;
                            posX = 0;
                            lastChar = chr;
                            wordStartBreakIndex = null;
                            continue;
                        }
                    }

                    // Uh just skip unknown characters I guess.
                    if (!font.TryGetCharMetrics(chr, out var metrics))
                    {
                        lastChar = chr;
                        continue;
                    }

                    // Increase word size and such with the current character.
                    var oldWordSizePixels = wordSizePixels;
                    wordSizePixels += metrics.Advance;
                    // TODO: Theoretically, does it make sense to break after the glyph's width instead of its advance?
                    //   It might result in some more tight packing but I doubt it'd be noticeable.
                    //   Also definitely even more complex to implement.
                    posX += metrics.Advance;

                    if (posX > sizeX)
                    {
                        if (!forceSplitData.HasValue)
                        {
                            forceSplitData = (breakIndexCounter, oldWordSizePixels);
                        }

                        // Oh hey we get to break a word that doesn't fit on a single line.
                        if (wordSizePixels > sizeX)
                        {
                            var (breakIndex, splitWordSize) = forceSplitData.Value;
                            if (splitWordSize == 0) return;

                            // Reset forceSplitData so that we can split again if necessary.
                            forceSplitData = null;
                            LineBreaks.Add(breakIndex);
                            Height += font.LineHeight;
                            wordSizePixels -= splitWordSize;
                            wordStartBreakIndex = null;
                            posX = wordSizePixels;
                        }
                    }

                    lastChar = chr;
                }
            }

            // This needs to happen because word wrapping doesn't get checked for the last word.
            if (posX > sizeX)
            {
                DebugTools.Assert(wordStartBreakIndex.HasValue,
                    "wordStartBreakIndex can only be null if the word begins at a new line, in which case this branch shouldn't be reached as the word would be split due to being longer than a single line.");
                LineBreaks.Add(wordStartBreakIndex.Value);
                Height += font.LineHeight;
            }
        }

        public void Draw(
            DrawingHandleScreen handle,
            Font font,
            UIBox2 drawBox,
            float verticalOffset,
            // A stack for format tags.
            // This stack contains the format tag to RETURN TO when popped off.
            // So when a new color tag gets hit this stack gets the previous color pushed on.
            Stack<FormattedMessage.Tag> formatStack)
        {
            // The tag currently doing color.
            var currentColorTag = TagWhite;

            var globalBreakCounter = 0;
            var lineBreakIndex = 0;
            var baseLine = drawBox.TopLeft + new Vector2(0, font.Ascent + verticalOffset);
            formatStack.Clear();
            foreach (var tag in Message.Tags)
            {
                switch (tag)
                {
                    case FormattedMessage.TagColor tagColor:
                        formatStack.Push(currentColorTag);
                        currentColorTag = tagColor;
                        break;
                    case FormattedMessage.TagPop _:
                        var popped = formatStack.Pop();
                        switch (popped)
                        {
                            case FormattedMessage.TagColor tagColor:
                                currentColorTag = tagColor;
                                break;
                            default:
                                throw new InvalidOperationException();
                        }

                        break;
                    case FormattedMessage.TagText tagText:
                    {
                        var text = tagText.Text;
                        for (var i = 0; i < text.Length; i++, globalBreakCounter++)
                        {
                            var chr = text[i];
                            if (lineBreakIndex < LineBreaks.Count &&
                                LineBreaks[lineBreakIndex] == globalBreakCounter)
                            {
                                baseLine = new Vector2(drawBox.Left, baseLine.Y + font.LineHeight);
                                lineBreakIndex += 1;
                            }

                            var advance = font.DrawChar(handle, chr, baseLine, currentColorTag.Color);
                            baseLine += new Vector2(advance, 0);
                        }

                        break;
                    }
                }
            }
        }

        [Pure]
        private static bool IsWordBoundary(char a, char b)
        {
            return a == ' ' || b == ' ' || a == '-' || b == '-';
        }
    }
}
