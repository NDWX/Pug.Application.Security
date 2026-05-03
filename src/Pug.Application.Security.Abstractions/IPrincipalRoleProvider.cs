using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pug.Application.Security
{
	public interface IPrincipalRoleProvider
	{
		bool PrincipalIsInRole(string principal, string role);
		
		Task<bool> PrincipalIsInRoleAsync(string principal, string role);

		bool PrincipalIsInRoles(string principal, ICollection<string> roles);
		
		Task<bool> PrincipalIsInRolesAsync(string principal, ICollection<string> roles);

		IEnumerable<string> GetPrincipalRoles(string principal);

		Task<IEnumerable<string>> GetPrincipalRolesAsync(string principal);
	}
}