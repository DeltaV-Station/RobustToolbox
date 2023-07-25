﻿using System.Diagnostics;
using System.Text;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Robust.Shared.RTShell.Errors;

public interface IConError
{
    public FormattedMessage Describe()
    {
        var msg = new FormattedMessage();
        if (Expression is { } expr && IssueSpan is { } span)
        {
            msg.AddMessage(ConHelpers.HighlightSpan(expr, span, Color.Red));
            msg.PushNewline();
            msg.AddMessage(ConHelpers.ArrowSpan(span));
            msg.PushNewline();
        }
        msg.AddMessage(DescribeInner());
#if TOOLS
        if (Trace is not null)
        {
            msg.PushNewline();
            msg.AddText(Trace.ToString());
        }
#endif
        return msg;
    }

    protected FormattedMessage DescribeInner();
    public string? Expression { get; protected set; }
    public Vector2i? IssueSpan { get; protected set; }
    public StackTrace? Trace { get; protected set; }

    public IConError Contextualize(string expression, Vector2i issueSpan)
    {
        if (Expression is not null && IssueSpan is not null)
            return this;

#if  TOOLS
        Trace = new StackTrace(skipFrames: 1);
#endif

        Expression = expression;
        IssueSpan = issueSpan;
        return this;
    }
}

public static class ConHelpers
{
    public static FormattedMessage HighlightSpan(string input, Vector2i span, Color color)
    {
        if (span.X == span.Y)
            return new FormattedMessage();
        var msg = FormattedMessage.FromMarkup(input[..span.X]);
        msg.PushColor(color);
        msg.AddText(input[span.X..span.Y]);
        msg.Pop();
        msg.AddText(input[span.Y..]);
        return msg;
    }

    public static FormattedMessage ArrowSpan(Vector2i span)
    {
        var builder = new StringBuilder();
        builder.Append(' ', span.X);
        builder.Append('^', span.Y - span.X);
        return FormattedMessage.FromMarkup(builder.ToString());
    }
}
