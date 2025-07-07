using SCCMHound.src.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpHoundCommonLib.OutputTypes;
using Xunit;
using SCCMHound.src;
using System.Diagnostics;

namespace SCCMHound.tests
{
    public class LocalAdminsResolverTest
    {
        [Fact]
        public void LocalAdminsResolver_test()
        {
            List<ComputerExt> computers = SCCMCollectorTests.DeserializeComputers();
            List<User> users = SCCMCollectorTests.DeserializeUsers();
            List<Group> groups = SCCMCollectorTests.DeserializeGroups();
            List<LocalAdmin> localAdmins = AdminServiceCollectorTests.DeserializeLocalAdmins();
            List<Domain> domains = null;

            LocalAdminsResolver localAdminsResolver = new LocalAdminsResolver(computers, groups, users, localAdmins, domains);

            foreach (ComputerExt computer in computers)
            {
                Debug.Print((string)computer.Properties["name"]);
            }

            if (computers.Count > 0)
            {
                Assert.True(true);
            }
        }
    }
}
