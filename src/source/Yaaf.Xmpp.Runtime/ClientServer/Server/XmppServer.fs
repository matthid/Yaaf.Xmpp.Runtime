// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Server

open Yaaf.DependencyInjection
open Yaaf.FSharp.Control
open Yaaf.Helper
open Yaaf.Logging
open Yaaf.Xmpp
open Yaaf.Xmpp
open Yaaf.Xmpp.Setup
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.Features
open Yaaf.Xmpp.XmlStanzas
type DefaultRouting() =
    interface IServerRouting with
        member x.Resolve localJid register jid = 
            async { // resolve
                let! res = Resolve.resolveComplete false jid.Domainpart
                match res with
                | Some(hostname, client, stream) -> 

                    let data = 
                        { RemoteHostname = ""
                          RemoteJid = Some jid
                          Stream = new IOStreamManager(stream :> System.IO.Stream)
                          IsInitializing = false }

                    let client = 
                        XmppClient.Connect({ LocalJid = localJid
                                             Login = [] }, data, XmppSetup.CreateSetup())
                    register (client :> IXmppClient)
                    return client :> IXmppClient
                //tcpClient <- Some client
                //let config = getConfig()
                //config.TlsHostname <- hostname
                //return! x.Connect(connectInfo, stream)
                | None -> return failwith "could not resolve hostname or could not connect to resolved address"
            } |> Log.TraceMe


type ConfigCreator = ConfigType -> ClientSetup -> ClientSetup
type ServerSetup = 
  internal {  Components : Yaaf.Xmpp.Runtime.ComponentConfig list 
              ServerCertificate : ServerCertificateData option
              Domain : string
              ClientLoginProviders : Yaaf.Sasl.IServerMechanism list
              Routing : IServerRouting option
              Connections : IConnectionManager option
              Delivery : IStanzaDelivery option
              Kernel : IKernel
              ClientConfig : ConfigCreator option
              RegisterServerPlugins : IServicePluginManager<IServerPlugin> -> unit
              } 

/// Represents a simple server implementation
/// This type takes care of connected resources, specified in XMPP.Core
type XmppServer(c : ServerSetup) as this = 

    let kernel = c.Kernel.CreateChild()
    let pluginManager = new ServicePluginManager<IServerPlugin>(kernel, XmppRuntime.CreateErrorHelper) :> IServicePluginManager<IServerPlugin>
    do
        //kernel.Bind<IKernel>().ToConstant(kernel) |> ignore
        kernel.Bind<IServerApi>().ToConstant(this) |> ignore
        kernel.Bind<IServicePluginManager<IServerPlugin>>().ToConstant(pluginManager) |> ignore
        c.RegisterServerPlugins pluginManager

    let createS2sConfig init = c.ClientConfig.Value (ConfigType.S2SConfig init) (XmppSetup.CreateSetupFromKernel kernel)
    let s2sConfig_init = createS2sConfig true
    let s2sConfig_noInit = createS2sConfig false
    let c2sConfig = c.ClientConfig.Value (ConfigType.C2SConfig) (XmppSetup.CreateSetupFromKernel kernel)
    let componentConfig =  c.ClientConfig.Value (ConfigType.ComponentConfig false) (XmppSetup.CreateSetupFromKernel kernel)
    
    let routing = 
        match c.Routing with
        | Some r -> r
        | None -> DefaultRouting() :> IServerRouting
    let connections = 
        match c.Connections with
        | Some r -> r
        | None -> ConnectionManager(c.Domain) :> IConnectionManager
    let delivery =
        match c.Delivery with
        | Some r -> r
        | None -> StanzaDelivery(c.Domain, connections) :> IStanzaDelivery
    
    let errors = Event<System.EventHandler<exn>, _>()
    let stopToken = new System.Threading.CancellationTokenSource()
    let mutable components = []
    do
        connections.Errors |> Event.add (fun e -> errors.Trigger(this, e))
        connections.ClientNegotiated 
        |> Event.add (fun client -> 
            match client.StreamType with
            | ComponentStream _ -> 
                components <- { Name = None; Jid = client.RemoteJid } :: components
            | _ -> ())
        connections.ClientDisconnected
        |> Event.add (fun (client, reason) ->
            match client.StreamType with
            | ComponentStream _ -> 
                components <- components |> List.filter (fun cp -> cp.Jid.FullId <> client.RemoteJid.FullId)
            | _ -> ())
    
    [<CLIEventAttribute>]
    member x.Errors = errors.Publish
    
    member x.Delivery with get () = delivery
    member x.Domain with get () = c.Domain
    member x.Components with get () = components
    member x.ConnectionManager with get () = connections :> IServerApiConnectionManager
    member x.PluginManager = pluginManager
    
    interface IServerApi with
        member x.Delivery with get () = x.Delivery
        member x.IsLocalJid jid = jid.Domainpart = x.Domain
        member x.ConnectedComponents = x.Components
        member x.TcpListeners = []
        member x.ConnectionManager with get () = x.ConnectionManager
        member x.Domain with get () = x.Domain
        member x.GetGlobalService<'a> () = kernel.Get<'a>()

    member x.ClientConnected (*createClient:_ -> IXmppClient*) streamType stream = 
        async { 
            Log.Verb(fun _ -> L "Establishing XMPP Connection")
            //let configModifier = 
            let setup =
                match streamType with
                | ServerStream -> s2sConfig_noInit
                | ClientStream -> c2sConfig
                | ComponentStream true -> componentConfig
                | _ -> failwith "jabber:component:connect connections are currently not supported!"
            
            //let xmppClient = createClient (configModifier)
            // we don't need to login as we are the receiving side, and we don't know the remote jid jet
            let data = 
                { RemoteHostname = ""
                  RemoteJid = None
                  Stream = stream
                  IsInitializing = false }
            Log.Verb(fun _ -> L "Delegate Connection process to XmppClient")
            
            let xmppClient = XmppClient.Connect({ LocalJid = JabberId.Parse c.Domain
                                                  Login = [] }, data, XmppSetup.safeChild setup)
            connections.RegisterIncommingConnection(xmppClient)
        }
        |> Log.TraceMe
    member x.CancelToken = stopToken.Token
    member x.Shutdown(force) = 
        // stop all listeners
        Log.Warn(fun _ -> L "SHUTTING DOWN SERVER")
        Log.Info(fun _ -> L "closing listeners...")
        stopToken.Cancel()
        async { 
            // stop all connections
            Log.Info(fun _ -> L "stopping connections (force: %A)..." force)
            return! connections.Shutdown force } |> Log.TraceMe

open System.Runtime.CompilerServices
[<Extension>]
type XmppServerExtensions = 
    [<Extension>]
    static member StartListen (x:XmppServer, streamType : StreamType, endpoint : System.Net.IPEndPoint) = 
        if x.CancelToken.IsCancellationRequested then failwith "Server already shut down!"
        match streamType with
        | ComponentStream false -> failwith "can't wait for jabber:component:connect connections (the server has to activly connect!)"
        | _ -> ()
        Log.Verb(fun _ -> L "Start Listen on %A for %A connections" endpoint streamType)
        let listenAsync = 
            async { 
                try 
                    let listener = new System.Net.Sockets.TcpListener(endpoint)
                    x.CancelToken.Register(fun _ -> listener.Stop()) |> ignore
                    listener.Start()
                    Log.Info(fun _ -> L "Listening on %A for %A connections" endpoint streamType)
                    let! token = Async.CancellationToken
                    while not token.IsCancellationRequested do
                        let! client = listener.AcceptTcpClientAsync() |> Task.await
                        try 
                            Log.Info(fun _ -> L "Client (type: %A) connected: %A <- %A" streamType client.Client.LocalEndPoint client.Client.RemoteEndPoint)
                            let networkStream = client.GetStream()
                            networkStream.ReadTimeout <- -1
                            networkStream.WriteTimeout <- -1
                            do! x.ClientConnected streamType (IOStreamManager(networkStream:>System.IO.Stream))
                        with exn -> 
                            Log.Err(fun _ -> L "Client couldn't connect: %A" exn)
                            client.Close()
                    Log.Info(fun _ -> L "Finished Listening on %A for %A connections" endpoint streamType)
                with exn -> 
                    Log.Crit(fun _ -> L "Listener Loop failed!: %A" exn)
                    reraisePreserveStackTrace exn
            }
            |> Log.TraceMe
        Async.StartAsTaskImmediate(listenAsync, cancellationToken = x.CancelToken)
