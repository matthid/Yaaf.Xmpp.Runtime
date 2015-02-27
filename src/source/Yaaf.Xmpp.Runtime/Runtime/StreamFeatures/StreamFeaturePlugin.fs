// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Runtime.Features

open Yaaf.FSharp.Control
open FSharpx.Collections
open System.Collections.Generic
open System.Xml.Linq
open Yaaf.Helper
open Yaaf.IO
open Yaaf.Logging
open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime

module Parsing = 
    open System.Xml.Linq
    open Yaaf.Xml
    
    type FeatureList = XElement seq
    
    let isFeaturesElement (elem : StreamElement) = 
        match elem.Name.NamespaceName, elem.Name.LocalName with
        | Equals Stream.streamNS, "features" -> true
        | _ -> false
    
    let createFeaturesElement (features : FeatureList) = getXElemWithChilds (getXName "features" Stream.streamNS) features
    
    let parseFeatureList (elem : StreamElement) = 
        let defaultBehaviour() = failwith "expected feature list"
        match elem.Name.NamespaceName with
        | Equals Stream.streamNS -> 
            match elem.Name.LocalName with
            | "features" -> 
                (elem.Elements()
                 |> Seq.toList
                 |> Seq.ofList) : FeatureList
            | _ -> defaultBehaviour()
        | _ -> defaultBehaviour()

open Parsing
open Yaaf.DependencyInjection

type FeatureInfo = 
    /// Available (isRequired, announceElement)
    | Available of bool * XElement
    | NegotiatedOrAnnounceOnly of XElement
    | Unavailable
    
    member x.CanNegotiate 
        with get () = 
            match x with
            | Available _ -> true
            | NegotiatedOrAnnounceOnly(_) | Unavailable -> false
    
    member x.Element 
        with get () = 
            match x with
            | Available(_, elem) | NegotiatedOrAnnounceOnly(elem) -> Some elem
            | Unavailable -> None
    
    member x.IsMandatory 
        with get () = 
            match x with
            | Available(mand, _) -> mand
            | NegotiatedOrAnnounceOnly(_) | Unavailable -> false

/// Note that the context instance can change over time (whenever a new stream is instanciated, for example after tls, sasl)
type IStreamFeatureHandler =
    inherit IPluginServiceProvider

    /// To check the state of the server feature, returns the state and the element describing it
    abstract GetState : FeatureList -> FeatureInfo
    
    /// Used when client selects feature
    abstract InitializeFeature : unit -> Async<unit>
    
    // Server
    /// Features can disable itself by returning None, true means mandatory
    abstract CreateAnnounceFeatureElement : unit -> FeatureInfo
    
    // TODO: Remove the following methods

    /// Used so that the receiving entity can select the selected feature
    abstract IsFeatureSelected : StreamElement -> bool
    
    // Will be done by the corresponding plugin
    abstract HandleReceivingCommunication : StreamElement -> Async<unit>
    
type FeatureSelector = seq<IStreamFeatureHandler * bool> -> IStreamFeatureHandler

type IStreamFeatureService =
    abstract FeatureManager : IServicePluginManager<IStreamFeatureHandler> with get
    abstract OpenStream : bool -> Async<unit>

type IStreamFeatureConfig =
    abstract FeatureSelector : FeatureSelector with get
    
type StreamFeatureConfig =
    {
        FeatureSelector : FeatureSelector
    } with 
    interface IStreamFeatureConfig with
        member x.FeatureSelector = x.FeatureSelector
    static member OfInterface (x:IStreamFeatureConfig) =
        {
            FeatureSelector = x.FeatureSelector
        }
    static member Default =
        let defaultFeatureSelector features = 
            match features |> Seq.tryHead with
            | None -> failwith "no feature found"
            | Some(handler, isMandatory) -> handler
        {
            FeatureSelector = defaultFeatureSelector
        }

type IBindService = 
    abstract BoundJid : JabberId option with get
    abstract BindingTask : System.Threading.Tasks.Task<JabberId> with get

type FeaturePlugin 
    (runtimeConfig: IRuntimeConfig, coreApi : ICoreStreamApi, kernel : IKernel, 
     config : IStreamFeatureConfig, exclusiveSend : IExclusiveSend, openInfo : OpenHandshake.ICoreStreamOpenerService,
     registrar : IPluginManagerRegistrar) as x =
    let featureManager = 
        new ServicePluginManager<IStreamFeatureHandler>(kernel, XmppRuntime.CreateErrorHelper) 
        :> IServicePluginManager<IStreamFeatureHandler>
    let mutable negotiationComplete = false
    let negTask = new System.Threading.Tasks.TaskCompletionSource<_>()

    do
        registrar.RegisterFor<IXmlPipelinePlugin>(x)
        kernel.Bind<IServicePluginManager<IStreamFeatureHandler>>()
            .ToConstant(featureManager)
            |> ignore
    let getFeatureInfos () =
        let handlers = featureManager.GetPlugins()
        let features = 
            handlers
            |> Seq.map (fun handler -> handler, handler.CreateAnnounceFeatureElement())
            |> Seq.cache
        let canNegFeatures = 
            features
            |> Seq.filter (fun (handler, announceState) -> announceState.CanNegotiate)
            |> Seq.toList
        features, canNegFeatures

    let handlePassive () =
        async {
            // Send feature list
            let features, canNegFeatures = getFeatureInfos() 
        
            // order doesn't matter: "4.3.2.  Stream Features Format"
            let featureElements = 
                features
                |> Seq.map (fun (h, a) -> a.Element)
                |> Seq.choose id
                |> List.ofSeq

            do! coreApi.AbstractStream.Write (createFeaturesElement featureElements)
            
            if canNegFeatures.Length = 0 && not negotiationComplete then 
                negotiationComplete <- true
                negTask.SetResult()
        }

    let handleInitializing features =
        async {
            Log.Verb (fun _ -> "handleInitializing started!")
            let handlers = featureManager.GetPlugins()
            // Feature selection
            let found = 
                handlers
                //|> Seq.map snd
                |> Seq.map (fun h -> h, h.GetState(features))
                |> Seq.filter (fun (h, state) -> state <> Unavailable)
                |> Seq.cache
        
            let available = 
                found
                |> Seq.filter (fun (h, state) -> 
                       match state with
                       | Available _ -> true
                       | _ -> false)
                |> Seq.map (function 
                       | (h, Available(isMandatory, elem)) -> h, (isMandatory, elem)
                       | _ -> failwith "there should be no more unavailable item")
                |> Seq.cache
        
            let unknownFeatures = new List<_>(features)
            found |> Seq.iter (fun (h, s) -> 
                         match s with
                         | Available(_, elem) | NegotiatedOrAnnounceOnly elem -> unknownFeatures.Remove elem |> ignore
                         | _ -> failwith "there should be no more unavailable item")
            if unknownFeatures.Count > 0 then 
                // warn
                Log.Warn
                    (fun () -> 
                    L "Some featues were not recognized: \n%s" 
                        (String.concat "\n" (unknownFeatures |> Seq.map (fun t -> t.ToString()))))
            if available |> Seq.isEmpty then 
                negotiationComplete <- true
                negTask.SetResult()
                //return Success // empty features element
            else 
                let selector = config.FeatureSelector
                let selectedFeature : IStreamFeatureHandler = 
                    selector (available |> Seq.map (fun (h, (isMandatory, _)) ->  h, isMandatory))
                do! selectedFeature.InitializeFeature ()
            Log.Verb (fun _ -> "handleInitializing ended!")
            return ()
        }

    let isPluginElement (elem : StreamElement) = 
        let _, canNegFeatures = getFeatureInfos() 
        let getSelected = 
            try 
                canNegFeatures
                |> Seq.map fst
                |> Seq.tryFind (fun h -> h.IsFeatureSelected elem)
            with exn -> 
                Log.Err(fun () -> L "Invalid element received: %A" exn)
                None
        getSelected

    let handleElement (elem:StreamElement) =
        async {
            let getSelected = isPluginElement elem
            match getSelected with
            | Some handler -> 
                do! exclusiveSend.DoWork (fun () -> handler.HandleReceivingCommunication (elem))
            | None -> ()
            
            let _, canNegFeatures = getFeatureInfos() 
            let hasMandatory = canNegFeatures |> Seq.exists (fun (h, info) -> info.IsMandatory)
            if not hasMandatory && not negotiationComplete then 
                // Finished when there are no more mandatory
                negotiationComplete <- true
                negTask.SetResult()
        }

    let mutable bindService = None
    let getBindService () =
        match bindService with
        | Some s -> Some s
        | None ->
            match kernel.TryGet<IBindService>() with
            | Some res ->
                bindService <- Some res
                Some res
            | None ->
                None
    interface IStreamFeatureService with
        member x.FeatureManager = featureManager
        
        member x.OpenStream readFeatures = 
            if runtimeConfig.IsInitializing then async.Return () else 
                handlePassive ()

    interface INegotiationService with
        member x.NegotiationCompleted = negotiationComplete
        // 7.1.  Fundamentals: After a client has bound a resource to the stream, it is referred to as a "connected resource". 
        member x.ConnectionTask = 
            match getBindService() with
            | Some service -> service.BindingTask
            | None -> negTask.Task.ContinueWith(fun (t:System.Threading.Tasks.Task<unit>) -> (x:>INegotiationService).RemoteJid)
        member x.NegotiationTask = negTask.Task

        member x.RemoteJid
            with get() = 
                if runtimeConfig.IsInitializing then
                    runtimeConfig.RemoteJabberId.Value
                else
                    match runtimeConfig.StreamType with
                    | ClientStream ->
                        let bindService = getBindService()
                        if bindService.IsNone then failwith "bindservice is required on c2cstreams"
                        if bindService.Value.BoundJid.IsSome then
                            bindService.Value.BoundJid.Value
                        else 
                            failwith "jabberid not bound (negotiation not finished)!"
                            Log.Info (fun _ -> "RemoteJid requested but no resource was bound!")
                            //if sasl.IsRemoteAuthenticated then
                            //    {   Localpart = Some sasl.AuthorizedId
                            //        Domainpart = runtimeConfig.JabberId.Domainpart
                            //        Resource = None }
                            //else
                            Log.Warn (fun _ -> "RemoteJid requested but we dont use sasl service (jabberid could be invalid!)!!!")
                            openInfo.Info.RemoteOpenInfo.From.Value
                    | ServerStream ->
                        openInfo.Info.RemoteOpenInfo.From.Value
                    | ComponentStream _ ->
                        openInfo.Info.RemoteOpenInfo.To.Value
        member x.LocalJid
            with get() = 
                match runtimeConfig.StreamType with
                | ClientStream ->
                    if runtimeConfig.IsInitializing then
                    
                        let bindService = getBindService()
                        if bindService.IsNone then failwith "bindservice is required on c2cstreams"
                        
                        //if not x.NegotiationComplete then failwith "JabberId only available after negotiation"
                        if bindService.Value.BoundJid.IsSome then
                            bindService.Value.BoundJid.Value
                        else 
                            failwith "no Bound Jid found, are youu accessing LocalJid before negotiation?"
                            //Log.Warn (fun _ -> 
                            //    sprintf "LocalJid property accessed before negotation, falling back to runtimeConfig.Jabberid")
                            //runtimeConfig.JabberId
                    else
                        // no resource binding for the server itself
                        runtimeConfig.JabberId
                | ComponentStream _ 
                | ServerStream -> 
                    // no resource binding for servers
                    runtimeConfig.JabberId // -> failwith "not implemented"
        member x.IsNegotiationElement elem =
            if isFeaturesElement elem then true
            elif (isPluginElement elem).IsSome then true
            else false


    interface IXmppPlugin with
        member x.PluginService = [ Service.Get<IStreamFeatureService,_> x; Service.Get<INegotiationService,_> x ] |> List.toSeq
        member x.Name = "FeaturePlugin"
    interface IXmlPipelinePlugin with
        member x.StreamOpened () = 
            if runtimeConfig.IsInitializing then async.Return () else 
                exclusiveSend.DoWork handlePassive
        member x.ReceivePipeline = 
            { Pipeline.empty  "StreamFeature Raw XML Handler" with
                HandlerState =
                    fun info ->
                         if isFeaturesElement info.Result.Element then HandlerState.ExecuteAndHandle
                         elif (isPluginElement info.Result.Element).IsSome then HandlerState.ExecuteAndHandle
                         else HandlerState.Unhandled
                ProcessSync = 
                    fun info ->
                        async {
                            if isFeaturesElement info.Result.Element then
                                Log.Verb (fun _ -> "Starting to Handle Feature Element!")
                                do! exclusiveSend.DoWork (fun () -> handleInitializing (parseFeatureList info.Result.Element))
                            do! handleElement info.Result.Element
                            return ()
                        }
            } :> IPipeline<_>
        member x.SendPipeline = Pipeline.emptyPipeline "Empty StreamFeature Raw XML Handler"
