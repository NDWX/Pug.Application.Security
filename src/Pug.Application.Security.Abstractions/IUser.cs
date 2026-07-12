using System;

namespace Pug.Application.Security
{
	/// <summary>
	/// An IUser object encapsulates a user's identity and provides a single point of entry for all user related security.
	/// </summary>
	[Obsolete( "Use IPrincipal instead" )]
	public interface IUser : IPrincipal
	{
		
	}
}
