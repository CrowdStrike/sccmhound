using SCCMHound.src.models;
using SharpHoundCommonLib;
using SharpHoundCommonLib.OutputTypes;
using SharpHoundCommonLib.Processors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Group = SharpHoundCommonLib.OutputTypes.Group;

namespace SCCMHound.src
{
    public class SCCMCollector
    {

        private SCCMConnector connection {  get; set; } 

        public SCCMCollector(SCCMConnector connection)
        {
            this.connection = connection;
        }

        private ManagementObjectCollection executeQuery(ObjectQuery query)
        {
            Debug.Print("Query: {0}", query);
            EnumerationOptions options = new EnumerationOptions
            {
                Rewindable = false,
                BlockSize = 0x32, // get WMI results in 0x32 sized blocks
                ReturnImmediately = true,
                EnsureLocatable = true
            };

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(this.connection.scope, query, options);
            System.Management.ManagementObjectCollection res = searcher.Get();

#if DEBUG
            /*
            ManagementObjectCollection.ManagementObjectEnumerator enumerator = res.GetEnumerator();
            if(enumerator.MoveNext())
            {
                ManagementObject firstElem = (ManagementObject)enumerator.Current;
                foreach (PropertyData prop in firstElem.Properties)
                {
                    Debug.Print("{0}: {1}", prop.Name, prop.Value);
                }
            }  
            */
#endif
            return res;
        }

        public List<UserMachineRelationship> QueryUserMachineRelationships()
        {
            //ManagementObjectCollection relationshipCollection = executeQuery(new SelectQuery("SELECT * FROM SMS_UserMachineIntelligence")); Was initially using this to pull primary user data BUT this was unreliable in terms of sessions. TODO: add this as an additional object parameter
            ManagementObjectCollection relationshipCollection = executeQuery(new SelectQuery("SELECT * FROM SMS_CombinedDeviceResources WHERE CurrentLogonUser IS NOT NULL"));
            List<UserMachineRelationship> relationships = new List<UserMachineRelationship>();

            foreach (ManagementObject mObj in relationshipCollection)
            {
                try
                {
                    string resourceName = mObj["Name"].ToString();
                    string recordUser = mObj["CurrentLogonUser"].ToString();
                    UserMachineRelationship relationship = new UserMachineRelationship(resourceName, recordUser);
                    relationships.Add(relationship);
                }
                // TODO: replace with specific exception handlers
                catch (Exception ex)
                {
                    Debug.Print(ex.ToString());
                }
            }
            return relationships;
        }

        // Not yet used in the output BloodHound datasets
        public List<ApplicationInstallation> QueryApplications()
        {
            List<ApplicationInstallation> applicationInstallations = new List<ApplicationInstallation>();
            ManagementObjectCollection apps64 = executeQuery(new ObjectQuery("SELECT DISTINCT DisplayName FROM SMS_G_System_ADD_REMOVE_PROGRAMS_64"));
            ManagementObjectCollection apps32 = executeQuery(new ObjectQuery("SELECT DISTINCT DisplayName FROM SMS_G_System_ADD_REMOVE_PROGRAMS"));

            Dictionary<string, List<string>> groupedApps64 = new Dictionary<string, List<string>>();



            foreach (ManagementObject mobjApplication64 in apps64)
            {
                string applicationDisplayName = getPropertyFromManagementObject(mobjApplication64, "DisplayName");
                if (!applicationDisplayName.Equals(""))
                { 
                    string lookupKey = applicationDisplayName.Substring(0, Math.Min(applicationDisplayName.Length, 6));

                    if (!groupedApps64.ContainsKey(lookupKey))
                    {
                        groupedApps64.Add(lookupKey, new List<string>());
                    }

                    groupedApps64[lookupKey].Add(applicationDisplayName);
            }


            }

            Dictionary<string, List<string>> groupedApps32 = new Dictionary<string, List<string>>();



            foreach (ManagementObject mObjApplications32 in apps32)
            {
                string applicationDisplayName = getPropertyFromManagementObject(mObjApplications32, "DisplayName");

                if (!applicationDisplayName.Equals(""))
                {
                    string lookupKey = applicationDisplayName.Substring(0, Math.Min(applicationDisplayName.Length, 6));

                    if (!groupedApps32.ContainsKey(lookupKey))
                    {
                        groupedApps32.Add(lookupKey, new List<string>());
                    }

                    groupedApps32[lookupKey].Add(applicationDisplayName);

                }
            }

            foreach (string key in groupedApps64.Keys)
            {
                try
                {
                    
                    // Consider batching based on fixed size (100 apps at a time)
                    ManagementObjectCollection applicationCollection64 = executeQuery(new SelectQuery(string.Format("SELECT ResourceId, DisplayName FROM SMS_G_System_ADD_REMOVE_PROGRAMS_64 WHERE DisplayName LIKE \"{0}%\"", key))); // TODO: better way to add args for WQL like prepared statements?

                    foreach (ManagementObject mObj in applicationCollection64)
                    {
                        try
                        {
                            ApplicationInstallation applicationInstallation;

                            string resourceID = getPropertyFromManagementObject(mObj, "ResourceId");
                            string displayName = getPropertyFromManagementObject(mObj, "DisplayName");

                            if (!(resourceID.Equals("") || displayName.Equals("")))
                            {
                                applicationInstallation = new ApplicationInstallation(displayName, resourceID, 64);
                                applicationInstallations.Add(applicationInstallation);
                            }

                        }
                        // TODO: replace with specific exception handlers
                        catch (Exception ex)
                        {
                            Debug.Print(ex.ToString());
                        }
                    }
                }
                // TODO: replace with specific exception handlers
                catch (Exception ex)
                {
                    Debug.Print(ex.ToString());
                }
            }

            foreach (string key in groupedApps32.Keys)
            {
                try
                {
                    ManagementObjectCollection applicationCollection32 = executeQuery(new SelectQuery(string.Format("SELECT ResourceId, DisplayName FROM SMS_G_System_ADD_REMOVE_PROGRAMS WHERE DisplayName LIKE \"{0}%\"", key)));
                    foreach (ManagementObject mObj in applicationCollection32)
                    {
                        try
                        {
                            ApplicationInstallation applicationInstallation;

                            string resourceID = getPropertyFromManagementObject(mObj, "ResourceId");
                            string displayName = getPropertyFromManagementObject(mObj, "DisplayName");

                            if (!(resourceID.Equals("") || displayName.Equals("")))
                            {
                                applicationInstallation = new ApplicationInstallation(displayName, resourceID, 32);
                                applicationInstallations.Add(applicationInstallation);
                            }
                        }
                        // TODO: replace with specific exception handlers
                        catch (Exception ex)
                        {
                            Debug.Print(ex.ToString());
                        }
                    }
                
                }
                // TODO: replace with specific exception handlers
                catch (Exception ex)
                {
                    Debug.Print(ex.ToString());
                }
            }

            // TODO: arm?

            return applicationInstallations;
        }


            public List<Group> QueryGroups()
        {
            List<Group> groups = new List<Group>();
            ManagementObjectCollection usersCollection = executeQuery(new SelectQuery("SELECT * FROM SMS_R_UserGroup"));
            foreach (ManagementObject mObj in usersCollection)
            {
                try
                {
                    string sid = getPropertyFromManagementObject(mObj, "SID");

                    if (!string.IsNullOrEmpty(sid))
                    {
                        Group groupObj = new Group
                        {
                            ObjectIdentifier = sid
                        };

                        string domain = getPropertyFromManagementObject(mObj, "ADDomainName").ToUpper();
                        if (!domain.Equals("")) groupObj.Properties.Add("domain", domain);

                        groupObj.Properties.Add("domainsid", HelperUtilities.getDomainSidFromUserSid(sid));

                        string name = getPropertyFromManagementObject(mObj, "UniqueUsergroupName");
                        if (!name.Equals("")) groupObj.Properties.Add("name", $"{name.Split('\\')[1].ToUpper()}@{domain.ToUpper()}");

                        groups.Add(groupObj);
                    }
                }
                // TODO: replace with specific exception handlers
                catch (Exception ex)
                {
                    Debug.Print(ex.ToString());
                }
            }
            return groups;
        }


                public List<User> QueryUsers()
        {
            List<User> users = new List<User>();
            ManagementObjectCollection usersCollection = executeQuery(new SelectQuery("SELECT * FROM SMS_R_User"));

            foreach (ManagementObject mObj in usersCollection)
            {
                try
                {
                    string sid = getPropertyFromManagementObject(mObj, "SID");

                    Regex azureSidRegex = new Regex(@"[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}\\[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}");
                    if (azureSidRegex.IsMatch(sid))
                    {
                        Debug.Print("Azure object - skipping for now");
                        // skip for now, is azure object TODO
                    }
                    else if (!string.IsNullOrEmpty(sid))
                    {
                        User userObj = new User
                        {
                            ObjectIdentifier = sid
                        };

                        string sccmUniqueUserName = getPropertyFromManagementObject(mObj, "UniqueUserName");
                        
                        // If not computer account
                        if (!sccmUniqueUserName.EndsWith("$"))
                        {
                            if (!sccmUniqueUserName.Equals("")) userObj.Properties.Add("sccmUniqueUserName", sccmUniqueUserName);

                            string name = getPropertyFromManagementObject(mObj, "UserPrincipalName"); // throws exceptions - refactor? TODO

                            string domain = getPropertyFromManagementObject(mObj, "FullDomainName"); ;
                            if (domain.Equals(""))
                            {
                                
                                Debug.Print("Domain is blank");
                                Regex getDomainFromName = new Regex(@"(?<=\@)(.?)*");
                                Match regexMatch = getDomainFromName.Match(name);
                                domain = regexMatch.Value;
                            }
                            else
                            {
                                userObj.Properties.Add("domain", domain.ToUpper()); // BloodHound capitalizes these names
                            }

                            if (name.Contains("onmicrosoft.com"))
                            {
                                userObj.Properties.Add("sccmMicrosoftAccountName", name.ToUpper());
                            }

                            
                            // Convert sccmUniqueUserName to name
                            Regex stripNetbios = new Regex(@"(?<=\\)(.?)*");
                            Match regexNameMatch = stripNetbios.Match(sccmUniqueUserName);
                            name = String.Format("{0}@{1}", regexNameMatch.Value, domain);
                            

                            userObj.Properties.Add("name", name.ToUpper());// BloodHound capitalizes these names







                            string distinguishedname = getPropertyFromManagementObject(mObj, "DistinguishedName").ToUpper() ;
                            if (!distinguishedname.Equals("")) userObj.Properties.Add("distinguishedname", distinguishedname);
                            userObj.Properties.Add("domainsid", HelperUtilities.getDomainSidFromUserSid(sid));

                            string displayname = getPropertyFromManagementObject(mObj, "FullUserName");
                            if (!displayname.Equals("")) userObj.Properties.Add("displayname", displayname);

                            string email = getPropertyFromManagementObject(mObj, "Mail");
                            if (!email.Equals("")) userObj.Properties.Add("email", email);



                            string primaryGroupID = getPropertyFromManagementObject(mObj, "PrimaryGroupID");
                            if (!primaryGroupID.Equals("")) userObj.PrimaryGroupSID = String.Format("{0}-{1}", HelperUtilities.getDomainSidFromUserSid(sid), primaryGroupID);

                            string sccmCreationDate = getPropertyFromManagementObject(mObj, "CreationDate");
                            if (!sccmCreationDate.Equals("")) userObj.Properties.Add("sccmCreationDate", sccmCreationDate); // Add creation date to sccmCreationDate instead of whencreated as this is use for the AD objects creation date

                            string[] sccmAgentName = (string[])getPropertyObjectFromManagementObject(mObj, "AgentName");
                            if (!sccmAgentName.Equals(null)) userObj.Properties.Add("sccmAgentName", sccmAgentName);

                            string[] sccmAgentSite = (string[])getPropertyObjectFromManagementObject(mObj, "AgentSite");
                            if (!sccmAgentSite.Equals(null)) userObj.Properties.Add("sccmAgentSite", sccmAgentSite);

                            string[] sccmAgentTime = (string[])getPropertyObjectFromManagementObject(mObj, "AgentTime");
                            if (!sccmAgentTime.Equals(null)) userObj.Properties.Add("sccmAgentTime", sccmAgentTime);

                            string sccmUserAccountControl = getPropertyFromManagementObject(mObj, "UserAccountControl");
                            if (!sccmUserAccountControl.Equals("")) userObj.Properties.Add("sccmUserAccountControl", sccmUserAccountControl);

                            string[] sccmUserContainerName = (string[])getPropertyObjectFromManagementObject(mObj, "UserContainerName");
                            if (!sccmUserContainerName.Equals(null)) userObj.Properties.Add("sccmUserContainerName", sccmUserContainerName);

                            string[] sccmUserGroupName = (string[])getPropertyObjectFromManagementObject(mObj, "UserGroupName"); // TODO: build group collector
                            if (!sccmUserGroupName.Equals(null)) userObj.Properties.Add("sccmUserGroupName", sccmUserGroupName);

                            string sccmResourceID = getPropertyFromManagementObject(mObj, "ResourceID");
                            if (!sccmResourceID.Equals("")) userObj.Properties.Add("sccmResourceID", sccmResourceID);

                            /*
                            Debug.Print("User Object Identifier: {0}", userObj.ObjectIdentifier);
                            foreach (KeyValuePair<string, object> prop in userObj.Properties)
                            {
                                Debug.Print("{0}: {1}", prop.Key, prop.Value);
                            }
                            */
                            if (sccmUniqueUserName.Equals("") || domain.Equals("") || distinguishedname.Equals("") || displayname.Equals("") || primaryGroupID.Equals(""))
                            {
                                Debug.Print("Object missing properties");
                            }
                            else
                            {
                                Debug.Print("Object not missing properties");
                            }

                            users.Add(userObj);
                        }
                    }
                }
                // TODO: replace with specific exception handlers
                catch (Exception ex)
                {
                    Debug.Print(ex.ToString());
                }
            }
            return users;
        }

        private object getPropertyObjectFromManagementObject(ManagementObject mObj, string query)
        {
            object result;

            try
            {
                result = mObj[query];
            }
            // TODO: replace with specific exception handlers
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
                result = null;
            }

            return result;

        }

        private string getPropertyFromManagementObject(ManagementObject mObj, string query) 
        {
            string result = "";

            try
            {
                if (mObj[query] != null)
                {
                    result = mObj[query].ToString();
                }
                
            }
            catch (Exception ex)
            {
                Debug.Print("Could not retreive property");
            }

            return result;

        }

        public List<ComputerExt> QueryComputers()
        {
            List<ComputerExt> computers = new List<ComputerExt>();

            ManagementObjectCollection computersCollection = executeQuery(new SelectQuery("SELECT * FROM SMS_R_System")); // update query to ignore obsolete?


            foreach (ManagementObject mObj in computersCollection)
            {
                try
                {
                    string sid = getPropertyFromManagementObject(mObj, "SID");
                    if (!string.IsNullOrEmpty(sid))
                    {
                        ComputerExt compObj = new ComputerExt
                        {
                            ObjectIdentifier = sid
                        };
                        

                        string domain = getPropertyFromManagementObject(mObj, "FullDomainName");
                        if (!domain.Equals(""))
                        {
                            compObj.Properties.Add("domain", domain.ToUpper());
                        }
                        else
                        {
                            string dn = getPropertyFromManagementObject(mObj, "DistinguishedName");
                            if (!dn.Equals("")) {
                                domain = HelperUtilities.ConvertLdapToDomain(dn);
                                compObj.Properties.Add("domain", domain.ToUpper());
                            }
                            else
                            {
                                string resourceName = ((string[])getPropertyObjectFromManagementObject(mObj, "ResourceNames"))[0];
                                string netbiosName = getPropertyFromManagementObject(mObj, "NetbiosName");

                                if ((!resourceName.Equals("")) && (!netbiosName.Equals(""))) {
                                    domain = HelperUtilities.GetDomainFromResource(resourceName, netbiosName);
                                    if (!domain.Equals(""))
                                    {
                                        compObj.Properties.Add("domain", domain.ToUpper());
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Unable to resolve domains for {resourceName}");

                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Unable to resolve domains for {resourceName}");
                                }
                            }
                        }//BloodHound capitalizes these names

                        string name = getPropertyFromManagementObject(mObj, "Name");
                        if (!name.Equals("")) compObj.Properties.Add("name", String.Format("{0}.{1}",name,domain).ToUpper()); // BloodHound capitalizes these names
                        // Need to add else to populate domain if doesnt find it

                        string distinguishedname = getPropertyFromManagementObject(mObj, "DistinguishedName");
                        if (!distinguishedname.Equals("")) compObj.Properties.Add("distinguishedname", distinguishedname);

                        compObj.Properties.Add("domainsid", HelperUtilities.getDomainSidFromUserSid(sid));

                        string samaccountname = getPropertyFromManagementObject(mObj, "NetbiosName");
                        if (!samaccountname.Equals("")) compObj.Properties.Add("samaccountname", samaccountname + "$"); // BloodHound capitalizes these names

                        string primaryGroupID = getPropertyFromManagementObject(mObj, "PrimaryGroupID");
                        if (!primaryGroupID.Equals("")) compObj.PrimaryGroupSID = String.Format("{0}-{1}", HelperUtilities.getDomainSidFromUserSid(sid), primaryGroupID);

                        string operatingSystem = getPropertyFromManagementObject(mObj, "OperatingSystemNameandVersion");
                        if (!operatingSystem.Equals("")) compObj.Properties.Add("operatingsystem", operatingSystem);

                        string sccmName = getPropertyFromManagementObject(mObj, "Name");
                        if (!sccmName.Equals("")) compObj.Properties.Add("sccmName", sccmName);

                        Boolean sccmActive = Convert.ToBoolean(getPropertyObjectFromManagementObject(mObj, "Active"));
                        compObj.Properties.Add("sccmActive", sccmActive);

                        string sccmADSiteName = getPropertyFromManagementObject(mObj, "ADSiteName");
                        if (!sccmADSiteName.Equals("")) compObj.Properties.Add("sccmADSiteName", sccmADSiteName);

                        Boolean sccmClient = Convert.ToBoolean(getPropertyObjectFromManagementObject(mObj, "Client"));
                        compObj.Properties.Add("sccmClient", sccmClient);

                        Boolean sccmDecomissioned = Convert.ToBoolean(getPropertyObjectFromManagementObject(mObj, "Decomissioned"));
                        if (!sccmDecomissioned.Equals(null)) compObj.Properties.Add("sccmDecomissioned", sccmDecomissioned);

                        string[] sccmIPAddresses = (string[])getPropertyObjectFromManagementObject(mObj, "IPAddresses");
                        if (!sccmIPAddresses.Equals("")) compObj.Properties.Add("sccmIPAddresses", sccmIPAddresses);

                        string sccmLastLogonUserDomain = getPropertyFromManagementObject(mObj, "LastLogonUserDomain");
                        if (!sccmLastLogonUserDomain.Equals("")) compObj.Properties.Add("sccmLastLogonUserDomain", sccmLastLogonUserDomain);

                        string sccmLastLogonUserName = getPropertyFromManagementObject(mObj, "LastLogonUserName");
                        if (!sccmLastLogonUserName.Equals("")) compObj.Properties.Add("sccmLastLogonUserName", sccmLastLogonUserName);

                        string sccmLastLogonTimestamp = getPropertyFromManagementObject(mObj, "LastLogonTimestamp");
                        if (!sccmLastLogonTimestamp.Equals("")) compObj.Properties.Add("sccmLastLogonTimestamp", sccmLastLogonTimestamp);

                        string sccmResourceDomainORWorkgroup = getPropertyFromManagementObject(mObj, "ResourceDomainORWorkgroup");
                        if (!sccmResourceDomainORWorkgroup.Equals("")) compObj.Properties.Add("sccmResourceDomainORWorkgroup", sccmResourceDomainORWorkgroup);

                        string[] sccmSystemContainerName = (string[])getPropertyObjectFromManagementObject(mObj, "SystemContainerName");
                        if (!sccmSystemContainerName.Equals("")) compObj.Properties.Add("sccmSystemContainerName", sccmSystemContainerName);

                        string[] sccmSystemGroupName = (string[])getPropertyObjectFromManagementObject(mObj, "SystemGroupName");
                        if (!sccmSystemGroupName.Equals("")) compObj.Properties.Add("sccmSystemGroupName", sccmSystemGroupName);

                        string[] sccmSystemRoles = (string[])getPropertyObjectFromManagementObject(mObj, "SystemRoles");
                        if (!sccmSystemRoles.Equals("")) compObj.Properties.Add("sccmSystemRoles", sccmSystemRoles);

                        string[] sccmResourceNames = (string[])getPropertyObjectFromManagementObject(mObj, "ResourceNames");
                        if (!sccmResourceNames.Equals("")) compObj.Properties.Add("sccmResourceNames", sccmResourceNames);

                        string sccmUserAccountControl = getPropertyFromManagementObject(mObj, "UserAccountControl");
                        if (!sccmUserAccountControl.Equals("")) compObj.Properties.Add("sccmUserAccountControl", sccmUserAccountControl);

                        string sccmResourceID = getPropertyFromManagementObject(mObj, "ResourceID");
                        if (!sccmResourceID.Equals("")) compObj.Properties.Add("sccmResourceID", sccmResourceID);

                        /*
                        Debug.Print("Computer Object Identifier: {0}", compObj.ObjectIdentifier);
                        foreach (KeyValuePair<string, object> prop in compObj.Properties)
                        {
                            Debug.Print("{0}: {1}", prop.Key, prop.Value);
                        }
                        */

                        computers.Add(compObj);
                    }
                }
                // TODO: replace with specific exception handlers
                catch (Exception ex)
                {
                    Debug.Print(ex.ToString());
                }

                
            }
            return computers;
        }
    }
}
