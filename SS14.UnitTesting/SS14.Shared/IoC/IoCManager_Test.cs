﻿using NUnit.Framework;
using SS14.Client.Interfaces.Resource;
using SS14.Server.Interfaces.Configuration;
using SS14.Shared.IoC;

namespace SS14.UnitTesting.SS14.Shared.IoC
{
    [TestFixture]
    public class IoCManager_Test : SS14UnitTest
    {
        [Test]
        public void ResolveIConfigurationManager_ShouldReturnConfigurationManager()
        {
           var temp = IoCManager.Resolve<IServerConfigurationManager>();

           Assert.IsNotNull(temp, "IoC failed to return an IServerConfigurationManager.");
        }

        [Test]
        public void ResolveIResourceManager_ShouldReturnResourceManager()
        {
            var temp = IoCManager.Resolve<IResourceManager>();

            Assert.IsNotNull(temp, "IoC failed to return an IResourceManager.");
        }


    }
}
