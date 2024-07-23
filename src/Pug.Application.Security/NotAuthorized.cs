using System;

namespace Pug.Application.Security
{
	public class NotAuthorized : Exception
	{
		public NotAuthorized()
		{
		}

		public NotAuthorized(string message)
			: base(message)
		{
		}

		public NotAuthorized(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
