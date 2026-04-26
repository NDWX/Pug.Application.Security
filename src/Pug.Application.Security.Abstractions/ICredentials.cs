using System;

namespace Pug.Application.Security
{
	public interface ICredentials : IEquatable<ICredentials>
	{
		string Identifier
		{
			get;
		}
	}
}
