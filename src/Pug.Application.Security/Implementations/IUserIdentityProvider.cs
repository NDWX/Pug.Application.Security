namespace Pug.Application.Security
{
	public interface IUserIdentityProvider
	{
		IPrincipalIdentity GetUserIdentity();
	}
}
