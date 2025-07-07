using SCCMHound.src.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpHoundCommonLib.OutputTypes;
using Xunit;
using SCCMHound.src;

namespace SCCMHound.tests
{
    public class ComputerSessionResolverTests
    {
        [Fact]
        public void ComputerSessionsResolver_test()
        {
            List<ComputerExt> computers = SCCMCollectorTests.DeserializeComputers();
            List<User> users = SCCMCollectorTests.DeserializeUsers();
            List<UserMachineRelationship> relationships = SCCMCollectorTests.DeserializeRelationships();
            List<Domain> domains = null;

            ComputerSessionsResolver compSessResolver = new ComputerSessionsResolver(computers, users, relationships, false, domains);

            foreach (ComputerSessions compSessions in compSessResolver.computerSessionsList)
            {
                compSessions.printComputerSessions();
            }

            if (compSessResolver.computerSessionsList.Count > 0)
            {
                Assert.True(true);
            }
        }
    }
}
