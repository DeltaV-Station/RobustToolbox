﻿using System.Threading.Tasks;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Toolshed.Errors;

namespace Robust.UnitTesting.Shared.Toolshed;

[TestFixture]
public sealed class ErrorHandlingTest : ToolshedTest
{
    [Test]
    public async Task ExceptionsAreErrors()
    {
        ExpectError<UnhandledExceptionError>();
        InvokeCommand("fuck", out _);
    }

    [Test]
    public async Task NoDivideByZeroError()
    {
        // This shouldn't throw, because toolshed is nice :)
        await Server.WaitAssertion(() => { InvokeCommand<float>("f 1 / 0"); });
        await Server.WaitAssertion(() => { InvokeCommand<int>("i 1 / 0"); });
    }

    [Test]
    public async Task SelfNotForServerConsole()
    {
        ExpectError<NotForServerConsoleError>();
        InvokeCommand("self", out _);
    }
}
