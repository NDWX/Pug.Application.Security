using System;
using System.Collections.Generic;

namespace Pug.Application.Security
{
	public interface IPrincipalIdentity : ICredentials, IDisposable, System.Security.Principal.IIdentity
	{

		IDictionary<string, string> Attributes
		{
			get;
		}
	}
}
