using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pug.Application.Security
{
    public interface IPrincipal : System.Security.Principal.IPrincipal, IDisposable
    {
        new IPrincipalIdentity Identity { get; }

        [Obsolete( "Use overload with NounQualifier for resource instead" )]
        bool IsAuthorized(IDictionary<string, string> context, string operation, DomainObject resource, string purpose = "");

        bool IsAuthorized(IDictionary<string, string> context, string operation, NounQualifier resource, string purpose = "");

        [Obsolete( "Use overload with NounQualifier for resource instead" )]
        Task<bool> IsAuthorizedAsync(IDictionary<string, string> context, string operation, DomainObject resource, string purpose = "");

        Task<bool> IsAuthorizedAsync(IDictionary<string, string> context, string operation, NounQualifier resource, string purpose = "");

        IEnumerable<string> GetRoles();
		
        Task<IEnumerable<string>> GetRolesAsync();
    }
}