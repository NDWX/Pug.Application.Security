using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pug.Application.Security
{
	public interface IAuthorizationProvider
	{
		bool UserIsAuthorized(IDictionary<string, string> context, IUser user, string operation, DomainObject resource, string purpose = "");
		
		Task<bool> UserIsAuthorizedAsync(IDictionary<string, string> context, IUser user, string operation, DomainObject resource, string purpose = "");
	}
}
