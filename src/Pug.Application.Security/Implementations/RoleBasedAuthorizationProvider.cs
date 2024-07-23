using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pug.Application.Security
{
	public abstract class RoleBasedAuthorizationProvider : IAuthorizationProvider
	{
		protected RoleBasedAuthorizationProvider(IUserRoleProvider roleProvider)
		{
			RoleProvider = roleProvider;
		}

		protected IUserRoleProvider RoleProvider { get; }

		public abstract bool UserIsAuthorized(IDictionary<string, string> context, IUser user,
											string operation, string objectType, string objectName = "",
											string purpose = "", string domain = "");

		public abstract Task<bool> UserIsAuthorizedAsync( IDictionary<string, string> context, IUser user, string operation, string objectType,
														string objectName = "", string purpose = "",
														string domain = "" );
	}
}
