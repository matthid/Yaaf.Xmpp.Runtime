// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Server

open Yaaf.FSharp.Control
open System.Collections.Concurrent
open Yaaf.Helper
open Yaaf.Logging
open Yaaf.Logging.AsyncTracing
open Yaaf.Xmpp
open Yaaf.Xmpp.XmlStanzas

type StanzaDelivery(myDomain : string, mgr : IServerApiConnectionManager) = 
    let waitingStanzas = new ConcurrentDictionary<string, ConcurrentQueue<Stanza>>()
    let clientDisconnected (xmppClient : IXmppClient, res : exn option) = ()
    let clientNegotiated (xmppClient : IXmppClient) = ()
    do 
        mgr.ClientDisconnected |> Event.add (clientDisconnected)
        mgr.ClientNegotiated |> Event.add clientNegotiated
    
    member x.TryDeliver jid (stanza : Stanza) = 
        async { 
            
            if jid.Domainpart = myDomain then 
                // local delivery
                if jid.Localpart.IsNone then 
                    // can't deliver, this should have been handled already
                    invalidOp "unable to deliver to myself"
                let! sent = 
                    mgr.GetConnectionsSafeStart jid 
                        (fun client ->
                            let elem = Parsing.createStanzaElement client.StreamType.StreamNamespace stanza
                            client.WriteElem(elem))
                return sent.Length > 0
            else 
                match mgr.GetConnections(jid.Domain) with
                | [] -> return false
                | c :: [] -> 
                    async { 
                        try 
                            let elem = Parsing.createStanzaElement c.StreamType.StreamNamespace stanza
                            do! c.WriteElem elem
                        with exn -> Log.Crit(fun _ -> L "Error while sending stanza: %O" exn)
                    }
                    |> Async.Start
                    return true
                | _ -> return failwith "multiple s2s connections are not supported"
        }
    
    interface IStanzaDelivery with
        member x.TryDeliver jid (stanza) = x.TryDeliver jid (stanza)