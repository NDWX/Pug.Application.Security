using System;
using System.Linq;
using System.Security.Claims;

namespace Pug.Application.Security.PrincipalIdentityAccessors.AspNetCore
{
	/// <summary>
	/// Claim types read by <see cref="OAuth20IdentityAccessor"/>. Each claim is listed with its JWT short name and
	/// the long name that JwtBearer maps it to by default (MapInboundClaims = true).
	/// </summary>
	internal static class OAuth20ClaimTypes
	{
		public static readonly string[] Subject = { "sub", ClaimTypes.NameIdentifier };

		public static readonly string[] ObjectIdentifier =
			{ "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier" };

		public static readonly string[] Tenant = { "tid", "http://schemas.microsoft.com/identity/claims/tenantid" };

		public static readonly string[] ClientIdentifier = { "client_id" };

		// Entra v2 tokens carry the client in 'azp', v1 tokens in 'appid'
		public static readonly string[] EntraClientIdentifier = { "azp", "appid" };

		public static readonly string[] AuthenticationMethod =
			{ "amr", "http://schemas.microsoft.com/claims/authnmethodsreferences" };

		/// <summary>
		/// Returns the first non-blank value of the first claim type, in order, that the principal carries.
		/// Claim types are compared ordinally, ignoring case.
		/// </summary>
		public static string FindFirstValue( ClaimsPrincipal principal, string[] types )
		{
			foreach( string type in types )
			{
				string value = principal.Claims
										.FirstOrDefault(
											c => string.Equals( c.Type, type, StringComparison.OrdinalIgnoreCase ) &&
												!string.IsNullOrWhiteSpace( c.Value )
										)?
										.Value;

				if( value != null )
					return value;
			}

			return null;
		}
	}
}
