using SCCMHound.src;
using SharpHoundCommonLib;
using SharpHoundCommonLib.Enums;
using SharpHoundCommonLib.OutputTypes;
using System;
using System.Collections.Generic;

public class UserFactory
{
    public static User CreateUser(string userName, string domainName, List<Domain> domains)
    {
        Console.WriteLine($"Attempting to resolve {userName} via identified domains");
        if (string.IsNullOrEmpty(userName))
            return null;

        string objectIdentifier = userName;

        foreach (Domain domain in domains)
        {
            if (domainName.Equals(domain.Properties["name"])) {
                try
                {
                    if (userName.Split('@')[0] != "ADMINISTRATOR")
                    {
                        objectIdentifier = HelperUtilities.GetUserSid(userName);
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine($"AD query for {userName} failed");
                    objectIdentifier = userName;
                }

                break;

            }
        }

        

        var user = new User
        {
            ObjectIdentifier = objectIdentifier,

        };


        user.Properties.Add("name", userName);
        user.Properties.Add("domain", domainName);

        return user;
    }
}