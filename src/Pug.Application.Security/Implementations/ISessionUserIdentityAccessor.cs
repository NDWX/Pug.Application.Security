namespace Pug.Application.Security
{
    public interface ISessionUserIdentityAccessor
    {
        IPrincipalIdentity GetUserIdentity();
    }
}