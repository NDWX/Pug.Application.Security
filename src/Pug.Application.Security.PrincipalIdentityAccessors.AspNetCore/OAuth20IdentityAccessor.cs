using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Pug.Application.Security.PrincipalIdentityAccessors.AspNetCore
{
	public class OAuth20IdentityAccessor : IPrincipalIdentityAccessor
	{
		private readonly IHttpContextAccessor _httpContextAccessor;

		private static readonly Dictionary<string, string> ClaimTypeDictionary =
			new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase )
			{
				["iss"] = PrincipalAttributeNames.TokenIssuer,
				["sub"] = PrincipalAttributeNames.TokenSubject,
				[ClaimTypes.NameIdentifier] = PrincipalAttributeNames.TokenSubject,
				["aud"] = PrincipalAttributeNames.TokenAudience,
				["exp"] = PrincipalAttributeNames.TokenValidityEnd,
				["nbf"] = PrincipalAttributeNames.TokenValidityStart,
				["iat"] = PrincipalAttributeNames.TokenIssueTimestamp,
				["jti"] = PrincipalAttributeNames.TokenIdentifier,
				["scope"] = PrincipalAttributeNames.IdentityServerScope,
				["client_id"] = PrincipalAttributeNames.IdentityServerClientIdentifier,
				["amr"] = PrincipalAttributeNames.AuthenticationType,
				["http://schemas.microsoft.com/claims/authnmethodsreferences"] = PrincipalAttributeNames.AuthenticationType,
				["tid"] = PrincipalAttributeNames.TokenTenant,
				["http://schemas.microsoft.com/identity/claims/tenantid"] = PrincipalAttributeNames.TokenTenant,
				["oid"] = PrincipalAttributeNames.TokenObjectIdentifier,
				["http://schemas.microsoft.com/identity/claims/objectidentifier"] = PrincipalAttributeNames.TokenObjectIdentifier,
				["azp"] = PrincipalAttributeNames.TokenAuthorizedParty
			};

		public OAuth20IdentityAccessor(IHttpContextAccessor httpContextAccessor)
		{
			_httpContextAccessor = httpContextAccessor;
		}

		public IPrincipalIdentity GetUserIdentity() => GetPrincipalIdentity();

		/// <summary>
		/// Returns the identity of the caller of the current HTTP request.
		/// </summary>
		/// <returns>
		/// Null when there is no HTTP request, meaning the caller is internal to the process.
		/// An identity of type <see cref="OAuth2PrincipalTypes.Anonymous"/>, with IsAuthenticated false and no
		/// identifier, when the request is not authenticated or carries no subject.
		/// </returns>
		/// <remarks>
		/// A token carrying both 'tid' and 'oid' is treated as issued by Microsoft Entra ID: its object ID is the
		/// identifier, and it is an application when 'oid' equals 'sub' (app-only token).
		/// Other tokens use 'sub' as the identifier, or 'client_id' when the token has no subject of its own.
		/// </remarks>
		public IPrincipalIdentity GetPrincipalIdentity()
		{
			HttpContext httpContext = _httpContextAccessor.HttpContext;

			if (httpContext == null)
				return null;

			ClaimsPrincipal principal = httpContext.User;

			if (principal == null)
				return null;

			Dictionary<string, string> attributes = GetAttributes(principal);

			if (principal.Identity?.IsAuthenticated != true)
				return CreateAnonymousIdentity(attributes);

			string subject = OAuth20ClaimTypes.FindFirstValue(principal, OAuth20ClaimTypes.Subject);
			string objectIdentifier = OAuth20ClaimTypes.FindFirstValue(principal, OAuth20ClaimTypes.ObjectIdentifier);
			string tenant = OAuth20ClaimTypes.FindFirstValue(principal, OAuth20ClaimTypes.Tenant);
			string authenticationType = OAuth20ClaimTypes.FindFirstValue(principal, OAuth20ClaimTypes.AuthenticationMethod);

			string client;
			string identifier;
			string principalType = OAuth2PrincipalTypes.User;

			if (tenant != null && objectIdentifier != null)
			{
				// Microsoft Entra ID: 'sub' is pairwise per application, 'oid' identifies the principal in the tenant
				client = OAuth20ClaimTypes.FindFirstValue(principal, OAuth20ClaimTypes.EntraClientIdentifier);
				identifier = objectIdentifier;

				if (string.Equals(objectIdentifier, subject, StringComparison.Ordinal))
					principalType = OAuth2PrincipalTypes.Application;
			}
			else
			{
				client = OAuth20ClaimTypes.FindFirstValue(principal, OAuth20ClaimTypes.ClientIdentifier);
				identifier = subject;

				if (client != null && (subject == null || subject == client))
				{
					principalType = OAuth2PrincipalTypes.Application;
					identifier = client;
				}
			}

			if (string.IsNullOrWhiteSpace(identifier))
				return CreateAnonymousIdentity(attributes);

			attributes[IdentityAttributeNames.ClientIdentifier] = client;

			return new BasicPrincipalIdentity(
				principalType,
				identifier, identifier,
				true, authenticationType,
				attributes
			);
		}

		private static Dictionary<string, string> GetAttributes(ClaimsPrincipal principal)
		{
			Dictionary<string, string> attributes = new Dictionary<string, string>();

			foreach (Claim claim in principal.Claims)
			{
				if (!ClaimTypeDictionary.TryGetValue(claim.Type, out string claimName))
					continue;

				string attributeName = $"oauth20.claims.{claimName}";

				if (attributes.TryGetValue(attributeName, out string value))
				{
					attributes[attributeName] = $"{value};{claim.Value}";
				}
				else
					attributes.Add(attributeName, claim.Value);
			}

			return attributes;
		}

		private static IPrincipalIdentity CreateAnonymousIdentity(Dictionary<string, string> attributes)
		{
			return new BasicPrincipalIdentity(
				OAuth2PrincipalTypes.Anonymous,
				null, null,
				false, null,
				attributes
			);
		}
	}
}
