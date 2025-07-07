using SCCMHound.src;
using SCCMHound.src.models;
using SharpHoundCommonLib.Enums;
using SharpHoundCommonLib.OutputTypes;
using SharpHoundRPC.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;


namespace SCCMHound.tests
{
    public class JSONWriterTests
    {

        [Fact]
        public void WriteMockComputersJSONTest()
        {
            List<ComputerExt> computers = new List<ComputerExt>();

            computers.Add (new ComputerExt
            {
                ObjectIdentifier = "S-1-5-21-1234567890-1234567890-123456789-515"
            });

            JSONWriter.writeJSONFileComputers(computers, ("computers-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ".json"));

            Assert.True(true);
        }

        [Fact]
        public void WriteSCCMComputersJSONTest()
        {
            SCCMConnector sccmConnector = SCCMConnector.CreateInstance(TestConstants.testInstanceIP, TestConstants.testInstanceSiteCode, null);
            SCCMCollector sccmCollector = new SCCMCollector(sccmConnector);
            List<ComputerExt> computers = sccmCollector.QueryComputers();

            JSONWriter.writeJSONFileComputers(computers, ("computers-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ".json"));

            Assert.True(true);
        }

        [Fact]
        public void WriteAllTest_serialized()
        {
            // Populates SCCM dataset lists
            List<ComputerExt> computers = SCCMCollectorTests.DeserializeComputers();
            List<User> users = SCCMCollectorTests.DeserializeUsers();
            List<Group> groups = SCCMCollectorTests.DeserializeGroups();
            List<UserMachineRelationship> relationships = SCCMCollectorTests.DeserializeRelationships();
            List<UserMachineRelationship> relationshipsCMPivot = AdminServiceCollectorTests.DeserializeRelationshipsCMPivot();
            List<LocalAdmin> localAdmins = AdminServiceCollectorTests.DeserializeLocalAdmins();
            List<Domain> domains;

            try
            {
                Console.WriteLine("resolving domains...");
                DomainsResolver domainsResolver = new DomainsResolver(users, computers, groups);
                domains = domainsResolver.domains;

            }
            catch (Exception e)
            {
                Console.WriteLine("resolving users to groups triggered exception");
                Console.WriteLine(e);
                throw e;
            }

            try
            {
                Console.WriteLine("resolving users to groups...");
                UsersGroupsResolver usersGroupsResolver = new UsersGroupsResolver(users, groups);

            }
            catch (Exception e)
            {
                Console.WriteLine("resolving users to groups triggered exception");
                Console.WriteLine(e);
                throw e;
            }

            try
            {
                Console.WriteLine("resolving sessions...");
                ComputerSessionsResolver compSessResolver = new ComputerSessionsResolver(computers, users, relationships, false, domains);

                compSessResolver.printSessions();
            }
            catch (Exception e)
            {
                Console.WriteLine("resolving sessions or writing data triggered exception");
                Console.WriteLine(e);
                throw e;
            }



            Console.WriteLine("Collecting local administrators...");
            try
            {
                LocalAdminsResolver localAdminsResolver = new LocalAdminsResolver(computers, groups, users, localAdmins, domains);

            }
            catch (Exception e)
            {
                Console.WriteLine("collecting local administrators triggered an exception");
                Console.WriteLine(e);
                throw e;
            }



            Console.WriteLine("Collecting pivot sessions...");
            try
            {
                    
                ComputerSessionsResolver compSessResolver = new ComputerSessionsResolver(computers, users, relationshipsCMPivot, false, domains);

            }
            catch (Exception e)
            {
                Console.WriteLine("collecting pivot sessions triggered an exception");
                Console.WriteLine(e);
                throw e;
            }


            try
            {
                Console.WriteLine("writing json...");
                JSONWriter.writeJSONFileComputers(computers, ("computers-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".json"));
                JSONWriter.writeJSONFileGroups(groups, ("groups-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".json"));
                JSONWriter.writeJSONFileUsers(users, ("users-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".json"));
                JSONWriter.writeJSONFileDomains(domains, ("domains-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".json"));

            }
            catch (Exception e)
            {
                Console.WriteLine("resolving sessions or writing data triggered exception");
                Console.WriteLine(e);
                throw e;
            }

            Assert.True(true);
        }

        [Fact]
        public void WriteSCCMUsersTest()
        {
            SCCMConnector sccmConnector = SCCMConnector.CreateInstance(TestConstants.testInstanceIP, TestConstants.testInstanceSiteCode, null);
            SCCMCollector sccmCollector = new SCCMCollector(sccmConnector);
            List<User> users = sccmCollector.QueryUsers();

            JSONWriter.writeJSONFileUsers(users, ("users-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ".json"));

            Assert.True(true);
        }

        [Fact]
        public void WriteSCCMUsersTest_serialized()
        {
            List<User> users = SCCMCollectorTests.DeserializeUsers();

            JSONWriter.writeJSONFileUsers(users, ("users-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ".json"));

            Assert.True(true);
        }

        [Fact]
        public void WriteSCCMGroupsTest_serialized()
        {
            List<Group> groups = SCCMCollectorTests.DeserializeGroups();

            JSONWriter.writeJSONFileGroups(groups, ("groups-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ".json"));

            Assert.True(true);
        }
    }
}
