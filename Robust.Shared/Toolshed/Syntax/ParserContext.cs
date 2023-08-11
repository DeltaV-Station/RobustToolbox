﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using JetBrains.Annotations;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Utility;

namespace Robust.Shared.Toolshed.Syntax;

public sealed partial class ParserContext
{
    [Dependency] public readonly ToolshedManager Toolshed = default!;

    public readonly string Input;
    public int MaxIndex { get; private set; }

    public int Index { get; private set; } = 0;

    public ParserContext(string input, ToolshedManager toolshed)
    {
        Toolshed = toolshed;
        Input = input;
        MaxIndex = input.Length - 1;
    }

    private ParserContext(ParserContext parserContext, int sliceSize, int? index)
    {
        IoCManager.InjectDependencies(this);
        DebugTools.Assert(sliceSize > 0);
        Input = parserContext.Input;
        Index = index ?? parserContext.Index;
        MaxIndex = Math.Min(parserContext.MaxIndex, Index + sliceSize - 1);
    }

    public bool SpanInRange(int length)
    {
        return MaxIndex >= (Index + length - 1);
    }

    public bool EatMatch(char c) => EatMatch(new Rune(c));

    public bool EatMatch(Rune c)
    {
        if (PeekRune() == c)
        {
            Index++;
            return true;
        }

        return false;
    }

    public bool EatMatch(string c)
    {
        if (PeekWord() == c)
        {
            GetWord();
            return true;
        }

        return false;
    }

    /// <remarks>
    ///     This should only be used for comparisons! It'll return '\0' (NOT null) for large runes.
    /// </remarks>
    public char? PeekChar()
    {
        if (PeekRune() is not { } rune)
            return null;

        if (rune.Utf16SequenceLength > 1)
            return '\x01';
        Span<char> buffer = stackalloc char[2];
        rune.EncodeToUtf16(buffer);

        return buffer[0];
    }

    public Rune? PeekRune()
    {
        if (!SpanInRange(1))
            return null;

        return Rune.GetRuneAt(Input, Index);
    }

    public Rune? GetRune()
    {
        if (PeekRune() is { } c)
        {
            Index += c.Utf16SequenceLength;
            return c;
        }

        return null;
    }

    /// <remarks>
    ///     This should only be used for comparisons! It'll return '\0' (NOT null) for large runes.
    /// </remarks>
    public char? GetChar()
    {
        if (PeekRune() is { } c)
        {
            Index += c.Utf16SequenceLength;

            if (c.Utf16SequenceLength > 1)
                return '\x01';

            Span<char> buffer = stackalloc char[2];
            c.EncodeToUtf16(buffer);

            return buffer[0];
        }

        return null;
    }

    [PublicAPI]
    public void DebugPrint()
    {
        Logger.DebugS("parser", Input);
        MakeDebugPointer(Index);
        MakeDebugPointer(MaxIndex, '|');
    }

    private void MakeDebugPointer(int pointAt, char pointer = '^')
    {
        var builder = new StringBuilder();
        builder.Append(' ', pointAt);
        builder.Append(pointer);
        Logger.DebugS("parser", builder.ToString());
    }

    private string? MaybeGetWord(bool advanceIndex, Func<Rune, bool>? test)
    {
        var startingIndex = Index;
        test ??= static c => !Rune.IsWhiteSpace(c);

        var builder = new StringBuilder();

        ConsumeWhitespace();

        // Walk forward until we run into whitespace
        while (PeekRune() is { } c && test(c))
        {
            builder.Append(GetRune());
        }

        if (startingIndex == Index)
            return null;

        if (!advanceIndex)
            Index = startingIndex;

        return builder.ToString();
    }

    public string? PeekWord(Func<Rune, bool>? test = null) => MaybeGetWord(false, test);

    public string? GetWord(Func<Rune, bool>? test = null) => MaybeGetWord(true, test);

    public ParserRestorePoint Save()
    {
        return new ParserRestorePoint(Index, new(_terminatorStack));
    }

    public void Restore(ParserRestorePoint point)
    {
        Index = point.Index;
        _terminatorStack = point.TerminatorStack;
    }

    public int ConsumeWhitespace()
    {
        if (NoMultilineExprs)
            return Consume(static x => Rune.IsWhiteSpace(x) && x != new Rune('\n'));
        return Consume(Rune.IsWhiteSpace);
    }

    private Stack<string> _terminatorStack = new();

    public void PushTerminator(string term)
    {
        _terminatorStack.Push(term);
    }

    public bool PeekTerminated()
    {
        if (_terminatorStack.Count == 0)
            return false;

        ConsumeWhitespace();
        return PeekWord(Rune.IsSymbol) == _terminatorStack.Peek();
    }

    public bool EatTerminator()
    {
        if (PeekTerminated())
        {
            GetWord(Rune.IsSymbol);
            _terminatorStack.Pop();
            return true;
        }

        return false;
    }

    public bool CheckEndLine()
    {
        if (NoMultilineExprs)
            return EatMatch('\n');
        return false;
    }

    public int Consume(Func<Rune, bool> control)
    {
        var amount = 0;

        while (PeekRune() is { } c && control(c))
        {
            GetRune();
            amount++;
        }

        return amount;
    }

    public ParserContext? SliceBlock(Rune startDelim, Rune endDelim)
    {
        var checkpoint = Save();

        ConsumeWhitespace();

        if (GetRune() != startDelim)
        {
            Restore(checkpoint);
            return null;
        }

        var blockStart = Index;

        var stack = 1;

        while (stack > 0)
        {
            var c = GetRune();
            if (c == startDelim)
                stack++;

            if (c == endDelim)
            {
                if (--stack == 0)
                    break;
            }

            if (c == null)
            {
                Restore(checkpoint);
                return null;
            }
        }

        return new ParserContext(this, Index - blockStart, blockStart);
    }
}

public readonly struct ParserRestorePoint
{
    public readonly int Index;
    internal readonly Stack<string> TerminatorStack;

    public ParserRestorePoint(int index, Stack<string> terminatorStack)
    {
        Index = index;
        TerminatorStack = terminatorStack;
    }
}

public record struct OutOfInputError : IConError
{
    public FormattedMessage DescribeInner()
    {
        return FormattedMessage.FromMarkup("Ran out of input data when data was expected.");
    }

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}
