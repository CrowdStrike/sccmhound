using SCCMHound.src.models;
using SharpHoundCommonLib.OutputTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Domain = SharpHoundCommonLib.OutputTypes.Domain;
using Group = SharpHoundCommonLib.OutputTypes.Group;

namespace SCCMHound.src
{
    public class DomainsResolver
    {
        public List<Domain> domains { get; set; } = new List<Domain>();
        public DomainsResolver(List<User> users, List<ComputerExt> computers, List<Group> groups)
        {
            foreach (User user in users)
            {
                try
                {
                    if (!domainAlreadyResolved((string)user.Properties["domainsid"]))
                    {
                        Domain domainObj = new Domain
                        {
                            ObjectIdentifier = (string)user.Properties["domainsid"],
                        };
                        domainObj.Properties["netbios"] = user.Properties["sccmUniqueUserName"].ToString().Split('\\')[0];
                        domainObj.Properties["name"] = user.Properties["domain"];
                        domainObj.Properties["domain"] = user.Properties["domain"];
                        domainObj.Properties["domainsid"] = user.Properties["domainsid"];
                        domainObj.Properties["highvalue"] = true;
                        domainObj.Properties["distinguishedname"] = $"DC={((string)user.Properties["distinguishedname"]).Substring(((string)user.Properties["distinguishedname"]).IndexOf("DC=") + 3)}".ToUpper();

                        domains.Add(domainObj);
                    }
                }
                catch (KeyNotFoundException e) { } // Prevent exception bubbling up when a User object does not contain a required attribute
                
            }
            Debug.Print("Resolved domains from users");

            foreach (ComputerExt computer in computers)
            {
                try
                {
                    if (!domainAlreadyResolved((string)computer.Properties["domainsid"]))
                    {
                        Domain domainObj = new Domain
                        {
                            ObjectIdentifier = (string)computer.Properties["domainsid"],
                        };
                        domainObj.Properties["netbios"] = computer.Properties["sccmResourceDomainORWorkgroup"];
                        domainObj.Properties["name"] = computer.Properties["domain"];
                        domainObj.Properties["domain"] = computer.Properties["domain"];
                        domainObj.Properties["domainsid"] = computer.Properties["domainsid"];
                        domainObj.Properties["highvalue"] = true;
                        domainObj.Properties["distinguishedname"] = $"DC={((string)computer.Properties["distinguishedname"]).Substring(((string)computer.Properties["distinguishedname"]).IndexOf("DC=") + 3)}".ToUpper();

                        domains.Add(domainObj);
                    }
                }
                catch (KeyNotFoundException e) { } // Prevent exception bubbling up when a Computer object does not contain a required attribute

            }
            Debug.Print("Resolved domains from computers");

            foreach (Group group in groups)
            {
                try
                {
                    if (!domainAlreadyResolved((string)group.Properties["domainsid"]))
                    {
                        Domain domainObj = new Domain
                        {
                            ObjectIdentifier = (string)group.Properties["domainsid"],
                        };

                        domainObj.Properties["name"] = group.Properties["domain"];
                        domainObj.Properties["domain"] = group.Properties["domain"];
                        domainObj.Properties["domainsid"] = group.Properties["domainsid"];
                        domainObj.Properties["highvalue"] = true;
                        domainObj.Properties["distinguishedname"] = string.Join(",", ((string)group.Properties["domain"]).Split('.').Select(part => $"DC={part.ToUpper()}"));

                        domains.Add(domainObj);
                    }
                }
                catch (KeyNotFoundException e) { } // Prevent exception bubbling up when a Group object does not contain a required attribute

            }
            Debug.Print("Resolved domains from groups");

        }

        Boolean domainAlreadyResolved(string objectIdentifier)
        {
            foreach (Domain domain in this.domains)
            {
                if (domain.ObjectIdentifier.Equals(objectIdentifier)) {
                    return true;
                }
            }
            return false;
        }

    }
}
