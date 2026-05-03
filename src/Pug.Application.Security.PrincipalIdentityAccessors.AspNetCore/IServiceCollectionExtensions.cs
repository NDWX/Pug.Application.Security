using System;
using Microsoft.Extensions.DependencyInjection;

namespace Pug.Application.Security.PrincipalIdentityAccessors.AspNetCore
{
	public static class ServiceCollectionExtensions
	{
		public static IServiceCollection AddOAuth20IdentityAccessor(this IServiceCollection services)
		{
			services.AddSingleton<IPrincipalIdentityAccessor, OAuth20IdentityAccessor>();

			return services;
		}
	}
}