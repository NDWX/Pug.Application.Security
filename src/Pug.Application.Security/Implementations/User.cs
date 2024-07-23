using System.Collections.Generic;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Pug.Application.Security
{
	public class User : IUser
	{
		private readonly IUserRoleProvider _userRoleProvider;
		private readonly IAuthorizationProvider _userSecurity;

		public User(IPrincipalIdentity credentials, IUserRoleProvider userRoleProvider,
					IAuthorizationProvider userSecurity)
		{
			Identity = credentials;
			_userRoleProvider = userRoleProvider;
			_userSecurity = userSecurity;
		}

		public bool IsInRole(string role)
		{
			return _userRoleProvider.UserIsInRole(Identity.Identifier, role);
		}

		public bool IsAuthorized(IDictionary<string, string> context, string operation, string objectType,
								string objectName = "", string purpose = "", string domain = null)
		{
			return _userSecurity.UserIsAuthorized(context, this, operation, objectType, objectName, purpose, domain);
		}

		public Task<bool> IsAuthorizedAsync( IDictionary<string, string> context, string operation, string objectType, string objectName = "", string purpose = "", string domain = null )
		{
			return _userSecurity.UserIsAuthorizedAsync(context, this, operation, objectType, objectName, purpose, domain);
		}

		public IEnumerable<string> GetRoles(string domain = null)
		{
			return _userRoleProvider.GetUserRoles(Identity.Identifier, domain);
		}

		public Task<IEnumerable<string>> GetRolesAsync( string domain = null )
		{
			return _userRoleProvider.GetUserRolesAsync(Identity.Identifier, domain);
		}

		public IPrincipalIdentity Identity { get; }

		IIdentity IPrincipal.Identity => Identity;

		public void Dispose()
		{
			Identity.Dispose();
		}
	}
}