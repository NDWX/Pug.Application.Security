using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pug.Application.Security
{
	public interface IAuthorizationProvider
	{
		[Obsolete( "Use overload with NounQualifier for resource instead" )]
		bool UserIsAuthorized(IDictionary<string, string> context, IUser user, string operation, DomainObject resource, string purpose = "");
		
		[Obsolete( "Use overload with NounQualifier for resource instead" )]
		Task<bool> UserIsAuthorizedAsync(IDictionary<string, string> context, IUser user, string operation, DomainObject resource, string purpose = "");
		
		bool PrincipalIsAuthorized(IDictionary<string, string> context, IPrincipal principal, string operation, NounQualifier resource, string purpose = "");
		
		Task<bool> PrincipalIsAuthorizedAsync(IDictionary<string, string> context, IPrincipal principal, string operation, NounQualifier resource, string purpose = "");
	}
}
