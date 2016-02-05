// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp

open Yaaf.FSharp.Control
open System
open Mono.System.Xml
open System.Xml.Linq
open System.IO
open Yaaf.Logging
open Yaaf.Logging.AsyncTracing
open Yaaf.Xml
open Yaaf.Xmpp.Runtime
open Yaaf.Helper
open Yaaf.IO

type StreamElement = XElement
type OpenElement = XElement


module StreamHelpers = 
    let guardAsync f = 
        async { 
            try 
                return! f()
            with :? XmlException as xml -> return StreamError.fail XmlStreamError.BadFormat "invalid xml received"
        }
        |> Log.TraceMe
    
    let guard f = guardAsync (fun () -> async.Return(f())) |> Async.RunSynchronously

open StreamHelpers

module StreamData = 
    let private invCulture = System.Globalization.CultureInfo.InvariantCulture
    
    type IParser<'a> = 
        abstract Parse : string -> 'a
        abstract ToString : 'a -> string
    
    let DateTime = 
        { new IParser<System.DateTime> with
              member x.Parse s = DateTime.Parse(s, invCulture)
              member x.ToString d = d.ToUniversalTime().ToString("s", invCulture) + "Z" }
    
    let SimpleBool = 
        { new IParser<bool> with
              member x.Parse s = bool.Parse(s)
              member x.ToString d = d.ToString(invCulture).ToLowerInvariant() }
    
    let Int = 
        { new IParser<int> with
              member x.Parse s = System.Int32.Parse(s, invCulture)
              member x.ToString d = d.ToString(invCulture) }
    let Long = 
        { new IParser<System.Int64> with
              member x.Parse s = System.Int64.Parse(s, invCulture)
              member x.ToString d = d.ToString(invCulture) }
    let ULong = 
        { new IParser<System.UInt64> with
              member x.Parse s = System.UInt64.Parse(s, invCulture)
              member x.ToString d = d.ToString(invCulture) }

module Stream = 
    let streamNS = KnownStreamNamespaces.streamNS
    let clientNS = KnownStreamNamespaces.clientNS
    let serverNS = KnownStreamNamespaces.serverNS

type StreamType = 
    | ClientStream
    | ServerStream
    /// for external components (true means jabber:component:accept)
    | ComponentStream of bool
    
    static member Parse ns = 
        match ns with
        | Equals KnownStreamNamespaces.clientNS -> ClientStream
        | Equals KnownStreamNamespaces.serverNS -> ServerStream
        | Equals KnownStreamNamespaces.componentAcceptNS -> ComponentStream true
        | Equals KnownStreamNamespaces.componentConnectNS -> ComponentStream false
        | _ -> failwith "unknown stream"
    
    member x.StreamNamespace 
        with get () = 
            match x with
            | ClientStream -> KnownStreamNamespaces.clientNS
            | ServerStream -> KnownStreamNamespaces.serverNS
            | ComponentStream isAccept -> 
                if isAccept then KnownStreamNamespaces.componentAcceptNS
                else KnownStreamNamespaces.componentConnectNS
    
    member x.IsServerSide 
        with get (isInitializing) = 
            match x with
            | ClientStream -> not isInitializing
            | ServerStream -> true
            | ComponentStream isAccept -> isInitializing <> isAccept
    
    // accept means the client is initializing
    // accept, isInitializing -> isServer
    // true  ,     true       -> false
    // false ,     true       -> true
    // true  ,     false      -> true
    // false ,     false      -> false
    member x.IsC2sClient 
        with get (isInitializing) = 
            match x with
            | ClientStream -> isInitializing
            | _ -> false
    
    member x.OnClientStream 
        with get () = 
            match x with
            | ClientStream -> true
            | _ -> false
    
    member x.OnServerStream 
        with get () = 
            match x with
            | ServerStream -> true
            | _ -> false
    
    member x.OnComponentStream 
        with get () = 
            match x with
            | ComponentStream _ -> true
            | _ -> false
