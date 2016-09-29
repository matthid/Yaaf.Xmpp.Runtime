// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
module Yaaf.Xmpp.Resolve

// See 3.2.  Resolution of Fully Qualified Domain Names
open Yaaf.FSharp.Control
open DnDns
open DnDns.Enums
open DnDns.Query
open DnDns.Records
open DnDns.Security
open System.Net
open System.Net.Sockets
open Yaaf.Helper
open Yaaf.Logging.AsyncTracing

let resolveHostname service port hostname = 
    let request = new DnsQueryRequest()
    let resp = request.Resolve(sprintf "_%s._tcp.%s." service hostname, NsType.SRV, NsClass.INET, ProtocolType.Udp)
    
    let addHostnameAndPort (addresses) port hostname = 
        addresses
        |> AsyncSeq.ofSeq
        |> AsyncSeq.map (fun (a : IPAddress) -> hostname, IPEndPoint(a, port))
    if resp.Answers.Length > 0 then 
        resp.Answers
        |> Seq.map (fun a -> 
               match a with
               | :? SrvRecord as srv -> Some srv
               | _ -> None)
        |> Seq.choose id
        |> Seq.sortBy (fun s -> s.Priority)
        |> AsyncSeq.ofSeq
        |> AsyncSeq.mapAsync (fun s -> async { let! addresses = System.Net.Dns.GetHostAddressesAsync s.HostName 
                                                                |> Task.await
                                               return s.HostName, addresses, s.Port })
        |> AsyncSeq.collect (fun (host, addresses, port) -> addHostnameAndPort addresses (int port) host)
    else // fallback
         
        asyncSeq { let! addresses = System.Net.Dns.GetHostAddressesAsync hostname |> Task.await
                   yield! addHostnameAndPort addresses port hostname }

let resolveHostnameClient hostname = resolveHostname "xmpp-client" 5222 hostname
let resolveHostnameServer hostname = resolveHostname "xmpp-server" 5269 hostname

open Yaaf.Helper
open Yaaf.IO
open Yaaf.Logging

let tryConnectEP (hostname : string, ep : System.Net.IPEndPoint) = 
    async { 
        try 
            let tcpClient = new System.Net.Sockets.TcpClient()
            // https://bugzilla.novell.com/show_bug.cgi?id=606228 ?
            tcpClient.NoDelay <- true
            do! tcpClient.ConnectAsync(ep.Address, ep.Port) |> Task.ofPlainTask
            return Some(hostname, tcpClient, tcpClient.GetStream())
        with exn -> 
            Log.Err(fun () -> sprintf "Unable to connect to %s: %O" hostname exn)
            return None
    }

let connectResolutions = 
    AsyncSeq.chooseAsync tryConnectEP
    >> AsyncSeq.map Some
    >> AsyncSeq.firstOrDefault None 

let resolveComplete client hostname = 
    let resolve = 
        if client then resolveHostnameClient
        else resolveHostnameServer
    resolve hostname |> connectResolutions

let resolveWithPort client hostname port = 
    let resolve = 
        if client then resolveHostname "xmpp-client" port
        else resolveHostname "xmpp-server" port
    resolve hostname |> connectResolutions
    

