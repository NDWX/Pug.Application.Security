using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pug.Application.Security
{
	[Obsolete( "Use IPrincipalRoleProvider instead")]
	public interface IUserRoleProvider
	{
		bool UserIsInRole(string user, string role);
		
		Task<bool> UserIsInRoleAsync(string user, string role);

		bool UserIsInRoles(string user, ICollection<string> roles);
		
		Task<bool> UserIsInRolesAsync(string user, ICollection<string> roles);

		IEnumerable<string> GetUserRoles(string user);

		Task<IEnumerable<string>> GetUserRolesAsync(string user);
	}
}
