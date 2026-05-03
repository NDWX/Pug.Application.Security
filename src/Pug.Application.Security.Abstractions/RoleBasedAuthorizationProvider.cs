using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pug.Application.Security
{
	public abstract class RoleBasedAuthorizationProvider : IAuthorizationProvider
	{
		protected RoleBasedAuthorizationProvider( IPrincipalRoleProvider roleProvider )
		{
			RoleProvider = roleProvider;
		}

		protected IPrincipalRoleProvider RoleProvider { get; }

		[Obsolete( "Use overload with NounQualifier for resource instead" )]
		public bool UserIsAuthorized(
			IDictionary<string, string> context, IUser user,
			string operation, DomainObject resource,
			string purpose = ""
		)
		{
			return PrincipalIsAuthorized(
				context,
				user,
				operation,
				new NounQualifier()
					{ Domain = resource.Domain, Type = resource.Object.Type, Identifier = resource.Object.Identifier },
				purpose
			);
		}

		[Obsolete( "Use overload with NounQualifier for resource instead" )]
		public Task<bool> UserIsAuthorizedAsync(
			IDictionary<string, string> context, IUser user, string operation, DomainObject resource,
			string purpose = ""
		)
		{
			return PrincipalIsAuthorizedAsync(
				context,
				user,
				operation,
				new NounQualifier()
					{ Domain = resource.Domain, Type = resource.Object.Type, Identifier = resource.Object.Identifier },
				purpose
			);
		}

		public abstract bool PrincipalIsAuthorized(
			IDictionary<string, string> context, IPrincipal principal, string operation, NounQualifier resource,
			string purpose = ""
		);

		public abstract Task<bool> PrincipalIsAuthorizedAsync(
			IDictionary<string, string> context, IPrincipal principal, string operation, NounQualifier resource,
			string purpose = ""
		);
	}
}
