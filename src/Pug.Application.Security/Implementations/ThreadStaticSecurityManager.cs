using System;

namespace Pug.Application.Security
{
	public class ThreadStaticSecurityManager : Application.Security.SecurityManager
	{
		[ThreadStatic]
		IUser currentUser = null;

		public ThreadStaticSecurityManager( IUserIdentityProvider userIdentityProvider, IUserRoleProvider userRoleProvider, IAuthorizationProvider authorizationProvider ) 
		: base( userIdentityProvider, userRoleProvider, authorizationProvider )
		{

		}

		protected override IUser GetCurrentUser()
		{
			return currentUser;
		}

		protected override void SetCurrentUser( IUser user )
		{
			currentUser = user;
		}
	}
}