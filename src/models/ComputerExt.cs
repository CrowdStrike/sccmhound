using SharpHoundCommonLib.OutputTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCCMHound.src.models
{
    public class SHLocalAdmin
    {
        public string ObjectIdentifier;
        public string ObjectType;

        public SHLocalAdmin(string memberName, string memberType)
        {
            ObjectIdentifier = memberName;
            ObjectType = memberType;
        }

        public SHLocalAdmin() { }

        
    }

    public class SHLocalAdmins
    {
        public SHLocalAdmin[] Results;
        Boolean collected;

        public SHLocalAdmins(SHLocalAdmin[] results)
        {
            this.Results = results;

            if (results.Length > 0) { this.collected = true; }
        }

        public TypedPrincipal[] getTypedPrincipals()
        {
            List<TypedPrincipal> principals = new List<TypedPrincipal>();

            foreach (SHLocalAdmin admin in Results)
            {
                TypedPrincipal tp = new TypedPrincipal();
                tp.ObjectIdentifier = admin.ObjectIdentifier;
                if (Enum.TryParse(admin.ObjectType, out SharpHoundCommonLib.Enums.Label lbl))
                {
                    tp.ObjectType = lbl;
                }
                principals.Add(tp);
            }

            return principals.ToArray();
        }
    }


    public class ComputerExt : Computer
    {

        public SHLocalAdmins LocalAdmins = new SHLocalAdmins(Array.Empty<SHLocalAdmin>());
        public SessionAPIResult RemoteDesktopUsers { get; set; } = new SessionAPIResult();
        public SessionAPIResult DcomUsers { get; set; } = new SessionAPIResult();
        public SessionAPIResult PSRemoteUsers { get; set; } = new SessionAPIResult();
    }
}
