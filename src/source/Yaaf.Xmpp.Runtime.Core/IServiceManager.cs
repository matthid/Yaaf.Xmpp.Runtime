using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Yaaf.Xmpp {
    /// <summary>
    /// Defines a manager of services, it allows the registration of additional services
    /// </summary>
	public interface IServiceManager {
		/// <summary>
		/// Helper Method to register per-user-services. 
		/// Throws exception if the TService was already registered with another TInstance class.
		/// Note that you can't use the same TService for per-stream and per-user services! (This ensures that be can use both types in constructors)
		/// </summary>
		/// <typeparam name="TService"></typeparam>
		/// <typeparam name="TInstance"></typeparam>
		void RegisterService<TService, TInstance> () where TInstance : TService;
	}
    /// <summary>
    /// This class provides an abstract implementation of the IServiceManager interface by delegating the
    /// generic RegisterService call to a concrete RegisterService method with actuall Type parameters.
    /// This works around some F# limitations with the IServiceManager interface (ie you can't override the generic RegisterService method directly).
    /// </summary>
	public abstract class ServiceManager : IServiceManager {
        /// <summary>
        /// Default constructor.
        /// </summary>
		public ServiceManager ()
		{
		}

        /// <summary>
        /// The concrete RegisterService implementation.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="instance"></param>
		protected abstract void RegisterService (Type service, Type instance);

        /// <summary>
        /// Implementation of the generic RegisterService method from the IServiceManager interface.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TInstance"></typeparam>
		public void RegisterService<TService, TInstance> () where TInstance : TService
		{
			RegisterService (typeof (TService), typeof (TInstance));
		}
	}
}
