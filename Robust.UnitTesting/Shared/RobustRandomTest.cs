﻿using NUnit.Framework;
using Robust.Shared.Random;

namespace Robust.UnitTesting.Shared;

[TestFixture, TestOf(typeof(RandomExtensions))]
public sealed class RobustRandomTest
{
    private const int Seed = 85723475;

    [Test]
    public void TestDieRandom()
    {
        IRobustRandom compRandom = new RobustRandom(Seed);
        IRobustRandom testRandom = new RobustRandom(Seed);
        var actual = testRandom.RollDice(1, 4);
        var expected = compRandom.Next(1, 4);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    [Parallelizable]
    [TestCase(3,4)]
    [TestCase(1,6)]
    [TestCase(4,70)]
    [TestCase(5,120)]
    [TestCase(30,1000)]
    public void TestDiceRandom(int num, int faces)
    {
        IRobustRandom compRandom = new RobustRandom(Seed);
        IRobustRandom testRandom = new RobustRandom(Seed);
        var actual = testRandom.RollDice(num, faces);
        var sum = 0;
        for (uint i = 0; i < num; i++)
        {
            sum += compRandom.Next(1, faces);
        }
        Assert.That(actual, Is.EqualTo(sum));
    }
}
