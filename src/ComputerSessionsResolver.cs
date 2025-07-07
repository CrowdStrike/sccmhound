using SCCMHound.src.models;
using SharpHoundCommonLib.OutputTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SCCMHound.src
{
    public class ComputerSessionsResolver
    {
        public List<ComputerSessions> computerSessionsList = new List<ComputerSessions>();
        Dictionary<string, ComputerExt> computerLookupByResourceName;
        Dictionary<string, User> userLookupByUniqueUserName;

        public void printSessions()
        {
            Debug.Print($"{computerSessionsList.Count} sessions were enumerated.");
        }

        public ComputerSessionsResolver(List<ComputerExt> computers, List<User> users, List<UserMachineRelationship> relationships, Boolean addUnresolvedSessionsToSessions, List<SharpHoundCommonLib.OutputTypes.Domain> domains)
        {
            this.computerLookupByResourceName = HelperUtilities.createLookupTableComputers(computers);
            this.userLookupByUniqueUserName = HelperUtilities.createLookupTableUsers(users);

            Dictionary<string, ComputerSessions> computerSessionsLookupByComputer = new Dictionary<string, ComputerSessions>();

            // for each user machine relationship, resolve the relationship, storing the result in the corresponding ComputerSessions object
            foreach (UserMachineRelationship relationship in relationships)
            {
                try
                {
                    ComputerSessions computerSessions;

                    // Resolve computer from computerLookupByResourceName dict
                    ComputerExt computer = computerLookupByResourceName[relationship.resourceName];

                    if (!computerSessionsLookupByComputer.ContainsKey(computer.Properties["name"].ToString()))
                    {

                        computerSessions = new ComputerSessions(computer);
                        computerSessionsLookupByComputer[computer.Properties["name"].ToString()] = computerSessions;
                        this.computerSessionsList.Add(computerSessions);


                    }
                    // retrieve object from dictionary
                    else
                    {
                        computerSessions = computerSessionsLookupByComputer[computer.Properties["name"].ToString()];
                    }


                    if (userLookupByUniqueUserName.ContainsKey(relationship.uniqueUserName.ToLower())) {
                        // resolve user from userLookupByUniqueUserName dict
                        User recordUser = userLookupByUniqueUserName[relationship.uniqueUserName.ToLower()];
                        computerSessions.AddSessionUser(recordUser);
                    }
                    // if the user is not in the lookup table - make dummy user and add
                    else
                    {
                        string recordUsername = relationship.uniqueUserName.ToLower();
                        string name = "";
                        string domain = "";
                        string[] tokens = recordUsername.Split('\\');
                        if (tokens.Length == 2)
                        {
                            name = tokens[1];
                            domain = tokens[0];
                        }
                        else
                        {
                            name = recordUsername;
                        }

                        domain = HelperUtilities.lookupNetbiosReturnFQDN(domain, domains);
                        ;
                        User user = UserFactory.CreateUser($"{name}@{domain}".ToUpper(), domain, domains);
                        users.Add(user);
                        userLookupByUniqueUserName.Add(relationship.uniqueUserName.ToLower(), user);
                        computerSessions.AddSessionUser(user);
                    }
                    //else if (addUnresolvedSessionsToSessions)
                    {
                        // I dont like this. TODO: Remove
                        /*
                        // create a new user
                        
                        User recordUser = new User();
                        recordUser.Properties["name"] = relationship.uniqueUserName;
                        recordUser.ObjectIdentifier = String.Format("sccm-{0}", relationship.uniqueUserName);
                        recordUser.Properties["sccmUniqueUserName"] = relationship.uniqueUserName;
                        userLookupByUniqueUserName.Add(recordUser.Properties["sccmUniqueUserName"].ToString().ToLower(), recordUser);
                        computerSessions.AddSessionUser(recordUser);
                        users.Add(recordUser);
                        */
                        
                    }
                    // else, add user to sccmUnresolvableSessions
                    /*
                    else
                    {


                        if (!computer.Properties.ContainsKey("sccmUnresolvableSessions"))
                        {
                            computer.Properties["sccmUnresolvableSessions"] = new List<string>(); // container for user sessions that dont have corresponding SCCM users
                        }
                        List<string> externalUserSessions = (List<string>)computer.Properties["sccmUnresolvableSessions"];
                        externalUserSessions.Add(relationship.uniqueUserName.ToString());

                        
                    }
                    */
                    

                    
                    
                    
                    
                }
                // TODO: replace with specific exception handlers
                catch (Exception ex)
                {
                    Debug.Print(ex.ToString());
                }

            }

            foreach (ComputerSessions computerSessions in this.computerSessionsList)
            {
                computerSessions.PopulateComputerExtWithSessionData();
            }
        }
    }
}
