using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Pug.Application.Security.PrincipalIdentityAccessors.AspNetCore.Tests
{
	public class OAuth20IdentityAccessorTests
	{
		private const string EntraTenant = "11111111-1111-1111-1111-111111111111";
		private const string EntraObjectId = "22222222-2222-2222-2222-222222222222";
		private const string EntraPairwiseSubject = "pairwise-subject-for-this-app";
		private const string EntraAppId = "33333333-3333-3333-3333-333333333333";

		private const string ObjectIdentifierClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";
		private const string TenantClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";

		// Entra claims as issued, and as renamed by JwtBearer's default inbound claim mapping
		public static IEnumerable<object[]> EntraClaimTypes()
		{
			yield return new object[] { "sub", "oid", "tid" };
			yield return new object[] { ClaimTypes.NameIdentifier, ObjectIdentifierClaimType, TenantClaimType };
		}

		[Theory]
		[MemberData( nameof(EntraClaimTypes) )]
		public void EntraUserTokenIsUserIdentifiedByObjectId( string subType, string oidType, string tidType )
		{
			IPrincipalIdentity identity = GetIdentity(
				Authenticated(
					(subType, EntraPairwiseSubject), (oidType, EntraObjectId), (tidType, EntraTenant), ("azp", EntraAppId)
				)
			);

			Assert.Equal( OAuth2PrincipalTypes.User, identity.Type );
			Assert.Equal( EntraObjectId, identity.Identifier );
			Assert.True( identity.IsAuthenticated );
			Assert.Equal( EntraAppId, identity.Attributes[IdentityAttributeNames.ClientIdentifier] );
		}

		[Theory]
		[MemberData( nameof(EntraClaimTypes) )]
		public void EntraAppOnlyTokenIsApplicationIdentifiedByObjectId( string subType, string oidType, string tidType )
		{
			IPrincipalIdentity identity = GetIdentity(
				Authenticated(
					(subType, EntraObjectId), (oidType, EntraObjectId), (tidType, EntraTenant), ("azp", EntraAppId)
				)
			);

			Assert.Equal( OAuth2PrincipalTypes.Application, identity.Type );
			Assert.Equal( EntraObjectId, identity.Identifier );
			Assert.Equal( EntraAppId, identity.Attributes[IdentityAttributeNames.ClientIdentifier] );
		}

		[Fact]
		public void EntraV1TokenTakesClientFromAppId()
		{
			IPrincipalIdentity identity = GetIdentity(
				Authenticated(
					("sub", EntraObjectId), ("oid", EntraObjectId), ("tid", EntraTenant), ("appid", EntraAppId)
				)
			);

			Assert.Equal( OAuth2PrincipalTypes.Application, identity.Type );
			Assert.Equal( EntraAppId, identity.Attributes[IdentityAttributeNames.ClientIdentifier] );
		}

		[Fact]
		public void ObjectIdWithoutTenantIsNotTreatedAsEntra()
		{
			IPrincipalIdentity identity = GetIdentity(
				Authenticated( ("sub", "user-1"), ("oid", "something-else") )
			);

			Assert.Equal( OAuth2PrincipalTypes.User, identity.Type );
			Assert.Equal( "user-1", identity.Identifier );
		}

		[Fact]
		public void ClientCredentialsTokenIsApplicationIdentifiedByClientId()
		{
			IPrincipalIdentity identity = GetIdentity( Authenticated( ("client_id", "service-a") ) );

			Assert.Equal( OAuth2PrincipalTypes.Application, identity.Type );
			Assert.Equal( "service-a", identity.Identifier );
			Assert.Equal( "service-a", identity.Attributes[IdentityAttributeNames.ClientIdentifier] );
		}

		[Fact]
		public void UserTokenIsUserIdentifiedBySubject()
		{
			IPrincipalIdentity identity = GetIdentity(
				Authenticated( ("sub", "user-1"), ("client_id", "web-app"), ("amr", "pwd") )
			);

			Assert.Equal( OAuth2PrincipalTypes.User, identity.Type );
			Assert.Equal( "user-1", identity.Identifier );
			Assert.Equal( "pwd", identity.AuthenticationType );
			Assert.Equal( "user-1", identity.Attributes[$"oauth20.claims.{PrincipalAttributeNames.TokenSubject}"] );
		}

		[Fact]
		public void MappedSubjectClaimIsRecognised()
		{
			IPrincipalIdentity identity = GetIdentity( Authenticated( (ClaimTypes.NameIdentifier, "user-1") ) );

			Assert.Equal( OAuth2PrincipalTypes.User, identity.Type );
			Assert.Equal( "user-1", identity.Identifier );
		}

		[Fact]
		public void UnauthenticatedRequestIsAnonymous()
		{
			IPrincipalIdentity identity = GetIdentity( new ClaimsPrincipal( new ClaimsIdentity() ) );

			AssertAnonymous( identity );
		}

		[Fact]
		public void AuthenticatedRequestWithoutSubjectIsAnonymous()
		{
			IPrincipalIdentity identity = GetIdentity( Authenticated( ("name", "someone") ) );

			AssertAnonymous( identity );
		}

		[Fact]
		public void NoHttpContextMeansNoIdentity()
		{
			OAuth20IdentityAccessor accessor = new OAuth20IdentityAccessor( new StubHttpContextAccessor( null ) );

			Assert.Null( accessor.GetPrincipalIdentity() );
		}

		[Fact]
		public void ClaimTypesMatchUnderTurkishCulture()
		{
			CultureInfo original = CultureInfo.CurrentCulture;

			try
			{
				CultureInfo.CurrentCulture = new CultureInfo( "tr-TR" );

				IPrincipalIdentity identity = GetIdentity( Authenticated( ("client_id", "service-a") ) );

				Assert.Equal( OAuth2PrincipalTypes.Application, identity.Type );
				Assert.Equal( "service-a", identity.Identifier );
			}
			finally
			{
				CultureInfo.CurrentCulture = original;
			}
		}

		private static void AssertAnonymous( IPrincipalIdentity identity )
		{
			Assert.NotNull( identity );
			Assert.Equal( OAuth2PrincipalTypes.Anonymous, identity.Type );
			Assert.False( identity.IsAuthenticated );
			Assert.Null( identity.Identifier );
		}

		internal static ClaimsPrincipal Authenticated( params (string Type, string Value)[] claims )
		{
			return new ClaimsPrincipal(
				new ClaimsIdentity( claims.Select( c => new Claim( c.Type, c.Value ) ), "Bearer" )
			);
		}

		internal static IPrincipalIdentity GetIdentity( ClaimsPrincipal principal )
		{
			return CreateAccessor( principal ).GetPrincipalIdentity();
		}

		internal static OAuth20IdentityAccessor CreateAccessor( ClaimsPrincipal principal )
		{
			return new OAuth20IdentityAccessor(
				new StubHttpContextAccessor( new DefaultHttpContext { User = principal } )
			);
		}

		private class StubHttpContextAccessor : IHttpContextAccessor
		{
			public StubHttpContextAccessor( HttpContext httpContext )
			{
				HttpContext = httpContext;
			}

			public HttpContext HttpContext { get; set; }
		}
	}
}
