using System;
using System.Threading;

namespace Pug.Application.Security
{
	public class SecurityManager : ISecurityManager
	{
		private readonly string _application;
		private readonly AsyncLocal<SecurityContext> _asyncContext;

		public SecurityManager(
			string application, IPrincipalIdentityAccessor sessionUserIdentityAccessor,
			IPrincipalRoleProvider userRoleProvider, IAuthorizationProvider authorizationProvider
		)
		{
			_application = application ?? throw new ArgumentNullException( nameof(application) );
			PrincipalIdentityAccessor = sessionUserIdentityAccessor ??
										throw new ArgumentNullException( nameof(sessionUserIdentityAccessor) );

			PrincipalRoleProvider = userRoleProvider;
			AuthorizationProvider = authorizationProvider;

			_asyncContext = new AsyncLocal<SecurityContext>()
			{
				Value = new SecurityContext()
			};
		}

		[Obsolete(
			"Use SecurityManager(string, IPrincipalIdentityAccessor, IPrincipalRoleProvider, IAuthorizationProvider) instead"
		)]
		public SecurityManager(
			string application, ISessionUserIdentityAccessor sessionUserIdentityAccessor,
			IUserRoleProvider userRoleProvider, IAuthorizationProvider authorizationProvider
		) : this(
			application,
			(IPrincipalIdentityAccessor)sessionUserIdentityAccessor,
			new UserRoleProviderAdapter( userRoleProvider ),
			authorizationProvider
		)
		{
			UserRoleProvider = userRoleProvider;
		}

		internal SecurityContext SecurityContext
		{
			get
			{
				SecurityContext securityContext = _asyncContext.Value;

				if( securityContext is not null )
					return securityContext;

				securityContext = new SecurityContext();

				SecurityContext = securityContext;

				return securityContext;
			}
			private set { _asyncContext.Value = value; }
		}

		[Obsolete( "Use CurrentPrincipal instead" )]
		public IUser CurrentUser
		{
			get
			{
				SecurityContext securityContext = SecurityContext;

				IUser user = SecurityContext.CurrentUser;

				if( user is not null )
					return user;

				IPrincipalIdentity userIdentity = PrincipalIdentityAccessor.GetPrincipalIdentity();

				if( userIdentity is null )
					return null;

				user = new User( userIdentity, PrincipalRoleProvider, AuthorizationProvider );

				securityContext.CurrentUser = user;

				return user;
			}
		}

		public IPrincipal CurrentPrincipal
		{
			get
			{
				SecurityContext securityContext = SecurityContext;

				IPrincipal principal = SecurityContext.CurrentPrincipal;

				if( principal is not null )
					return principal;

				IPrincipalIdentity userIdentity = PrincipalIdentityAccessor.GetPrincipalIdentity();

				if( userIdentity is null )
					return null;

				principal = new Principal( userIdentity, PrincipalRoleProvider, AuthorizationProvider );

				securityContext.CurrentPrincipal = principal;

				return principal;
			}
		}

		protected IPrincipalIdentityAccessor PrincipalIdentityAccessor { get; }

		public IPrincipalRoleProvider PrincipalRoleProvider { get; }

		[Obsolete( "Use PrincipalRoleProvider instead" )]
		public IUserRoleProvider UserRoleProvider { get; }

		public IAuthorizationProvider AuthorizationProvider { get; }
	}
}