﻿using NUnit.Framework;
using Robust.Shared.Interfaces.Log;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Robust.UnitTesting.Shared.Utility
{
    [TestFixture]
    [TestOf(typeof(NullableHelper))]
    public class NullableHelper_Test
    {
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            //initializing logmanager so it wont error out if nullablehelper logs an error
            IoCManager.InitThread();
            IoCManager.Register<ILogManager, LogManager>();
            IoCManager.BuildGraph();
        }

        [Test]
        public void IsNullableTest()
        {
            var fields = typeof(NullableTestClass).GetAllFields();
            foreach (var field in fields)
            {
                Assert.That(NullableHelper.IsMarkedAsNullable(field), Is.True, $"{field}");
            }
        }

        [Test]
        public void IsNotNullableTest()
        {
            var fields = typeof(NotNullableTestClass).GetAllFields();
            foreach (var field in fields)
            {
                Assert.That(!NullableHelper.IsMarkedAsNullable(field), Is.True, $"{field}");
            }
        }
    }

    public class NullableTestClass
    {
        private int? i;
        private double? d;
        public object? o;
        public INullableTestInterface? Itest;
        public NullableTestClass? nTc;
        private char? c;
    }

    public class NotNullableTestClass
    {
        private int i;
        private double d;
        private object o = null!;
        private INullableTestInterface Itest = null!;
        private NullableTestClass nTc = null!;
        private char c;
    }

    public interface INullableTestInterface{}
}
