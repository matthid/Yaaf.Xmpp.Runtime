// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Server
open System
open System.Net
open System.Net.Sockets
open System.Threading.Tasks
open Yaaf.Logging
open Yaaf.Helper
open Yaaf.Xmpp
open Yaaf.Xmpp.XmlStanzas


type RunningHandler = 
  { Client : TcpClient
    Task : Task<unit>
    Connected : DateTime }

open System.Security.Cryptography.X509Certificates

    
type ServerCertificateData =
    | RawCertificate of X509Certificate2
    /// OpenSsl (keyfile, certfile, password)
    | OpenSslFiles of string * string * string
    | OpenSslText of string * string * string
    /// OpenSsl (keydata, certdata, password)
    | OpenSslLoaded of byte[] * byte[] * string with
    member x.Certificate 
        with get () = 
            match x with
            | RawCertificate (cert) -> cert
            | OpenSslFiles (keyfile, certfile, password) ->
                let getTextFromfile file =
                    System.IO.File.ReadAllText file
                OpenSslText(
                    getTextFromfile keyfile,
                    getTextFromfile certfile,
                    password)
                    .Certificate
            | OpenSslText (keyfileText, certfileText, password) ->
                // TODO: check which type of key we have and use the correct PemStringType
                let getBytesFromText text pemType =
                    CryptographyHelpers.Helpers.GetBytesFromPEM(text, pemType)
                OpenSslLoaded(
                    getBytesFromText keyfileText CryptographyHelpers.PemStringType.PrivateKey,
                    getBytesFromText certfileText CryptographyHelpers.PemStringType.Certificate,
                    password)
                    .Certificate
            | OpenSslLoaded (keyBytes, certBytes, password) ->
                let certificate = new X509Certificate2(certBytes, password)
                let prov = CryptographyHelpers.DecodePrivateKeyInfo(keyBytes)
                certificate.PrivateKey <- prov
                certificate
               
               
type IServerRouting = 
    // Resove : LocalJid -> Register -> configModifier -> JidToResolve -> Client
    abstract member Resolve : JabberId -> (IXmppClient -> unit) -> JabberId -> Async<IXmppClient>

type ConnectionFilter =
  //| IsOutGoingServer
  | IsServer
  | IsClient
  | IsComponent
  | Not of ConnectionFilter 
  | And of ConnectionFilter * ConnectionFilter
  | Or of ConnectionFilter * ConnectionFilter
  | Advanced of (IXmppClient -> bool)

type IServerApiConnectionManager = 
    /// Returns an previously registered connection (only outgoing and client connections)
    /// WARNING: Be carefull with the XmppClient instances (possible deadlocks)
    abstract member GetConnections : JabberId -> IXmppClient list
    /// Filter all registered and open Connections and return the resulting sequence.
    abstract member FilterConnections : ConnectionFilter -> IXmppClient seq

    // it is garantueed that ClientNegotiated or ClientDisconnected is triggered at least once for every RegisterIncommingConnection call
    // ClientDisconnected can be called before ClientNegotiated is called

    [<CLIEventAttribute>]
    abstract member ClientNegotiated : IEvent<System.EventHandler<IXmppClient>, IXmppClient>

    [<CLIEventAttribute>]
    abstract member ClientDisconnected : IEvent<System.EventHandler<IXmppClient * exn option>, IXmppClient * exn option>


type IConnectionManager = 
    inherit IServerApiConnectionManager
    [<CLIEventAttribute>]
    abstract member Errors : IEvent<System.EventHandler<exn>,exn>
    //[<CLIEventAttribute>]
    //abstract member ClientTimedOut : IEvent<System.EventHandler<XmppClient>,XmppClient>
    
    /// incomming connection of given type
    abstract member RegisterIncommingConnection : IXmppClient -> unit

    /// outgoing s2s connection
    abstract member RegisterOutgoingConnection : IXmppClient -> unit

    ///// Returns an previously registered connection (only outgoing and client connections)
    //abstract member GetConnections : JabberId -> XmppClient list

    abstract member Shutdown : bool -> Async<unit>

type IStanzaDelivery = 
    /// Tries to deliver the given stanza to its destination
    /// If the destination is another domain it will be routed to this domain. Returns true, if this process was successfull and false if it was not.
    /// If the destination is our domain 
    ///  - BareJid -> Send to the given Bare JabberId (ie to all open connections)
    ///  - FullJid -> Send to the given single connection
    /// Returns true when anything was sent.
    abstract member TryDeliver : JabberId -> Stanza -> Async<bool>

type ConfigType =
    | S2SConfig of bool
    | C2SConfig
    | ComponentConfig of bool

[<Obsolete("Use ConnectionManager.FilterConnections to query components instead")>]
type ConnectedComponent =
  { Name : string option
    Jid : JabberId }

type IServerPlugin = 
    inherit Runtime.IPlugin
    
type IServerApi = 
    //inherit IServerApiService
    abstract Delivery : IStanzaDelivery with get
    /// Returns true when the domain part of the jid equals one of the domains handled by the server.
    abstract IsLocalJid : JabberId -> bool
    abstract ConnectionManager : IServerApiConnectionManager with get
   
    [<Obsolete("Use ConnectionManager.FilterConnections to query components instead")>] 
    abstract ConnectedComponents : ConnectedComponent list with get
    abstract Domain : string with get
    abstract TcpListeners : System.Net.Sockets.TcpListener list with get
    abstract GetGlobalService<'a when 'a : not struct> : unit -> 'a


[<AutoOpen>]
module InterfaceExtensions = 
    type IServerApiConnectionManager with

        /// This api is safe and only enqueues the work, it is safe when you don't depend on the results...
        member x.GetConnectionsSafeStart jid f =
            async {
                let cons = x.GetConnections jid
                let results = System.Collections.Generic.List<_>()
                for i, con in cons |> Seq.mapi(fun i t -> i, t) do
                    let res = 
                        async {
                            if not con.IsClosed then
                                let! data = f con
                                return Some data
                            else 
                                Log.Warn (fun _ -> L "Ignoring stale connection: %s" con.RemoteJid.FullId)
                                return None 
                        } |> Async.StartAsTaskImmediate
                    results.Add(res)
                return results.ToArray()
            } |> Log.TraceMe

    type IServerApi with    
        member x.GetConnectionsSafeStart jid f = 
            x.ConnectionManager.GetConnectionsSafeStart jid f
            
        member x.OnAllSimple (jid : JabberId) (f : IXmppClient -> Async<unit>) = 
            // use BareJid to enumerate ALL connections
            x.GetConnectionsSafeStart jid.BareJid f |> Async.Ignore
            
        member x.OnAllSimpleWithoutSelf (context : IXmppClient) (jid : JabberId) (f : IXmppClient -> Async<unit>) = 
            async {
                // use BareJid to enumerate ALL connections
                do! x.GetConnectionsSafeStart jid.BareJid (fun con -> 
                        if context <> con then f con
                        else async.Return())
                    |> Async.Ignore
            }
        member x.OnAllSimpleWithSelf (context : IXmppClient) (jid : JabberId) (f : IXmppClient -> Async<unit>) = 
            async { 
                // Do myself immediatly
                do! f context
                // others
                do! x.OnAllSimpleWithoutSelf context jid f
            }

        