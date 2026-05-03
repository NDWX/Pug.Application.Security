namespace Pug.Application.Security
{
	public interface IPrincipalIdentityAccessor
	{
		IPrincipalIdentity GetPrincipalIdentity();
	}
}