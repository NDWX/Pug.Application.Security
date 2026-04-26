using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pug.Application.Security
{
	/// <summary>
	/// An IUser object encapsulates a user's identity and provides a single point of entry for all user related security.
	/// </summary>
	public interface IUser : System.Security.Principal.IPrincipal, IDisposable
	{
		new IPrincipalIdentity Identity { get; }

		bool IsAuthorized(IDictionary<string, string> context, string operation, DomainObject resource, string purpose = "");

		Task<bool> IsAuthorizedAsync(IDictionary<string, string> context, string operation, DomainObject resource, string purpose = "");

		IEnumerable<string> GetRoles();
		
		Task<IEnumerable<string>> GetRolesAsync();
	}
}
