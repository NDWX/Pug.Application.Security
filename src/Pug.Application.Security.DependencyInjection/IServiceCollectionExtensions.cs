using System;
using Microsoft.Extensions.DependencyInjection;

namespace Pug.Application.Security.DependencyInjection
{
	public static class ServiceCollectionExtensions
	{
		[Obsolete( "Use overload with IPrincipalRoleProvider instead")]
		public static IServiceCollection AddSecurityManager(this IServiceCollection services,
															string applicationName,
															IUserRoleProvider userRoleProvider = null,
															IAuthorizationProvider authorizationProvider = null)
		{
			services.AddSingleton<ISecurityManager>(
				servicesProvider => new SecurityManager(
					applicationName,
					servicesProvider.GetService<ISessionUserIdentityAccessor>(),
					userRoleProvider ?? servicesProvider.GetService<IUserRoleProvider>(),
					authorizationProvider ?? servicesProvider.GetService<IAuthorizationProvider>()
				)
			);

			return services;
		}

		public static IServiceCollection AddSecurityManager(this IServiceCollection services,
															string applicationName,
															IPrincipalRoleProvider userRoleProvider = null,
															IAuthorizationProvider authorizationProvider = null)
		{
			services.AddSingleton<ISecurityManager>(
				servicesProvider => new SecurityManager(
					applicationName,
					servicesProvider.GetService<IPrincipalIdentityAccessor>(),
					userRoleProvider ?? servicesProvider.GetService<IPrincipalRoleProvider>(),
					authorizationProvider ?? servicesProvider.GetService<IAuthorizationProvider>()
				)
			);

			return services;
		}
	}
}