namespace Pug.Application.Security.PrincipalIdentityAccessors.AspNetCore
{
	public class PrincipalAttributeNames
	{
		public static readonly string TokenIssuer = "token-issuer";
		public static readonly string TokenSubject = "token-subject";
		public static readonly string TokenAudience = "token-audience";
		public static readonly string TokenValidityStart = "token-validity-start";
		public static readonly string TokenValidityEnd = "token-validity-end";
		public static readonly string TokenIssueTimestamp = "token-issue-timestamp";
		public static readonly string TokenIdentifier = "token-identifier";
		public static readonly string IdentityServerScope = "identityserver-scope";
		public static readonly string IdentityServerClientIdentifier = "identityserver-client-identifier";
		public static readonly string AuthenticationType = "authentication-type";
	}
}