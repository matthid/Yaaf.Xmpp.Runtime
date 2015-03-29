// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp

open Yaaf.FSharp.Control
open Yaaf.Sasl
open Yaaf.Helper
open Yaaf.IO
open Yaaf.Logging


type AdvancedConnectInfo = 
    { Stream : Yaaf.Xmpp.Runtime.IStreamManager
      // This member is only required for outgoing s2s connections
      RemoteJid : JabberId option
      RemoteHostname : string 
      IsInitializing : bool 
    } with
    static member FromStream hostname stream  =
        {
            Stream = new IOStreamManager(stream)
            RemoteJid = None
            RemoteHostname = hostname
            IsInitializing = true
        }

// Other namespace to ensure that callers don't have to reference Ninject (only when they extend the setup, or make a custom setup)
namespace Yaaf.Xmpp.Setup

open Yaaf.Sasl
open Yaaf.Helper
open Yaaf.IO
open Yaaf.Logging

open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.Features

open Yaaf.DependencyInjection

type internal XmppClientSetup private (kernel : IKernel, init : XmppRuntime -> unit) =
    let kernel = kernel.CreateChild()
    let mutable initRuntime = init
    static member internal Create () = XmppClientSetup(SimpleInjectorKernelCreator.CreateKernel(), ignore)
    static member internal Create (kernel : IKernel) = XmppClientSetup(kernel, ignore)
    static member internal Create (kernel : IKernel, init) = XmppClientSetup(kernel, init)

    member internal x.Kernel = kernel
    member internal x.InitRuntime runtime =
        initRuntime runtime

    member internal x.AddHelper (updateKernel, addInitRuntime) =
        //let kernel = new Ninject.Extensions.ChildKernel.ChildKernel(kernel, kernelSettings) :> IKernel
        updateKernel kernel
        let oldInit = initRuntime
        initRuntime <-
            fun runtime ->
                oldInit runtime
                addInitRuntime runtime
        x

namespace Yaaf.Xmpp

open Yaaf.FSharp.Control
open Yaaf.FSharp.Functional

open Yaaf.DependencyInjection

open Yaaf.Sasl
open Yaaf.Helper
open Yaaf.IO
open Yaaf.Logging

open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.Features
open Yaaf.Xmpp.Setup
// simple wrapper. This ensures that callers don't have to reference the Ninject assembly
// Note that this class is mutable and all methods change the current instance and return it (builder pattern)
// The main reason why ClientSetup and XmppClientSetup are mutable is because otherwise we have to create new ChildKernels for every change!
type ClientSetup internal (setup : XmppClientSetup) =
    member internal x.Setup = setup

#if CSHARP_EXTENSIONS
[<System.Runtime.CompilerServices.Extension>]
#endif
module XmppSetup =
    let CreateSetup() = ClientSetup( XmppClientSetup.Create() )
    let CreateSetupFromKernel(kernel) = ClientSetup( XmppClientSetup.Create(kernel) )
    let CreateSetupFromInit(kernel, init) = ClientSetup( XmppClientSetup.Create(kernel, init) )
    let safeChild (setup:ClientSetup) =
        let child = setup.Setup.Kernel.CreateChild()
        ClientSetup( XmppClientSetup.Create(child, setup.Setup.InitRuntime) )
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let AddHelper (setup: ClientSetup, updateKernel, addInitRuntime) = setup.Setup.AddHelper(updateKernel, addInitRuntime) |> konst setup
    let addHelper updateKernel addInitRuntime setup = AddHelper(setup, updateKernel, addInitRuntime)

    /// Helper method to set standard configs
    let inline SetConfig< ^C, ^I when ^C : (static member get_Default: unit-> ^C) and  ^C : (static member OfInterface: ^I -> ^C) and ^I : not struct > (setup, config : ^I) =
        let updateKernel kernel =
            Kernel.overrideConfig< ^I, ^C > kernel (fun (_: ^C) -> config)
                |> ignore
        AddHelper(setup, updateKernel, ignore)

    /// Sets the given IRuntimeConfig to the current configuration
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let SetRuntimeConfig (setup, config:IRuntimeConfig) =
        SetConfig<RuntimeConfig,_> (setup, config)
    let setRuntimeConfig config setup = SetRuntimeConfig(setup, config)

    /// Sets the given IStreamFeatureConfig to the current configuration
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let SetStreamFeatureConfig (setup, config:IStreamFeatureConfig) =
        SetConfig<StreamFeatureConfig,_> (setup, config)
    let setStreamFeatureConfig config setup = SetStreamFeatureConfig(setup, config)
    
    /// Sets the given ITlsConfig to the current configuration
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let SetTlsConfig (setup, config:ITlsConfig) =
        SetConfig<TlsConfig,_> (setup, config)
    let setTlsConfig config setup = SetTlsConfig(setup, config)
    
    /// Sets the given ISaslConfig to the current configuration
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let SetSaslConfig (setup, config:ISaslConfig) =
        SetConfig<SaslConfig,_> (setup, config)
    let setSaslConfig config setup = SetSaslConfig(setup, config)
    
    /// Sets the given IBindConfig to the current configuration
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let SetBindConfig (setup, config:IBindConfig) =
        SetConfig<BindConfig,_> (setup, config)
    let setBindConfig config setup = SetBindConfig(setup, config)

    /// Sets the corresponsing feature configs from the given Connection informations
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let AddConnectInfo (setup: ClientSetup, connectInfo, connectData:AdvancedConnectInfo) = 
        let updateKernel kernel =
            Kernel.overrideConfig kernel
                (fun (conf:RuntimeConfig) ->
                    { conf with 
                        JabberId = connectInfo.LocalJid
                        RemoteJabberId =
                            if connectData.IsInitializing && connectData.RemoteJid.IsNone then
                                Some connectInfo.LocalJid.Domain
                            else
                                connectData.RemoteJid
                    } :> IRuntimeConfig)
                |> ignore
            Kernel.overrideConfig kernel 
                (fun (conf:StreamFeatureConfig) -> conf :> IStreamFeatureConfig)
                |> ignore
            Kernel.overrideConfig kernel 
                (fun (conf:TlsConfig) -> { conf with TlsHostname = connectData.RemoteHostname} :> ITlsConfig)
                |> ignore
            Kernel.overrideConfig kernel 
                (fun (conf:SaslConfig) -> { conf with ClientMechanism = connectInfo.Login @ conf.ClientMechanism} :> ISaslConfig)
                |> ignore
            Kernel.overrideConfig kernel 
                (fun (conf:BindConfig) -> conf :> IBindConfig)
                |> ignore
            kernel.Bind<IStreamManager>().ToConstant(connectData.Stream) |> ignore
        AddHelper(setup, updateKernel, ignore)
    
    let addConnectInfo connectInfo connectData setup = AddConnectInfo(setup, connectInfo, connectData)
    
    /// Adds features and plugins required by Xmpp Core 
    /// (note that the XmppClientPlugin is missing, because a lot of configs are incompatible with it)
#if CSHARP_EXTENSIONS
    [<System.Runtime.CompilerServices.Extension>]
#endif
    let AddCoreClient (setup: ClientSetup) = 
        let setupRuntimeCore (runtime:XmppRuntime) = 
            let mgr = runtime.PluginManager
            mgr.RegisterPlugin<FeaturePlugin>()
            let featureService = mgr.GetPluginService<IStreamFeatureService>()
            let featureMgr = featureService.FeatureManager
            featureMgr.RegisterPlugin<TlsFeature>()
            featureMgr.RegisterPlugin<SaslFeature>()
            featureMgr.RegisterPlugin<BindFeature>()
            mgr.RegisterPlugin<XmlStanzaPlugin>()
            mgr.RegisterPlugin<AddressingPlugin>()
            mgr.RegisterPlugin<UnknownIqResponderPlugin>()
        AddHelper(setup, ignore, setupRuntimeCore)
    let addCoreClient = AddCoreClient
    
    ///// Adds the XmppClientPlugin, which basically handles IQ stanzas (returns errors compatible with Xmpp Core)
    //[<System.Runtime.CompilerServices.Extension>]
    //let AddXmppClientPlugin (setup: ClientSetup) =
    //    let setupRuntime (runtime:XmppRuntime) =
    //        let mgr = runtime.PluginManager
    //        mgr.RegisterPlugin<ClientPlugin.UnknownIqResponderPlugin>()
    //    AddHelper(setup, ignore, setupRuntime)
    //let addXmppClientPlugin = AddXmppClientPlugin

/// Represents a more abstract xmpp connection. 
/// Wrapps an XmppRuntime and provides an easy interface to register plugins and start a connection
type XmppClient internal (*internal for unit tests!*) (runtime : XmppRuntime, task : Task<exn option>) = 
    do
        if task = null then
            nullArg "task"

    let mgr = runtime.PluginManager
    let task = task
    // Check that required services are available
    let neg = mgr.GetPluginService<INegotiationService>()
    let runtimeConfig = mgr.GetPluginService<IRuntimeConfig>()
    // These are not available with ComponentNegotiation!
    //let bind = mgr.GetPluginService<IBindService>()
    //let sasl = mgr.GetPluginService<ISaslService>()
    //let tls = mgr.GetPluginService<ITlsService>()
    let negotiationComplete = neg.NegotiationTask
    let connect = neg.ConnectionTask

    let asCancellable timeout computation = 
        let func = new System.Func<_>(fun () -> Async.RunSynchronously(computation, timeout))
        let beginFunc (callback, _) = func.BeginInvoke(callback, ())
        let endFunc ar = func.EndInvoke(ar)
        Async.FromBeginEnd(beginFunc, endFunc)
    
    let getProperty name asy = 
        let time = 5000
        try 
            Async.RunSynchronously((*asCancellable time*) asy, timeout = time)
        with :? System.TimeoutException as t -> raise <| new System.TimeoutException(sprintf "XmppClient Property %s timed out" name, t)
    
    member x.Dispose () = runtime.Dispose()
    interface System.IDisposable with
      member x.Dispose () = x.Dispose()

    member x.Runtime = runtime
    
    member x.Exited = task
    member x.ConnectTask = connect
    
    member x.NegotiationTask = negotiationComplete
            
    member x.IsFaulted 
        with get () = task.IsFaulted || (task.IsCompleted && task.Result.IsSome)
    
    member x.IsCompleted 
        with get () = task.IsCompleted
    
    member x.IsClosed with get () = runtime.IsClosed

    /// Default setup for Xmpp Core (tls, sasl, bind,...)
    member x.CloseConnection(force) = runtime.CloseConnection(force) |> Async.StartAsTaskImmediate
    member x.CloseConnection(force,error) = runtime.CloseConnection(force,error) |> Async.StartAsTaskImmediate
        
    /// This method is mainly used for unit tests
    member x.WriteElem(s : StreamElement) = 
        // TODO: Check if started?
        runtime.QueueMessage s
    
    member x.NegotiationCompleted with get () = neg.NegotiationCompleted
    
    member x.RemoteJid 
        with get () = neg.RemoteJid
    
    member x.LocalJid 
        with get () = neg.LocalJid
    
    interface IXmppClient with
        member x.IsFaulted = x.IsFaulted
        member x.IsCompleted = x.IsCompleted
        member x.IsClosed = x.IsClosed
        member x.NegotiationCompleted = x.NegotiationCompleted
        member x.CloseConnection force = x.CloseConnection force 
        member x.CloseConnection (force, error) = x.CloseConnection(force, error)
        member x.WriteElem e = async.Return (x.WriteElem e)
        member x.StreamType = runtimeConfig.StreamType
        member x.RemoteJid = x.RemoteJid
        member x.LocalJid = x.LocalJid
        member x.NegotiationTask = x.NegotiationTask
        member x.ConnectTask = x.ConnectTask
        member x.Exited = x.Exited
        member x.GetService<'a when 'a : not struct> () = runtime.PluginManager.GetPluginService<'a>()
        
    static member RawConnect (setup : ClientSetup) =
        let setup = setup.Setup
        let kernel = setup.Kernel
        let runtimeConfig = kernel.Get<IRuntimeConfig>()
        let stream = kernel.Get<IStreamManager>()
        let core = 
          match kernel.TryGet<ICoreStreamApi>() with
          | Some c -> c
          | None -> new CoreStreamApi(new OpenHandshake.XmppCoreStreamOpener(runtimeConfig)) :> _

        let runtime = new XmppRuntime(core, runtimeConfig, kernel)
        setup.InitRuntime runtime
        let task = runtime.Connect(stream)
        let client = new XmppClient(runtime, task)
        runtime.PluginManager.RegisterPlugin(
           { new IXmppPlugin with
              member x.Name = "IXmppClient Provider"
              member x.PluginService = Service.FromInstance<IXmppClient, _>(client)
           })
        client

    static member Connect(connectInfo, connectData : AdvancedConnectInfo, setup:ClientSetup) = 
        setup
        |> XmppSetup.addConnectInfo connectInfo connectData
        |> XmppClient.RawConnect
    
    static member Connect(connectInfo, setup : ClientSetup) = 
        async { 
            let! res = Resolve.resolveComplete true connectInfo.LocalJid.Domainpart
            match res with
            | Some(hostname, client, stream) -> 
                let data = 
                    { RemoteHostname = hostname
                      Stream = new IOStreamManager(stream :> System.IO.Stream)
                      RemoteJid = Some connectInfo.LocalJid.Domain
                      IsInitializing = true }
                let xmppClient = XmppClient.Connect(connectInfo, data, setup)
                xmppClient.Exited.ContinueWith(fun (t:Task<exn option>) -> client.Close()) |> ignore
                return xmppClient
            | None -> 
                return failwith "could not resolve hostname or could not connect to resolved address"
        }
        |> Log.TraceMe
    static member Connect(connectInfo) = 
        XmppClient.Connect(connectInfo, XmppSetup.CreateSetup() |> XmppSetup.addCoreClient)