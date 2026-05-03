using System;

namespace Pug.Application.Security
{
    [Obsolete( "Use IPrincipalIdentityAccessor instead")]
    public interface ISessionUserIdentityAccessor : IPrincipalIdentityAccessor
    {
        IPrincipalIdentity GetUserIdentity();
        
    }
}