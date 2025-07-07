using ProtoBuf.Meta;
using SCCMHound.src.models;
using SharpHoundCommonLib.OutputTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Group = SharpHoundCommonLib.OutputTypes.Group;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;


namespace SCCMHound.src
{
    public class HelperUtilities
    {

        public static string lookupNetbiosReturnFQDN(string netbios, List<Domain> domains)
        {
            foreach (Domain domain in domains)
            {
                if (domain.Properties["netbios"].ToString().ToUpper().Equals(netbios.ToUpper()))
                {
                    return domain.Properties["name"].ToString();
                }
            }

            return netbios;
        }

        public static string GetGroupSid(string groupPrincipalName)
        {
            try
            {
                // Parse group name and domain from groupPrincipalName
                var parts = groupPrincipalName.Split('@');
                if (parts.Length != 2)
                    throw new ArgumentException("Input must be in the format group@domain");

                string groupName = parts[0];
                string domain = parts[1];

                Console.WriteLine($"Attempting to resolve {groupName} via LDAP://{domain}");

                LdapConnectionManager connectionManager = LdapConnectionManager.Instance;
                using (var context = connectionManager.GetDomainContext(domain))
                using (var group = GroupPrincipal.FindByIdentity(context, IdentityType.Name, groupName))
                {
                    if (group == null)
                        throw new Exception("Group not found.");

                    return group.Sid.ToString();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static string GetUserSid(string userPrincipalName)
        {
            try
            {
                // Parse user and domain from userPrincipalName
                var parts = userPrincipalName.Split('@');
                if (parts.Length != 2)
                    throw new ArgumentException("Input must be in the format user@domain");

                string username = parts[0];
                string domain = parts[1];

                Console.WriteLine($"Attempting to resolve {username} via LDAP://{domain}");

                LdapConnectionManager connectionManager = LdapConnectionManager.Instance;
                using (var context = connectionManager.GetDomainContext(domain))
                using (var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, username))
                {
                    if (user == null)
                        throw new Exception("User not found.");

                    return user.Sid.ToString();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static string ConvertLdapToDomain(string ldapPath)
        {
            if (string.IsNullOrEmpty(ldapPath))
                return string.Empty;

            // Split the LDAP path into parts
            var parts = ldapPath.Split(',');

            // Filter only DC components and remove the "DC=" prefix
            var domainParts = parts
                .Where(part => part.Trim().ToUpper().StartsWith("DC="))
                .Select(part => part.Trim().Substring(3));

            // Join the parts with dots
            return string.Join(".", domainParts);
        }

        public static string getDomainSidFromUserSid(string sid)
        {
            Match result = Regex.Match(sid, @"^(.*)(?=-)");
            return result.Value;
        }

        public static Dictionary<string, User> createLookupTableUsers(List<User> collection)
        {
            Dictionary<string, User> lookupTable = new Dictionary<string, User>();
            foreach (User element in collection)
            {
                try
                {
                    lookupTable.Add(element.Properties["sccmUniqueUserName"].ToString().ToLower(), element);
                }
                // There may be duplicates with the same name in the SCCM dataset. TODO: workout which one has the most active/accurate data and only store that
                catch (ArgumentException ex)
                {
                    Debug.Print(ex.ToString());
                }
                catch (KeyNotFoundException ex) // handles users which dont have a sccmUniqueUserName which is used for session correlation
                {
                    Debug.Print(ex.ToString());
                }
            }

            return lookupTable;
        }

        public static Dictionary<string, Group> createLookupTableGroups(List<SharpHoundCommonLib.OutputTypes.Group> collection)
        {
            Dictionary<string, Group> lookupTable = new Dictionary<string, Group>();
            foreach (Group element in collection)
            {
                try
                {
                    lookupTable.Add(element.Properties["name"].ToString().ToLower(), element);
                }
                // There may be duplicates with the same name in the SCCM dataset. TODO: workout which one has the most active/accurate data and only store that
                catch (ArgumentException ex)
                {
                    Debug.Print(ex.ToString());
                }
                catch (KeyNotFoundException ex) // handles users which dont have a sccmUniqueUserName which is used for session correlation
                {
                    Debug.Print(ex.ToString());
                }
            }

            return lookupTable;
        }



        public static Dictionary<string, ComputerExt> createLookupTableComputers(List<ComputerExt> collection)
        {
            Dictionary<string, ComputerExt> lookupTable = new Dictionary<string, ComputerExt>();
            foreach (ComputerExt element in collection)
            {
                try
                {
                    lookupTable.Add(element.Properties["sccmName"].ToString(), element);
                }

                // There may be duplicates with the same name in the SCCM dataset. TODO: workout which one has the most active/accurate data and only store that
                catch (ArgumentException ex)
                {
                    Debug.Print(ex.ToString());
                }

            }

            return lookupTable;
        }

        // Revisit
        public static void ConfigureRttModel(RuntimeTypeModel rttModel, Type type)
        {
            if (rttModel.CanSerialize(type)) return;

            var metaType = rttModel.Add(type, false);
            int counter = 1;

            foreach (var property in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite))
            {
                
                var dataField = metaType.Add(counter++, property.Name);
                
                var childType = property.PropertyType;

                if (childType.IsArray)
                {
                    childType = childType.GetElementType();
                }

                if (childType.IsGenericType && childType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    childType = childType.GetGenericArguments()[0];
                }

                if (!childType.IsPrimitive && childType != typeof(string) && !rttModel.CanSerialize(childType))
                {
                    ConfigureRttModel(rttModel, childType); // recursive call to serialize child type
                }

            }
        }

        public static string GetDomainFromResource(string resourceName, string netbiosName)
        {
            try
            {
                // Remove any leading or trailing spaces
                resourceName = resourceName?.Trim();
                netbiosName = netbiosName?.Trim();

                if (string.IsNullOrEmpty(resourceName) || string.IsNullOrEmpty(netbiosName))
                    return string.Empty;

                // Convert both strings to uppercase for case-insensitive comparison
                string upperResourceName = resourceName.ToUpper();
                string upperNetbiosName = netbiosName.ToUpper();

                // If resource name contains the netbios name, extract everything after it
                if (upperResourceName.Contains(upperNetbiosName))
                {
                    int index = upperResourceName.IndexOf(upperNetbiosName);
                    if (index >= 0)
                    {
                        // Get everything after the netbios name from the original string
                        // and trim both backslashes and dots
                        string domain = resourceName.Substring(index + netbiosName.Length).Trim('\\', '.');
                        return domain;
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
