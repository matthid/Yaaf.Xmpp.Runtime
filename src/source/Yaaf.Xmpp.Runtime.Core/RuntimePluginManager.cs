using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Yaaf.DependencyInjection;
using Microsoft.FSharp.Collections;
using System.Collections.Generic;

namespace Yaaf.Xmpp.Runtime {
    /// <summary>
    /// Implementation of the IPluginManager interface
    /// </summary>
    /// <typeparam name="P"></typeparam>
	public class PluginManager<P> : IPluginManager<P> {

		FSharpList<P> plugins = FSharpList<P>.Empty;

        /// <summary>
        /// Default contructor.
        /// </summary>
		public PluginManager ()
		{
		}

        /// <summary>
        /// registers the given plugin
        /// </summary>
        /// <param name="plug"></param>
		protected virtual void RegPlugin (P plug)
		{
			plugins = FSharpList<P>.Cons (plug, plugins);
		}
        /// <summary>
        /// registers the given plugin.
        /// </summary>
        /// <param name="plug"></param>
		public void RegisterPlugin (P plug)
		{
			RegPlugin (plug);
		}
		
        /// <summary>
        /// Returns the current list of plugins.
        /// </summary>
        /// <returns></returns>
		public IEnumerable<P> GetPlugins ()
		{
			return ListModule.ToSeq<P> (plugins);
		}

        /// <summary>
        /// Returns true when there is a plugin which is assignable to the given type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
		public bool HasPluginOf<T> () where T : P
		{
			foreach (var plug in plugins) {
				if (typeof (T).GetTypeInfo().IsAssignableFrom (plug.GetType ().GetTypeInfo())) {
					return true;
				}
			}
			return false;
		}

        /// <summary>
        /// Returns true when the given instance is registered.
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
		public bool HasPlugin (P instance) 
		{
			foreach (var plug in plugins) {
				if (object.Equals(instance, plug)) {
					return true;
				}
			}
			return false;
		}
	}
    /// <summary>
    /// Helper methods for the generic ServicePluginManager class.
    /// </summary>
	public class ServicePluginManager {
        /// <summary>
        /// Registers a sequence of services to the given kernel (by calling kernel.Bind).
        /// </summary>
        /// <param name="kernel"></param>
        /// <param name="services"></param>
        /// <param name="createPluginException"></param>
		public static void RegisterServices (IKernel kernel, IEnumerable<IService> services, Func<string, Exception, Exception> createPluginException)
		{
			if (kernel == null) {
				throw new ArgumentNullException ("kernel");
			}

			if (services != null) {
				foreach (var service in services) {
					if (!service.ServiceType.GetTypeInfo().IsAssignableFrom (service.ServiceInstance.GetType ().GetTypeInfo())) {
						throw createPluginException ("The given service instance can not be assigned to the given service type!", null);
					}
				}
				foreach (var service in services) {
					try {
						kernel.Bind (service.ServiceType).ToConstant (service.ServiceInstance);
					} catch (DependencyException e) {
						throw createPluginException (string.Format ("Could not register plugin: {0}", e.Message), e);
					}
				}
			}
		}
	}
    /// <summary>
    /// A generic implementation of the IServicePluginManager interface.
    /// </summary>
    /// <typeparam name="P"></typeparam>
	public class ServicePluginManager<P> : PluginManager<P>, IServicePluginManager<P> where P : class, IPluginServiceProvider {
		IKernel kernel;
		private Func<string, Exception, Exception> createPluginException;
        /// <summary>
        /// Creates a new ServicePluginManager instance with the given kernel and exceptions.
        /// </summary>
        /// <param name="kernel"></param>
        /// <param name="createPluginException"></param>
		public ServicePluginManager (IKernel kernel, Func<string, Exception, Exception> createPluginException)
			: base ()
		{
			this.kernel = kernel;
			this.createPluginException = createPluginException;
		}
        /// <summary>
        /// Override RegPlugin to register the plugin in the kernel
        /// </summary>
        /// <param name="plug"></param>
		protected override void RegPlugin (P plug)
		{
			ServicePluginManager.RegisterServices (kernel, plug.PluginService, createPluginException);
			base.RegPlugin (plug);
		}

        /// <summary>
        /// Provide the generic RegisterPlugin method which registers by generic type argument.
        /// We simply query the kernel for the instance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
		public void RegisterPlugin<T> () where T : class, P
		{
			try {
				var pluginInstance = kernel.Get<T> ();
				RegPlugin (pluginInstance);
			} catch (DependencyException e) {
				throw createPluginException (string.Format ("Could not register plugin: {0}", e.Message), e);
			}
		}

        /// <summary>
        /// Get the provided Service by simply querying the kernel.
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <returns></returns>
		public U GetPluginService<U> () where U : class
		{
			try {
				return kernel.Get<U> ();
			} catch (DependencyException e) {
				throw createPluginException (string.Format ("Could not get service: {0}", e.Message), e);
			}
		}
	}
}