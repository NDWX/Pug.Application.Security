using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace Pug.Application.Security.PrincipalIdentityAccessors.AspNetCore.Tests
{
	public class AnonymousPrincipalTests
	{
		[Fact]
		public async Task AnonymousPrincipalHasNoRolesAndRoleProviderIsNotCalled()
		{
			RecordingRoleProvider roleProvider = new RecordingRoleProvider();

			IPrincipal principal = CreateSecurityManager( new ClaimsPrincipal( new ClaimsIdentity() ), roleProvider )
				.CurrentPrincipal;

			Assert.NotNull( principal );
			Assert.False( principal.Identity.IsAuthenticated );
			Assert.Empty( principal.GetRoles() );
			Assert.Empty( await principal.GetRolesAsync() );
			Assert.False( principal.IsInRole( "admin" ) );
			Assert.Equal( 0, roleProvider.Calls );
		}

		[Fact]
		public void AuthenticatedPrincipalRolesComeFromRoleProvider()
		{
			RecordingRoleProvider roleProvider = new RecordingRoleProvider();

			IPrincipal principal = CreateSecurityManager(
					OAuth20IdentityAccessorTests.Authenticated( ("sub", "user-1") ), roleProvider
				)
				.CurrentPrincipal;

			Assert.Equal( new[] { "admin" }, principal.GetRoles() );
			Assert.True( principal.IsInRole( "admin" ) );
			Assert.Equal( 2, roleProvider.Calls );
		}

		private static SecurityManager CreateSecurityManager(
			ClaimsPrincipal principal, IPrincipalRoleProvider roleProvider
		)
		{
			return new SecurityManager(
				"test", OAuth20IdentityAccessorTests.CreateAccessor( principal ), roleProvider, null
			);
		}

		private class RecordingRoleProvider : IPrincipalRoleProvider
		{
			private static readonly string[] Roles = { "admin" };

			public int Calls { get; private set; }

			public bool PrincipalIsInRole( string principal, string role )
			{
				Calls++;
				return role == "admin";
			}

			public Task<bool> PrincipalIsInRoleAsync( string principal, string role ) =>
				Task.FromResult( PrincipalIsInRole( principal, role ) );

			public bool PrincipalIsInRoles( string principal, ICollection<string> roles )
			{
				Calls++;
				return roles.Contains( "admin" );
			}

			public Task<bool> PrincipalIsInRolesAsync( string principal, ICollection<string> roles ) =>
				Task.FromResult( PrincipalIsInRoles( principal, roles ) );

			public IEnumerable<string> GetPrincipalRoles( string principal )
			{
				Calls++;
				return Roles;
			}

			public Task<IEnumerable<string>> GetPrincipalRolesAsync( string principal ) =>
				Task.FromResult( GetPrincipalRoles( principal ) );
		}
	}
}
