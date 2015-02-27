// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp

open Yaaf.Helper

open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.OpenHandshake
open Yaaf.Xmpp.XmlStanzas
open Yaaf.Logging

/// Plugin which handles unknown iq stanzas
type UnknownIqResponderPlugin
    (neg : INegotiationService, stanzas : IXmlStanzaService, registrar : IPluginManagerRegistrar, addressing : IAddressingService,
     openInfo : ICoreStreamOpenerService) as x = 
    do
        registrar.RegisterFor<IRawStanzaPlugin> (x)
        
    let shouldAnswerStanza (stanza:IStanza) =
        addressing.IsLocalStanzaMaybeServer stanza &&
            stanza.Header.StanzaType = XmlStanzaType.Iq && stanza.Header.Type <> Some "error" && stanza.Header.Type <> Some "result"

    let answerUnprocessedStanza (localJid : JabberId, remoteJid: JabberId, sendStanza, stanza : IStanza) = 
        // route stanza, open connections
        // We have to answer unknown IQ-requests with error!
        let error = StanzaException.createSimpleErrorStanza StanzaErrorType.Cancel StanzaErrorConditon.ServiceUnavailable stanza
            
        let error = 
            error.WithHeader { error.Header with From = 
                                                     if error.Header.From.IsNone then Some localJid
                                                     else error.Header.From
                                                 To = 
                                                     if error.Header.To.IsNone then Some remoteJid
                                                     else error.Header.To }
        sendStanza error

    interface IXmppPlugin with
        member x.PluginService = Seq.empty
        member x.Name = "XmppClientPlugin"

    interface IRawStanzaPlugin with
        
        member x.ReceivePipeline = 
            { Pipeline.empty "XmppClientPlugin Unprocessed Stanza Pipeline" with
                HandlerState =
                    fun info -> 
                        if shouldAnswerStanza info.Result.Element then HandlerState.ExecuteIfUnhandled 100
                        else HandlerState.Unhandled
                Process =
                    fun info ->
                        async {
                            Log.Warn(fun () -> L "Responding to unknown IQ stanza!")
                            let elem = info.Result.Element
                            let sendStanza stan = stanzas.QueueStanzaGeneric None stan 
                            let remoteId =
                                if neg.NegotiationCompleted 
                                then neg.RemoteJid 
                                else match openInfo.Info.RemoteOpenInfo.From with
                                     | Some f -> f
                                     | None -> 
                                        Log.Err(fun () -> L "Found no remote jid, using 'unknown@yaaf.de'!")
                                        JabberId.Parse "unknown@yaaf.de"
                            answerUnprocessedStanza (neg.LocalJid, remoteId, sendStanza, elem)
                        } |> Log.TraceMe |> Async.StartAsTaskImmediate
            } :> IPipeline<_>
        //member x.SendPipeline = Pipeline.emptyPipeline()
