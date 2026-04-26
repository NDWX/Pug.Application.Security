using System;

namespace Pug.Application.Security
{
	public class InvalidCredentials : Exception
	{
		public InvalidCredentials()
		{
		}

		public InvalidCredentials(string message)
			: base(message)
		{
		}

		public InvalidCredentials(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
