using SCCMHound.src;
using SharpHoundCommonLib;
using SharpHoundCommonLib.Enums;
using SharpHoundCommonLib.OutputTypes;
using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;

public class GroupFactory
{
    public static Group CreateGroup(string groupName, string domainName, List<SharpHoundCommonLib.OutputTypes.Domain> domains)
    {
        Console.WriteLine($"Attempting to resolve {groupName} via identified domains");
        if (string.IsNullOrEmpty(groupName))
            return null;

        string objectIdentifier = groupName;

        foreach (SharpHoundCommonLib.OutputTypes.Domain domain in domains)
        {
            if (domainName.ToUpper().Equals(domain.Properties["name"].ToString().ToUpper()))
            {
                try
                {
                    objectIdentifier = HelperUtilities.GetGroupSid(groupName);
                }
                catch(Exception e) {
                    Console.WriteLine($"AD query for {groupName} failed");
                    objectIdentifier = groupName;
                }
            }
            break;
        }

        var group = new Group
        {
            ObjectIdentifier = objectIdentifier,

        };


        group.Properties.Add("name", groupName);
        group.Properties.Add("domain", domainName);

        return group;
    }
}