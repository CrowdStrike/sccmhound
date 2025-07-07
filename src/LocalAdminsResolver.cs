using SCCMHound.src.models;
using SharpHoundCommonLib.OutputTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SCCMHound.src
{
    class LocalAdminsResolver
    {
        public LocalAdminsResolver(List<ComputerExt> computers, List<Group> groups, List<User> users, List<LocalAdmin> localAdmins, List<Domain> domains)
        {
            Dictionary<string, ComputerExt> computerLookupByResourceName = HelperUtilities.createLookupTableComputers(computers);
            Dictionary<string, User> userLookupByUniqueUserName = HelperUtilities.createLookupTableUsers(users);
            Dictionary<string, Group> groupLookupByGroupName = HelperUtilities.createLookupTableGroups(groups);
            // group resolver

            Dictionary<string, ComputerLocalAdmins> computerLocalAdminsLookupByComputer = new Dictionary<string, ComputerLocalAdmins>();

            var computerLocalAdminsList = new List<ComputerLocalAdmins>();

            foreach (LocalAdmin localAdmin in localAdmins)
            {
                try
                {
                    if (computerLookupByResourceName.ContainsKey(localAdmin.deviceName))
                    {
                        ComputerExt computer = computerLookupByResourceName[localAdmin.deviceName];
                        ComputerLocalAdmins computerLocalAdmins;

                        if (!computerLocalAdminsLookupByComputer.ContainsKey(computer.Properties["name"].ToString()))
                        {

                            computerLocalAdmins = new ComputerLocalAdmins(computer);
                            computerLocalAdminsLookupByComputer[computer.Properties["name"].ToString()] = computerLocalAdmins;
                            computerLocalAdminsList.Add(computerLocalAdmins);


                        }
                        // retrieve object from dictionary
                        else
                        {
                            computerLocalAdmins = computerLocalAdminsLookupByComputer[computer.Properties["name"].ToString()];
                        }


                        if (localAdmin.type.Equals("User"))
                        {
                            if (userLookupByUniqueUserName.ContainsKey(localAdmin.name.ToLower()))
                            {
                                User recordUser = userLookupByUniqueUserName[localAdmin.name.ToLower()];
                                computerLocalAdmins.AddAdminUser(recordUser);
                            }
                            else
                            {
                                string name = "";
                                string domain = "";
                                string[] tokens = localAdmin.name.Split('\\');
                                if (tokens.Length == 2)
                                {
                                    name = tokens[1];
                                    domain = tokens[0];
                                }
                                else
                                {
                                    name = localAdmin.name;
                                }

                                domain = HelperUtilities.lookupNetbiosReturnFQDN(domain, domains);
;
                                User user = UserFactory.CreateUser($"{name}@{domain}".ToUpper(), domain, domains);
                                users.Add(user);
                                userLookupByUniqueUserName.Add(localAdmin.name.ToLower(), user);
                                computerLocalAdmins.AddAdminUser(user);
                            }
                        }
                        else if (localAdmin.type.Equals("Group"))
                        {
                            string name = "";
                            string domain = "";
                            string[] tokens = localAdmin.name.Split('\\');
                            if (tokens.Length == 2)
                            {
                                name = tokens[1];
                                domain = tokens[0];
                            }
                            else
                            {
                                name = localAdmin.name;
                            }

                            domain = HelperUtilities.lookupNetbiosReturnFQDN(domain, domains);

                            string localAdminName = $"{localAdmin.name.Split('\\')[1].ToUpper()}@{computer.Properties["domain"]}".ToLower();
                            if (groupLookupByGroupName.ContainsKey($"{name}@{domain}".ToLower()))
                            {
                                Group recordGroup = groupLookupByGroupName[$"{name}@{domain}".ToLower()];
                                computerLocalAdmins.AddAdminGroup(recordGroup);
                            }
                            else
                            {
                                Group group = GroupFactory.CreateGroup($"{name}@{domain}".ToUpper(), domain, domains);
                                groups.Add(group);
                                groupLookupByGroupName.Add($"{name}@{domain}".ToLower(), group);
                                computerLocalAdmins.AddAdminGroup(group);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Unable to resolve local admins for {localAdmin.name}");
                    Console.WriteLine(e.ToString());
                    
                }
            }

            foreach (ComputerLocalAdmins computerLocalAdmins in computerLocalAdminsList)
            {
                ComputerExt computer = computerLocalAdmins.computer;
                string groupSID = $"{computer.ObjectIdentifier}-500";
                Group groupObj = new Group
                {
                    ObjectIdentifier = groupSID
                };


                groupObj.Properties.Add("name", $"ADMINISTRATORS@{computer.Properties["name"]}");
                //groups.Add(groupObj); commented out as we don't want to add new BH objects for local groups


                computerLocalAdmins.PopulateComputerExtWithLocalAdminData(groupSID);
            }
            

            // handle new bloodhound format
            foreach (ComputerExt computer in computers)
            {
                LocalGroupAPIResult administrators = new LocalGroupAPIResult();
                administrators.Name = $"ADMINISTRATORS@{computer.Properties["name"]}";
                administrators.Results = computer.LocalAdmins.getTypedPrincipals();
                administrators.ObjectIdentifier = $"{computer.Properties["name"]}-S-1-5-32-544";
                administrators.Collected = true;

                if (administrators.Results.Length > 0)
                {
                    LocalGroupAPIResult[] localGroups = { administrators };
                    computer.LocalGroups = localGroups;
                }

                Debug.Print((string)computer.Properties["name"]);
            }
        }
    }
}
