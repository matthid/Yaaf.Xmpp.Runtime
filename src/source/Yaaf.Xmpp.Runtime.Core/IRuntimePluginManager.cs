using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yaaf.Xmpp.Runtime
{
    /// <summary>
    /// Exception when plugins fail, cannot be created or added.
    /// </summary>
#if FULL_NET
	[Serializable]
#endif
	public class PluginManagerException : Exception {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public PluginManagerException() { }
        /// <summary>
        /// Creates the exception with the given message.
        /// </summary>
        public PluginManagerException(string message) : base(message) { }
        /// <summary>
        /// Creates the exception with the given message and inner exception.
        /// </summary>
        public PluginManagerException(string message, Exception inner) : base(message, inner) { }
#if FULL_NET
        /// <summary>
        /// Constructor provided for serialization.
        /// </summary>
		protected PluginManagerException (
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base (info, context) { }
#endif
    }
    /// <summary>
    /// A plugin manager interface for registering and querying plugins.
    /// </summary>
    /// <typeparam name="TPlugin"></typeparam>
	public interface IPluginManager<TPlugin> {

		/// <summary>
		/// Try to not use this member, because if you do its your responsibility that the plugin is initialized with the proper service instances!
		/// Use the generic version of this method instead to get automatic service resolution!
		/// </summary>
		/// <param name="plugin">the plugin to register.</param>
		void RegisterPlugin (TPlugin plugin);

        /// <summary>
        /// Returns all currently registered plugins.
        /// </summary>
        /// <returns></returns>
		IEnumerable<TPlugin> GetPlugins ();

		/// <summary>
		/// Returns true if the current PluginManager has the plugin specified by the parameter
		/// </summary>
		bool HasPlugin (TPlugin instance);

		/// <summary>
		/// Returns true if the current PluginManager has the plugin specified by the type parameter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		bool HasPluginOf<T> () where T : TPlugin;
	}

	/// <summary>
	/// Most of the Plugins will require a plugin manager (ie to use API provided by other plugins
	/// </summary>
	public interface IServicePluginManager<TPlugin> : IPluginManager<TPlugin> where TPlugin : class, IPluginServiceProvider {
		/// <summary>
		/// Registers a plugin, 't must be the concrete plugin type
		/// If your plugin has custom initialisation parameters just setup the Ninject kernel before calling this method.
		/// NOTE: all Runtime specific services are already setup! (like ILocalDelivery or ICoreRuntimeApi for example, note also that you even can get the IKernel instance!)
		/// </summary>
		void RegisterPlugin<T> () where T : class, TPlugin;


		///// <summary>
		///// Try to not use this member, because if you do its your responsibility that the plugin is initialized with the proper service instances!
		///// Use the generic version of this method instead to get automatic service resolution!
		///// </summary>
		///// <param name="plugin">the plugin to register.</param>
		//[Obsolete ("This member should not be used, if you MUST use it file a bug report! Use RegisterPlugin<T> instead.")]
		//override void RegisterPlugin(TPlugin plugin);

        /// <summary>
        /// Gets a service instance which is provided by an previously registered plugin
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <returns></returns>
		U GetPluginService<U> () where U : class;
	}
    /// <summary>
    /// Extension methods for the Plugin Manager interfaces.
    /// </summary>
	public static class RuntimePluginManagerExtensions {
		//public static Tuple RequirePlugin<T, U> (this IRuntimePluginManager<U> service) where T : U
		//{
		//	return Tuple.Create (service.ServiceType, service.ServiceInstance);
		//}
	}

}
