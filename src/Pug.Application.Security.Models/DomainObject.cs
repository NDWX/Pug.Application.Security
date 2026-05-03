using System;

namespace Pug.Application.Security
{
	[Obsolete("Use ResourceQualifier for authorization/permission check, and consider that all object (resource or noun) should be identifiable by its it's unique identifier, and therefore it's domain is implied")]
	public record DomainObject
	{
		public string Domain
		{
			get;
#if NET5_0_OR_GREATER
		init;
#else
			set;
#endif
		}
		
		public Noun Object
		{
			get;
#if NET5_0_OR_GREATER
		init;
#else
			set;
#endif
		}
	}
}