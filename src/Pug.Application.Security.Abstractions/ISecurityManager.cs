namespace Pug.Application.Security
{
	/// <summary>
	/// General interface for a security manager.
	/// 
	/// A security manager provides application with  single point of contact for all security purposes.
	/// </summary>
	public interface ISecurityManager
	{
		/// <summary>
		/// Get currently logged in user.
		/// </summary>
		IUser CurrentUser { get; }
	}
}
