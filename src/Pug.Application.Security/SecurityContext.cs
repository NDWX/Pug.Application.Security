using System;

namespace Pug.Application.Security
{
	internal class SecurityContext
	{
		public IUser CurrentUser { get; set; }
		
		public IPrincipal CurrentPrincipal { get; set; }
	}
}