using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Pug.Application.Security.PrincipalIdentityAccessors.AspNetCore
{
	public class OAuth20IdentityAccessor : IPrincipalIdentityAccessor
	{
		private readonly IHttpContextAccessor _httpContextAccessor;

		private readonly Dictionary<string, string> _claimTypeDictionary = new Dictionary<string, string>()
		{
			["ISS"] = PrincipalAttributeNames.TokenIssuer,
			["SUB"] = PrincipalAttributeNames.TokenSubject,
			["AUD"] = PrincipalAttributeNames.TokenAudience,
			["EXP"] = PrincipalAttributeNames.TokenValidityEnd,
			["NBF"] = PrincipalAttributeNames.TokenValidityStart,
			["IAT"] = PrincipalAttributeNames.TokenIssueTimestamp,
			["JTI"] = PrincipalAttributeNames.TokenIdentifier,
			["SCOPE"] = PrincipalAttributeNames.IdentityServerScope,
			["CLIENT_ID"] = PrincipalAttributeNames.IdentityServerClientIdentifier,
			["AMR"] = PrincipalAttributeNames.AuthenticationType
		};

		public OAuth20IdentityAccessor(IHttpContextAccessor httpContextAccessor)
		{
			this._httpContextAccessor = httpContextAccessor;
		}

		public IPrincipalIdentity GetUserIdentity() => GetPrincipalIdentity();

		public IPrincipalIdentity GetPrincipalIdentity()
		{
			HttpContext httpContext = _httpContextAccessor.HttpContext;

			if (httpContext == null)
				return null;
			
			ClaimsPrincipal principal = httpContext.User;

			if (principal == null)
				return null;

			string client, subject, authenticationType;

			client = principal.Claims.Where(c => c.Type.ToUpper() == "CLIENT_ID")?.FirstOrDefault()?.Value;
			subject = principal.Claims.Where(c => c.Type.ToUpper() == "SUB")?.FirstOrDefault()?.Value;
			authenticationType = principal.Claims.Where(c => c.Type.ToUpper() == "AMR")?.FirstOrDefault()?.Value;

			Dictionary<string, string> attributes = new Dictionary<string, string>();

			foreach ( Claim claim in principal.Claims)
			{
				if (!_claimTypeDictionary.ContainsKey(claim.Type.ToUpper()))
					continue;

				string attributeName = $"oauth20.claims.{_claimTypeDictionary[claim.Type.ToUpper()]}";

				if (attributes.TryGetValue(attributeName, out string value))
				{
					attributes[attributeName] = $"{value};{claim.Value}";
				}
				else
					attributes.Add(attributeName, claim.Value);
			}

			attributes.Add(IdentityAttributeNames.ClientIdentifier, client);

			return new BasicPrincipalIdentity(subject, subject, principal.Identity.IsAuthenticated, authenticationType, attributes);
		}
	}
}