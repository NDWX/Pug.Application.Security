using System.Runtime.Serialization;

namespace Pug.Application.Security
{
	public record NounQualifier
	{
		[DataMember]
		public string Domain
		{
			get;
#if NET5_0_OR_GREATER
		init;
#else
			set;
#endif
		}

		[DataMember( IsRequired = true )]
		public string Type
		{
			get;
#if NET5_0_OR_GREATER
		init;
#else
			set;
#endif
		}

		[DataMember( IsRequired = true )]
		public string Identifier
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