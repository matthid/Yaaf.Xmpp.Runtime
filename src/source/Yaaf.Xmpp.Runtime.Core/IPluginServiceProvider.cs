
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yaaf.Xmpp.Runtime {
    /// <summary>
    /// Describes an service provided by a plugin.
    /// </summary>
	public interface IService {
        /// <summary>
        /// The type of the provided service (could be more generic than GetType() would suggest!)
        /// </summary>
		Type ServiceType { get; }

        /// <summary>
        /// An instance providing the service.
        /// </summary>
		object ServiceInstance { get; }
	}

	internal class ServiceImpl : IService {
		private readonly Type type;
		private readonly object instance;
		public ServiceImpl (Type type, object instance)
		{
			this.type = type;
			this.instance = instance;
		}
    
		public Type ServiceType
		{
			get { return type; }
		}

		public object ServiceInstance
		{
			get { return instance; }
		}
	}
    /// <summary>
    /// Interface for plugins to provide some services
    /// </summary>
	public interface IPluginServiceProvider {
        /// <summary>
        /// The services the plugin provides
        /// </summary>
		IEnumerable<IService> PluginService { get; }
	}
    /// <summary>
    /// Provides some simple (extension-) methods for the IService interface.
    /// </summary>
	public static class Service {
        /// <summary>
        /// Creates a tuple from the given IService instance
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
		public static Tuple<Type, object> AsTuple (this IService service)
		{
			return Tuple.Create (service.ServiceType, service.ServiceInstance);
		}
        /// <summary>
        /// Creates a sequence of tuples from the given IPluginServiceProvider instance.
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
		public static IEnumerable<Tuple<Type, object>> AsTuple (this IPluginServiceProvider service)
		{
			return Enumerable.Select (service.PluginService, s => s.AsTuple ());
		}
        /// <summary>
        /// Creates a new IService isntance from the given instance prodiving the given service.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <returns></returns>
		public static IService Get<TService, T> (T instance) where T : TService
		{
			return (IService) new ServiceImpl (typeof (TService), instance);
		}

        /// <summary>
        /// Creates a sequence of IService containing exactly one element for the given instance (see Get method).
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <returns></returns>
		public static IEnumerable<IService> FromInstance<TService, T> (T instance) where T : TService
		{
			return Enumerable.Repeat (Get<TService, T>(instance), 1);
		}

        /// <summary>
        /// Returns an empty Sequence of IService instances (Helper for plugins which provide no services).
        /// </summary>
		public static IEnumerable<IService> None { get { return Enumerable.Empty<IService>(); } }
	}
		
}
