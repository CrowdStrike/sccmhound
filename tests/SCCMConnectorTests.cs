﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SCCMHound.tests
{
    public class SCCMConnectorTests
    {
        [Fact]
        public void connect_success()
        {
            SCCMConnector sccmConnector = SCCMConnector.CreateInstance(TestConstants.testInstanceIP, TestConstants.testInstanceSiteCode, null);
            Assert.True(sccmConnector.scope.IsConnected);
        }

        [Fact]
        public void connect_failure()
        {
            var e = Assert.Throws<InvalidOperationException>(() => SCCMConnector.CreateInstance(TestConstants.testUnroutableIP, TestConstants.testInstanceSiteCode, null));
            Assert.Equal("Could not establish WMI connection",e.Message);
        }
    }
}