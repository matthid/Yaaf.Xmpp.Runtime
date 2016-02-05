// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Server

open Yaaf.DependencyInjection

open Yaaf.Logging
open Yaaf.Logging.AsyncTracing

open Yaaf.Xmpp
open Yaaf.Xmpp.Setup
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.Features


#if CSHARP_EXTENSIONS
[<System.Runtime.CompilerServices.Extension>]
#endif
module XmppSetup = 
    /// Sets the corresponsing feature configs from the given Connection informations
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let AddServerInfoClient (setup: ClientSetup, cert: ServerCertificateData option, loginProviders, resourceManager) = 
        let initKernel kernel =
            Kernel.overrideConfig kernel
                (fun (conf:RuntimeConfig) ->
                    { conf with 
                        IsInitializing = false
                        StreamType = StreamType.ClientStream
                    } :> IRuntimeConfig)
                |> ignore
            Kernel.overrideConfig kernel 
                (fun (conf:TlsConfig) -> 
                    match cert with
                    | Some cert -> { conf with Certificate = cert.Certificate } :> ITlsConfig
                    | None -> 
                        Log.Warn(fun () -> "Tls for s2s disabled (no server certificate given)")
                        { conf with EnableTls = false } :> ITlsConfig)
                |> ignore
            
            Kernel.overrideConfig kernel 
                (fun (conf:SaslConfig) -> { conf with ServerMechanism = loginProviders } :> ISaslConfig)
                |> ignore
            
            Kernel.overrideConfig kernel 
                (fun (conf:BindConfig) ->  { conf with ResourceManager = resourceManager } :> IBindConfig)
                |> ignore
        setup
        |> XmppSetup.addHelper initKernel ignore
    
    let addServerInfoClient cert loginProviders resourceManager setup = AddServerInfoClient(setup, cert, loginProviders, resourceManager)
    

    /// Sets the corresponsing feature configs from the given Connection informations
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let AddServerInfoServer (setup: ClientSetup, init, cert:ServerCertificateData option) = 
        let initKernel (kernel : IKernel) =
            Kernel.overrideConfig kernel
                (fun (conf:RuntimeConfig) ->
                    { conf with 
                        IsInitializing = init
                        StreamType = StreamType.ServerStream
                    } :> IRuntimeConfig)
                |> ignore
            Kernel.overrideConfig kernel 
                (fun (conf:TlsConfig) -> 
                    match cert with
                    | Some cert -> { conf with Certificate = cert.Certificate } :> ITlsConfig
                    | None -> 
                        Log.Warn(fun () -> "Tls for s2s disabled (no server certificate given)")
                        { conf with EnableTls = false } :> ITlsConfig)
                |> ignore
        setup
        |> XmppSetup.addHelper initKernel ignore
    let addServerInfoServer init cert setup = AddServerInfoServer(setup, init, cert)
    
    /// Sets the corresponsing feature configs from the given Connection informations
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let AddS2SCore (setup: ClientSetup) = 
        let initRuntime (runtime:XmppRuntime) =
            let mgr = runtime.PluginManager
            mgr.RegisterPlugin<FeaturePlugin>()
            let featureService = mgr.GetPluginService<IStreamFeatureService>()
            let featureMgr = featureService.FeatureManager
            featureMgr.RegisterPlugin<TlsFeature>()
            //featureMgr.RegisterPlugin<DialbackFeature>()
            mgr.RegisterPlugin<XmlStanzaPlugin>()
            mgr.RegisterPlugin<AddressingPlugin>()
            mgr.RegisterPlugin<UnknownIqResponderPlugin>()
            ()
        setup
        |> XmppSetup.addHelper ignore initRuntime
    let addS2SCore = AddS2SCore

    
    /// Sets the corresponsing feature configs from the given Connection informations
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let AddServerInfoComponent (setup: ClientSetup, components) = 
        let initKernel kernel =
            Kernel.overrideConfig kernel
                (fun (conf:RuntimeConfig) ->
                    { conf with 
                        IsInitializing = false
                        StreamType = StreamType.ComponentStream true
                    } :> IRuntimeConfig)
                |> ignore
            
            Kernel.overrideConfig kernel 
                (fun (conf:ComponentsConfig) ->  { conf with Components = components } :> IComponentsConfig)
                |> ignore
        setup
        |> XmppSetup.addHelper initKernel ignore
    let addServerInfoComponent components setup = AddServerInfoComponent(setup, components)

    
    /// Sets the corresponsing feature configs from the given Connection informations
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let AddComponentCore (setup: ClientSetup) = 
        let initRuntime (runtime:XmppRuntime) =
            let mgr = runtime.PluginManager
            mgr.RegisterPlugin<ComponentNegotiationPlugin>()
            mgr.RegisterPlugin<XmlStanzaPlugin>()
            mgr.RegisterPlugin<AddressingPlugin>()
            mgr.RegisterPlugin<UnknownIqResponderPlugin>()
        setup
        |> XmppSetup.addHelper ignore initRuntime
    let addComponentCore = AddComponentCore

    
    /// Sets the corresponsing feature configs from the given Connection informations
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let AddXmppServerPlugin (setup: ClientSetup) = 
        let initRuntime (runtime:XmppRuntime) =
            let mgr = runtime.PluginManager
            mgr.RegisterPlugin<XmppServerDeliveryPlugin>()
            ()
        setup
        |> XmppSetup.addHelper ignore initRuntime
    let addXmppServerPlugin = AddXmppServerPlugin

module XmppServerSetup =
    let CreateDefault(domain) =
        {
            Components = []
            ServerCertificate = None
            Domain = domain
            ClientLoginProviders = []
            Routing = None
            Connections = None
            Delivery = None
            Kernel = SimpleInjectorKernelCreator.CreateKernel()
            ClientConfig = None
            RegisterServerPlugins = ignore
        }
    let CreateFromKernel(domain) kernel =
        {
            Components = []
            ServerCertificate = None
            Domain = domain
            ClientLoginProviders = []
            Routing = None
            Connections = None
            Delivery = None
            Kernel = kernel
            ClientConfig = None
            RegisterServerPlugins = ignore
        }
    let addRouting routing setup = { setup with Routing = Some routing }
    let addConnections mgr setup = { setup with Connections = Some mgr }
    let addDelivery delivery setup = { setup with Delivery = Some delivery }

    let combineConfigurators maybeOld newConfigurator =
        (fun configType oldSetup ->
            let setup =
                match maybeOld with
                | Some old -> old configType oldSetup
                | None -> oldSetup
            newConfigurator configType setup)

    let addServerCore components cert resourceManager loginProviders setup =
        let defaultCoreConfigHelper components cert resourceManager loginProviders =
            (fun configType oldSetup ->
                match configType with
                | ConfigType.C2SConfig ->
                    oldSetup
                    |> XmppSetup.addCoreClient
                    |> XmppSetup.addServerInfoClient cert loginProviders resourceManager
                | ConfigType.S2SConfig init ->
                    oldSetup
                    |> XmppSetup.addServerInfoServer init cert
                    |> XmppSetup.addS2SCore
                | ConfigType.ComponentConfig init ->
                    if init then failwith "Can't handle inits for now"
                    oldSetup
                    |> XmppSetup.addServerInfoComponent components
                    |> XmppSetup.addComponentCore) 
        { setup with 
            Components = components @ setup.Components
            ServerCertificate = cert
            ClientLoginProviders = loginProviders @ setup.ClientLoginProviders
            ClientConfig = Some <| combineConfigurators setup.ClientConfig (defaultCoreConfigHelper components cert resourceManager loginProviders) }
    let addToKnownConfig f changer setup =
        let addHelper =
            (fun configType oldSetup ->
                if f configType then
                    oldSetup
                    |> changer
                else
                    oldSetup)
        { setup with 
            ClientConfig = Some <| combineConfigurators setup.ClientConfig addHelper }
    let registerServerPlugin<'a when 'a :> IServerPlugin and 'a : not struct> setup =
        { setup with 
            RegisterServerPlugins = 
                (fun mgr ->
                    setup.RegisterServerPlugins mgr
                    mgr.RegisterPlugin<'a>()) }
    let addToAllStreams changer setup = addToKnownConfig (fun _ -> true) changer setup
    let addToSingleStream t changer setup = addToKnownConfig (fun configType -> t = configType) changer setup

    let addXmppServerPlugin setup =
        setup
        |> addToAllStreams XmppSetup.addXmppServerPlugin
        
    let addPerUserService setup =
        setup
        |> registerServerPlugin<XmppServerUserServicePlugin>