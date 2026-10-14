using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Pug.Application.Security
{
	internal class Principal : IPrincipal
	{
		private readonly IPrincipalRoleProvider _userRoleProvider;
		private readonly IAuthorizationProvider _authorizationProvider;

		public Principal(IPrincipalIdentity credentials, IPrincipalRoleProvider userRoleProvider,
					IAuthorizationProvider authorizationProvider)
		{
			Identity = credentials;
			_userRoleProvider = userRoleProvider;
			_authorizationProvider = authorizationProvider;
		}

		// An anonymous identity has no identifier to resolve roles for, so it is never passed to the role provider
		private bool HasIdentifier => Identity.IsAuthenticated && !string.IsNullOrWhiteSpace( Identity.Identifier );

		public bool IsInRole(string role)
		{
			if( !HasIdentifier )
				return false;

			return _userRoleProvider.PrincipalIsInRole(Identity.Identifier, role);
		}

		[Obsolete( "Use overload with NounQualifier for resource instead" )]
		public bool IsAuthorized(IDictionary<string, string> context, string operation, DomainObject resource, string purpose = "")
		{
			return IsAuthorized(
				context,
				operation,
				new NounQualifier()
				{
					Domain = resource.Domain,
					Type = resource.Object.Type,
					Identifier = resource.Object.Identifier
				},
				purpose
			);
		}

		public bool IsAuthorized( IDictionary<string, string> context, string operation, NounQualifier resource, string purpose = "" )
		{
			return _authorizationProvider.PrincipalIsAuthorized(context, this, operation, resource, purpose);
		}

		[Obsolete( "Use overload with NounQualifier for resource instead" )]
		public Task<bool> IsAuthorizedAsync( IDictionary<string, string> context, string operation, DomainObject resource, string purpose = "" )
		{
			return IsAuthorizedAsync(
				context,
				operation,
				new NounQualifier()
				{
					Domain = resource.Domain,
					Type = resource.Object.Type,
					Identifier = resource.Object.Identifier
				},
				purpose
			);
		}

		public async Task<bool> IsAuthorizedAsync( IDictionary<string, string> context, string operation, NounQualifier resource, string purpose = "" )
		{
			return await _authorizationProvider.PrincipalIsAuthorizedAsync(context, this, operation, resource, purpose);
		}

		public IEnumerable<string> GetRoles()
		{
			if( !HasIdentifier )
				return Array.Empty<string>();

			return _userRoleProvider.GetPrincipalRoles(Identity.Identifier);
		}

		public Task<IEnumerable<string>> GetRolesAsync( )
		{
			if( !HasIdentifier )
				return Task.FromResult<IEnumerable<string>>( Array.Empty<string>() );

			return _userRoleProvider.GetPrincipalRolesAsync(Identity.Identifier);
		}

		public IPrincipalIdentity Identity { get; }

		IIdentity System.Security.Principal.IPrincipal.Identity => Identity;

		private void ReleaseUnmanagedResources()
		{
			Identity.Dispose();
		}

		protected virtual void Dispose( bool disposing )
		{
			ReleaseUnmanagedResources();
			
			if( disposing )
			{
				Identity?.Dispose();
			}
		}

		public void Dispose()
		{
			Dispose( true );
			GC.SuppressFinalize( this );
		}

		~Principal()
		{
			Dispose( false );
		}
	}
}