// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp

open Yaaf.Helper
open Yaaf.Logging
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.XmlStanzas
open Yaaf.Xmpp.XmlStanzas.Parsing

type IRawUnvalidatedStanzaPlugin =
    inherit IReceivePipelineProvider<Stanza>
type IRawStanzaPlugin =
    inherit IReceivePipelineProvider<Stanza>

type IXmlStanzaService =
    /// Service to queue a stanza, we use the optional func argument to OPT IN to return values (listen for stanzas with the same id)
    abstract QueueStanza : (Async<Stanza> -> Async<unit>) option -> Stanza -> unit
    //abstract UnvalidatedStanzaPluginManager : IRuntimePluginManager<IRawUnvalidatedStanzaPlugin> with get
    //abstract StanzaPluginManager : IRuntimePluginManager<IRawStanzaPlugin> with get
    //abstract UnprocessedStanzaPluginManager : IRuntimePluginManager<IUnprocessedStanzaPlugin> with get
    //abstract ErrorStanzaPluginManager : IRuntimePluginManager<IErrorStanzaPlugin> with get
    // ID service? (for id attribute of xml stanzas)
    abstract GenerateNextId : unit -> string

    /// Triggered when we receive an unhandled error stanza
    [<CLIEvent>]
    abstract UnhandledStanzaException : IEvent<ReceivedStanzaException>

[<AutoOpen>]
module StanzaServiceExtensions =
    type IXmlStanzaService with
        member x.QueueStanzaGeneric res (stanza:Stanza<_>) =
            x.QueueStanza res (stanza.SimpleStanza)
        member x.QueueStanzaGenericReturn stanza =
            let taskSource = new System.Threading.Tasks.TaskCompletionSource<_>()
            x.QueueStanzaGeneric
                (Some 
                    (fun rawStanza -> 
                        async { 
                            try
                                let! resultStanza = rawStanza
                                taskSource.SetResult(resultStanza) 
                            with exn ->
                                taskSource.SetException (exn)
                        } )) stanza
            taskSource.Task

//let Context_LastId = XmppConfigKey.createDef "Yaaf.Xmpp.Handlers.XmlStanzas.Id" 1
//
//type XmppContext with
//    
//    member x.LastId 
//        with get () = x.Get Context_LastId
//        and set (v : int) = x.Set Context_LastId v
//    
//    member x.GenerateNextId() = 
//        let newId = x.LastId
//        x.LastId <- x.LastId + 1
//        let prefix = 
//            if x.Permanent.Config.IsInitializing then "init_"
//            else "pass_"
//        prefix + newId.ToString()

    
type XmlStanzaPlugin(runtimeConfig : IRuntimeConfig, delivery : ILocalDelivery, registrar : IPluginManagerRegistrar, neg : INegotiationService) as x = 
    let ns = runtimeConfig.StreamNamespace
    let stanzaWaitList = new System.Collections.Generic.Dictionary<_, _>()
    let unvalidatedStanzaPluginManager = registrar.CreateManagerFor<IRawUnvalidatedStanzaPlugin>()
    let stanzaPluginManager = registrar.CreateManagerFor<IRawStanzaPlugin>()
    let unhandledStanzaException = Event<_>()
    do 
        registrar.RegisterFor<IXmlPipelinePlugin> (x)
    let queueStanza (onResponse : (Async<Stanza> -> Async<unit>) option) (stanza : Stanza) = 
        if onResponse.IsSome then 
            if stanza.Header.Id.IsNone then failwith "can't wait for result when id of stanza is not set!"
            stanzaWaitList.Add(stanza.Header.Id.Value, onResponse.Value)
        // TODO: add timer for automatic removal?
        // TODO: enforce stanza sematics here?
        //let stanza = 
        //    { stanza with 
        //        Header =
        //            { stanza.Header with 
        //                From = 
        //                    if stanza.Header.From.IsNone then
        //                        Some (JabberId.Parse serverApi.Domain)
        //                    else stanza.Header.From
        //                To = 
        //                    if stanza.Header.To.IsNone then
        //                        Some (context.Permanent.Config }}
        delivery.QueueMessage (createStanzaElement ns stanza)
    let stanzaService = x :> IXmlStanzaService
    let handleElement (stanza : Stanza) =
        async {
            try
                // process unvalidated pipeline, for example error stanzas have to be delivered in this step (because they would throw later)
                let! pipeResult =
                    Pipeline.processManager "UnvalidatedStanzaPipeline"
                        (unvalidatedStanzaPluginManager)
                        stanza
                let stanza = pipeResult.ResultElem
                let processed = ref false
                let errorStanza = 
                    async { return handleStanzaErrors stanza |> validateStanza }
                    |> Log.TraceMe
                    |> Async.StartAsTaskImmediate
                match stanza.Header.Id with
                | None -> ()
                | Some id -> 
                    match stanzaWaitList.TryGetValue(id) with
                    | false, _ -> ()
                    | true, handler -> 
                        let rem = stanzaWaitList.Remove(id)
                        assert rem
                        processed := true
                        Log.Verb(fun () -> L "Executing registered handler for id %s" id)
                        do! handler (errorStanza |> Task.await)
                if not pipeResult.IsHandled && not (!processed) then 
                    try
                        let! stanza = errorStanza |> Task.await
                        
                        let! pipeResult =
                            Pipeline.processManager "StanzaPipline"
                                (stanzaPluginManager)
                                (stanza)
                        let stanza = pipeResult.ResultElem
                        if not pipeResult.IsHandled then 
                            // It was not even routed ...
                            Log.Err(fun () -> L "Received completely unprocessed Stanza: %A" stanza)
                        do! pipeResult.ProcessTask |> Task.await
                    with
                    | :? ReceivedStanzaException as received ->
                        // while this stanza is an error stanza it still could be that we have to just deliver it
                        // Note that the above is now invalid: Such stanzas have to be handled in UnvalidatedStanzaPipeline
                        // (Check for type = 'error')
                        Log.Err(fun () -> L "Received unhandled error stanza: %A" received.RawStanza)
                        unhandledStanzaException.Trigger received
                do! pipeResult.ProcessTask |> Task.await
                Log.Verb(fun () -> L "Stanza was successfully handled!")
            with
            | :? StanzaParseException as parseError -> 
                Log.Warn(fun () -> L "Received stanza which was unable to be parsed: %O" parseError)
                if stanza.Header.Type.IsNone || stanza.Header.Type.Value <> "error" then 
                    // respond with bad-request
                    Log.Info(fun () -> L "Respond with bad-request")
                    let error = StanzaException.createBadRequest stanza
                    let error = error.WithHeader { error.Header with From = Some neg.LocalJid }
                    stanzaService.QueueStanzaGeneric None error
            | :? StanzaException as stanzaExn -> 
                // respond with the thrown exception
                Log.Warn(fun () -> L "Received stanza which made us return an error: %O" stanzaExn)
                stanzaService.QueueStanzaGeneric None stanzaExn.ErrorStanza
            | exn 
                when (match exn with 
                        // this means we should close the stream -> handled in the Runtime
                        | :? StreamErrorException 
                        // This means the other side finished the stream -> handled in the Runtime
                        | :? StreamFinishedException -> false
                        | _ -> true) -> 
                // internal-server-error
                Log.Err(fun () -> L "Received stanza which caused an internal server error: %O" exn)
                let error = StanzaException.createSimpleErrorStanza StanzaErrorType.Cancel StanzaErrorConditon.InternalServerError stanza
                let error = error.WithHeader { error.Header with From = Some runtimeConfig.JabberId }
                stanzaService.QueueStanzaGeneric None error
        } |> Log.TraceMe

    interface IXmlStanzaService with
        member x.QueueStanza res stanza = queueStanza res stanza
        //member x.StanzaPluginManager = stanzaPluginManager
        //member x.UnvalidatedStanzaPluginManager = unvalidatedStanzaPluginManager
        //member x.UnprocessedStanzaPluginManager = unprocessedStanzaPluginManager
        //member x.ErrorStanzaPluginManager = errorStanzaPluginManager
        member x.GenerateNextId () = System.Guid.NewGuid().ToString()
        [<CLIEvent>]
        member x.UnhandledStanzaException = unhandledStanzaException.Publish

    interface IXmppPlugin with
        member x.PluginService = Service.FromInstance<IXmlStanzaService,_> x
        member x.Name = "XmlStanzaPlugin"
    interface IXmlPipelinePlugin with
        member x.StreamOpened () = async.Return ()
        member x.ReceivePipeline = 
            { Pipeline.empty "XmlStanzaPlugin XML Pipeline Handler" with
                HandlerState = 
                    fun info ->
                        let elem = info.Result.Element
                        if isStanzaElement ns elem && (not (neg.IsNegotiationElement elem)) then
                            HandlerState.ExecuteAndHandle
                        else HandlerState.Unhandled
                Process = 
                    fun info ->
                        // processing could take a second
                        Async.StartAsTaskImmediate(handleElement (parseStanzaElementNoError ns info.Result.Element))
            } :> IPipeline<_>
        member x.SendPipeline = Pipeline.emptyPipeline "XmlStanzaPlugin XML Pipeline Handler"
