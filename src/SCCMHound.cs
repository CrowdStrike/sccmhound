using SCCMHound.src.models;
using SCCMHound.src;
using SCCMHound.tests;
using SharpHoundCommonLib.OutputTypes;
using System;
using System.Collections.Generic;
using CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using System.Management;
using System.DirectoryServices.ActiveDirectory;
using Domain = SharpHoundCommonLib.OutputTypes.Domain;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SCCMHound
{

    class Options
    {
        [Option("server", Required = true, HelpText = "SCCM server hostname/IP address.")]
        public string Server { get; set; }

        [Option("sitecode", Required = true, HelpText = "SCCM sitecode.")]
        public string Sitecode { get; set; }

        [Option('c', "collectionmethods", Required = false, Default = "Default", HelpText = "(LocalAdmins, CurrentSessions, All)")]
        public string Collectionmethods { get; set; }

        [Option("loop", Required = false, Default = false, HelpText = "Enable loop collection.")]
        public Boolean Loop { get; set; }

        [Option("loopduration", Required = false, Default = "00:30:00", HelpText = "Loop Duration.")]
        public String LoopDuration { get; set; }

        [Option("loopsleep", Required = false, Default = 60, HelpText = "Sleep time between loops in seconds.")]
        public int LoopSleep { get; set; }

        [Option("hc", Required = false, Default = false, HelpText = "Health check. Simply tests authentication and exits.")]
        public Boolean HealthCheck { get; set;}

        [Option('u', "username", Required = false, HelpText = "SCCM administrator username.")]
        public string username { get; set; }

        [Option('p', "password", Required = false, HelpText = "SCCM administrator password.")]
        public string password { get; set; }

        [Option('d', "domain", Required = false, HelpText = "SCCM administrator domain.")]
        public string domain { get; set; }

        [Option('v', "verbose", Required = false, Default = false, HelpText = "Print verbose information to help with troubleshooting.")]
        public Boolean verbose { get; set; }

    }

    public class SCCMHound
    {
        static void RunOptions(Options opts)
        {
            SCCMConnectorOptions sccmConnectorOptions = null;

            // check at least one credential arg was set
            if (opts.username is not null || opts.password is not null || opts.domain is not null)
            {
                // check if all args are not null
                if (opts.username is not null && opts.password is not null && opts.domain is not null) {
                    sccmConnectorOptions = new SCCMConnectorOptions(opts.username, opts.password, opts.domain);
                }
                else
                {
                    Console.WriteLine("Please specify a username, password, and domain when specifying a credential.");
                    Environment.Exit(-1);
                }

            }

            invoke(opts.Server, opts.Sitecode, opts.Loop, TimeSpan.Parse(opts.LoopDuration), opts.LoopSleep, opts.Collectionmethods, opts.HealthCheck, opts.verbose, sccmConnectorOptions);
        }
        static void HandleParseError(IEnumerable<Error> errs)
        {
        }

        public static void invoke(string sccmServer, string sccmSiteCode, Boolean loop, TimeSpan loopDuration, int sleepSeconds, string collectionMethods, Boolean healthCheck, Boolean verbose, SCCMConnectorOptions sccmConnectorOptions)
        {
            Console.WriteLine($"Connecting to {sccmServer} (sitecode: {sccmSiteCode})");
            SCCMConnector sccmConnector = null;
            AdminServiceConnector adminServiceConnector = null;

            try
            {
                Console.WriteLine("Establishing connection...");
                sccmConnector = SCCMConnector.CreateInstance(sccmServer, sccmSiteCode, sccmConnectorOptions);
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine($"The user was not authorized to access {sccmServer} (sitecode: {sccmSiteCode}). Please check that the supplied credentials are correct.");
                if (verbose) { Console.WriteLine(e.ToString()); }
                return;
            }
            catch (ManagementException e)
            {
                if (e.HResult == Convert.ToInt32("0x80131501", 16))
                {
                    Console.WriteLine($"The supplied sitecode ({sccmSiteCode}) was invalid. Please specify a valid sitecode.");
                }
                else if (!verbose)
                {
                    Console.WriteLine($"An unhandled condition occured when establishing a connection to target {sccmServer}. Debugging is required (-v)");
                }
                else
                {
                    Console.WriteLine($"An unhandled condition occured when establishing a connection to {sccmServer}");
                }

                if (verbose) { Console.WriteLine(e.ToString()); }
                return;
            }
            catch (COMException e)
            {
                if (e.HResult == Convert.ToInt32("800706BA", 16))
                {
                    Console.WriteLine($"Could not establish a WMI connection to target {sccmServer}. The RPC server is unavailable.");
                }
                else if (!verbose)
                {
                    Console.WriteLine($"An unhandled condition occured when establishing a connection to target {sccmServer}. Debugging is required (-v)");
                }
                else
                {
                    Console.WriteLine($"An unhandled condition occured when establishing a connection to {sccmServer}");
                }

                if (verbose) { Console.WriteLine(e.ToString()); }
                return;
            }
            catch (Exception e)
            {
                if (verbose)
                {
                    Console.WriteLine("An unhandled condition occured when establishing a connection to the target.");
                    Console.WriteLine(e.ToString());
                }
                else
                {
                    Console.WriteLine("An unhandled condition occured when establishing a connection to the target. Debugging is required (-v)");
                }
                return;
            }

            // If CMPivot is used
            if (collectionMethods.ToLower().Equals("localadmins") || collectionMethods.ToLower().Equals("currentsessions") || collectionMethods.ToLower().Equals("all"))
            {
                try
                {
                    adminServiceConnector = AdminServiceConnector.CreateInstance(sccmServer, sccmConnectorOptions);
                    AdminServiceCollector collector = new AdminServiceCollector(adminServiceConnector);
                    if (!collector.GetCollections()) {
                        throw new Exception();
                    }
                }
                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine("A 403 Unauthorized response was returned from the AdminService API. Are you sure your user is an SCCM Full Administrator?\nNote: DA != SCCM Full Administrator!");
                    if (verbose) { Console.WriteLine(e.ToString()); }
                    return;
                }
                catch (Exception e)
                {
                    if (verbose)
                    {
                        Console.WriteLine("An unhandled condition occured when establishing a connection to the target via the Admin Service API.");
                        Console.WriteLine(e.ToString());
                    }
                    else
                    {
                        Console.WriteLine("An unhandled condition occured when establishing a connection to the target via the Admin Service API. Debugging is required (-v)");
                    }
                    return;
                }

            }

            if (healthCheck)
            {
                if (sccmConnector.scope.IsConnected)
                {
                    Console.WriteLine($"Connection to {sccmServer} (sitecode: {sccmSiteCode}) established!");
                    Console.WriteLine("Health check passed! Connected.");
                    return;

                }
                else
                {
                    Console.WriteLine("Health check failed. Connector is not connected.");
                    return;
                }
            }

            if (sccmConnector.scope.IsConnected)
            {
                Console.WriteLine($"Connection to {sccmServer} (sitecode: {sccmSiteCode}) established!");
                SCCMCollector sccmCollector = new SCCMCollector(sccmConnector);

                List<ComputerExt> computers;
                List<User> users;
                List<Group> groups;
                List<UserMachineRelationship> relationships;
                List<Domain> domains;

                try
                {
                    Console.WriteLine($"Collecting computer objects from the {sccmSiteCode} site...");
                    computers = sccmCollector.QueryComputers();
                    Console.WriteLine($"Collected {computers.Count} computer objects.");

                    Console.WriteLine($"Collecting user objects from the {sccmSiteCode} site...");
                    users = sccmCollector.QueryUsers();
                    Console.WriteLine($"Collected {users.Count} user objects.");


                    Console.WriteLine($"Collecting group objects from the {sccmSiteCode} site...");
                    groups = sccmCollector.QueryGroups();
                    Console.WriteLine($"Collected {groups.Count} group objects.");


                    Console.WriteLine($"Collecting user machine relationship objects from the {sccmSiteCode} site...");
                    relationships = sccmCollector.QueryUserMachineRelationships();
                    Console.WriteLine($"Collected {relationships.Count} relationship objects.");

                    if (computers.Count == 0 && users.Count == 0 && groups.Count == 0 && relationships.Count == 0)
                    {
                        Console.WriteLine("No objects were returned from the invoked queries. Are you sure your user is an SCCM Full Administrator?\nNote: DA != SCCM Full Administrator!");
                        return;
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine("Data collection triggered  an exception.");
                    if (verbose) {  Console.WriteLine(e.ToString()); }
                    return;
                }

                try
                {
                    Console.WriteLine("Resolving domains from collected objects...");
                    DomainsResolver domainsResolver = new DomainsResolver(users, computers, groups);
                    domains = domainsResolver.domains;

                }
                catch (Exception e)
                {
                    Console.WriteLine("Resolving domains triggered an exception.");
                    if (verbose) { Console.WriteLine(e.ToString()); }
                    return;
                }

                try
                {
                    Console.WriteLine("Resolving users from collected objects...");
                    UsersGroupsResolver usersGroupsResolver = new UsersGroupsResolver(users, groups);

                }
                catch (Exception e)
                {
                    Console.WriteLine("Resolving users to groups triggered an exception.");
                    if (verbose) { Console.WriteLine(e.ToString()); }
                    return;
                }

                try
                {
                    Console.WriteLine("Resolving sessions from collected objects...");
                    ComputerSessionsResolver compSessResolver = new ComputerSessionsResolver(computers, users, relationships, false, domains);
                    compSessResolver.printSessions();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Resolving sessions triggered an exception.");
                    if (verbose) { Console.WriteLine(e.ToString()); }
                    return;
                }


                if (collectionMethods.ToLower().Equals("localadmins") || collectionMethods.ToLower().Equals("all"))
                {
                    Console.WriteLine($"Collecting local administrator information from computers in the {sccmSiteCode} site...");
                    try
                    {
                        //AdminServiceConnector connector = AdminServiceConnector.CreateInstance(sccmServer, sccmConnectorOptions);
                        AdminServiceCollector collector = new AdminServiceCollector(adminServiceConnector);
                        List<LocalAdmin> localAdmins = collector.GetAdministrators();
                        LocalAdminsResolver localAdminsResolver = new LocalAdminsResolver(computers, groups, users, localAdmins, domains);
                        
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        Console.WriteLine(e.ToString());
                        return;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Collecting local administrators triggered an exception");
                        if (verbose) { Console.WriteLine(e.ToString()); }
                        return;
                    }
                }

                if (collectionMethods.ToLower().Equals("currentsessions") || collectionMethods.ToLower().Equals("all"))
                {
                    Console.WriteLine($"Collecting current session information from computers in the {sccmSiteCode} site...");
                    try
                    {
                        //AdminServiceConnector connector = AdminServiceConnector.CreateInstance(sccmServer, sccmConnectorOptions);
                        AdminServiceCollector collector = new AdminServiceCollector(adminServiceConnector);
                        List<UserMachineRelationship> relationshipsCMPivot = collector.GetUsers();
                        ComputerSessionsResolver compSessResolver = new ComputerSessionsResolver(computers, users, relationshipsCMPivot, false, domains);

                    }
                    catch (UnauthorizedAccessException e)
                    {
                        Console.WriteLine(e.ToString());
                        return;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Collecting current session information triggered an exception");
                        if (verbose) { Console.WriteLine(e.ToString()); }
                        return;
                    }
                }

                try
                {
                    Console.WriteLine("Writing JSON output...");
                    JSONWriter.writeJSONFileComputers(computers, ("computers-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".json"));
                    JSONWriter.writeJSONFileGroups(groups, ("groups-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".json"));
                    JSONWriter.writeJSONFileUsers(users, ("users-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".json"));
                    JSONWriter.writeJSONFileDomains(domains, ("domains-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".json"));

                }
                catch (Exception e)
                {
                    Console.WriteLine("Writing JSON output triggered an exception. Check that you can write to the folder where executed from?");
                    if (verbose) { Console.WriteLine(e.ToString()); }
                    return;
                }                

                DateTime endTime = DateTime.Now.Add(loopDuration);
                int loopCount = 1;

                if(loop) { Console.WriteLine("Getting ready to start collection loops..."); }
                while (loop) {
                    try
                    {
                        // sleep for 1 minute
                        Console.WriteLine($"Sleeping for {sleepSeconds} seconds...");
                        System.Threading.Thread.Sleep(sleepSeconds * 1000);

                        Console.WriteLine($"Starting session loop iteration: {loopCount}...");
                        Console.WriteLine($"Collecting user machine relationship objects from the {sccmSiteCode} site...");
                        relationships = sccmCollector.QueryUserMachineRelationships();

                        Console.WriteLine("Resolving sessions from collected objects...");
                        ComputerSessionsResolver compSessResolver = new ComputerSessionsResolver(computers, users, relationships, false, domains);

                        compSessResolver.printSessions();

                        if (collectionMethods.ToLower().Equals("currentsessions") || collectionMethods.Equals("all"))
                        {
                    Console.WriteLine($"Collecting current session information from computers in the {sccmSiteCode} site...");
                            try
                            {
                                //AdminServiceConnector connector = AdminServiceConnector.CreateInstance(sccmServer, sccmConnectorOptions);
                                AdminServiceCollector collector = new AdminServiceCollector(adminServiceConnector);
                                List<UserMachineRelationship> relationshipsCMPivot = collector.GetUsers();
                                compSessResolver = new ComputerSessionsResolver(computers, users, relationshipsCMPivot, false, domains);

                            }
                            catch (UnauthorizedAccessException e)
                            {
                                Console.WriteLine(e.ToString());
                                return;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Collecting current session information triggered an exception");
                                if (verbose) { Console.WriteLine(e.ToString()); }
                            }
                        }

                        try
                        {
                            Console.WriteLine("Writing session loop JSON output...");
                            JSONWriter.writeJSONFileComputers(computers, ("sessions-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".json"));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Writing JSON output triggered an exception. Check that you can write to the folder where executed from?");
                            if (verbose) { Console.WriteLine(e.ToString()); }
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Loop collection triggered an exception");
                        if (verbose) { Console.WriteLine(e.ToString()); }
                        return;

                    }
                    finally
                    {
                        loopCount++;
                        if (DateTime.Now > endTime)
                        {
                            loop = false;
                        }
                    }
                }

            }
            else
            {
                Console.WriteLine("SCCM connector could not be established.");
                return;
            }

            // Clean up LDAP connections before exiting
            try
            {
                Console.WriteLine("Cleaning up any LDAP connections...");
                LdapConnectionManager.Instance.Cleanup();
            }
            catch (Exception e)
            {
                if (verbose)
                {
                    Console.WriteLine("An error occurred while cleaning up LDAP connections:");
                    Console.WriteLine(e.ToString());
                }
            }

            Console.WriteLine("Hound out!");
            return;
        }

        public static void Main(string[] args)
        {

            string banner = "\r\n   ▄████████  ▄████████  ▄████████   ▄▄▄▄███▄▄▄▄      ▄█    █▄     ▄██████▄  ███    █▄  ███▄▄▄▄   ████████▄  \r\n  ███    ███ ███    ███ ███    ███ ▄██▀▀▀███▀▀▀██▄   ███    ███   ███    ███ ███    ███ ███▀▀▀██▄ ███   ▀███ \r\n  ███    █▀  ███    █▀  ███    █▀  ███   ███   ███   ███    ███   ███    ███ ███    ███ ███   ███ ███    ███ \r\n  ███        ███        ███        ███   ███   ███  ▄███▄▄▄▄███▄▄ ███    ███ ███    ███ ███   ███ ███    ███ \r\n▀███████████ ███        ███        ███   ███   ███ ▀▀███▀▀▀▀███▀  ███    ███ ███    ███ ███   ███ ███    ███ \r\n         ███ ███    █▄  ███    █▄  ███   ███   ███   ███    ███   ███    ███ ███    ███ ███   ███ ███    ███ \r\n   ▄█    ███ ███    ███ ███    ███ ███   ███   ███   ███    ███   ███    ███ ███    ███ ███   ███ ███   ▄███ \r\n ▄████████▀  ████████▀  ████████▀   ▀█   ███   █▀    ███    █▀     ▀██████▀  ████████▀   ▀█   █▀  ████████▀  \r\n                                                                                                             \r\n";
            Console.WriteLine(banner);
            CommandLine.Parser.Default.ParseArguments<Options>(args)
            .WithParsed(RunOptions)
            .WithNotParsed(HandleParseError);
        }
    }
}
