using System.Collections.Generic;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Pug.Application.Security
{
	public class User : IUser
	{
		private readonly IUserRoleProvider _userRoleProvider;
		private readonly IAuthorizationProvider _authorizationProvider;

		public User(IPrincipalIdentity credentials, IUserRoleProvider userRoleProvider,
					IAuthorizationProvider authorizationProvider)
		{
			Identity = credentials;
			_userRoleProvider = userRoleProvider;
			_authorizationProvider = authorizationProvider;
		}

		public bool IsInRole(string role)
		{
			return _userRoleProvider.UserIsInRole(Identity.Identifier, role);
		}

		public bool IsAuthorized(IDictionary<string, string> context, string operation, DomainObject resource, string purpose = "")
		{
			return _authorizationProvider.UserIsAuthorized(context, this, operation, resource, purpose);
		}

		public Task<bool> IsAuthorizedAsync( IDictionary<string, string> context, string operation, DomainObject resource, string purpose = "" )
		{
			return _authorizationProvider.UserIsAuthorizedAsync(context, this, operation, resource, purpose);
		}

		public IEnumerable<string> GetRoles()
		{
			return _userRoleProvider.GetUserRoles(Identity.Identifier);
		}

		public Task<IEnumerable<string>> GetRolesAsync( )
		{
			return _userRoleProvider.GetUserRolesAsync(Identity.Identifier);
		}

		public IPrincipalIdentity Identity { get; }

		IIdentity IPrincipal.Identity => Identity;

		public void Dispose()
		{
			Identity.Dispose();
		}
	}
}