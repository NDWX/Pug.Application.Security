using System;
using System.Collections.Generic;

namespace Pug.Application.Security
{
	public interface IPrincipalIdentity : IDisposable, System.Security.Principal.IIdentity
	{
		string Identifier
		{
			get;
		}

		IDictionary<string, string> Attributes
		{
			get;
		}
	}
}
