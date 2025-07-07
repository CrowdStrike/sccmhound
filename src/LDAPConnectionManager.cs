using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;

namespace SCCMHound.src
{
    public class LdapConnectionManager
    {
        private static LdapConnectionManager connectionManager;
        private Dictionary<string, PrincipalContext> ldapPrincipals;
        private List<string> failedDomains = [];

        // Mutex for thread safety
        private readonly object _lock = new object();

        // Singleton
        public static LdapConnectionManager Instance
        {
            get
            {
                if (connectionManager == null)
                {
                    connectionManager = new LdapConnectionManager();
                }
                return connectionManager;
            }
        }

        // Private constructor
        private LdapConnectionManager()
        {
            ldapPrincipals = new Dictionary<string, PrincipalContext>(StringComparer.OrdinalIgnoreCase);
        }

        public PrincipalContext GetDomainContext(string domain)
        {
            if (string.IsNullOrEmpty(domain))
                throw new ArgumentException("Domain cannot be null or empty");

            lock (_lock)
            {
                // If the domain resolution previously failed, return null
                if (this.failedDomains.Contains(domain)) {
                    return null;
                }

                // Create a new domain context if it does not already exist
                if (!ldapPrincipals.TryGetValue(domain, out PrincipalContext context))
                {
                    try
                    {
                        context = new PrincipalContext(ContextType.Domain, domain);
                        ldapPrincipals[domain] = context;
                        Console.WriteLine($"Created new PrincipalContext for domain: {domain}");
                    }
                    // If an exception is hit when trying to create a new principal context, add it to the list of failed domains and return null
                    catch (Exception e)
                    {
                        this.failedDomains.Add(domain);
                        return null;
                    }
                }
                return context;
            }
        }

        // Disposes all principal contexts
        public void Cleanup()
        {
            lock (_lock)
            {
                foreach (var context in ldapPrincipals.Values)
                {
                    context.Dispose();
                }
                ldapPrincipals.Clear();
            }
        }
    }
}
