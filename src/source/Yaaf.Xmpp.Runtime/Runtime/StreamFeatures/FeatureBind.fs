// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Runtime.Features

open FSharpx.Collections
open Yaaf.FSharp.Control
open Yaaf.Helper
open Yaaf.Logging
open Yaaf.Logging.AsyncTracing
open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime

type BindElement = 
    /// Result from server
    | Jid of JabberId
    /// Request generated resource
    | Empty
    // 7.7.1.  Success Case
    /// Resource request to server
    | Resource of string

module ParsingBind = 
    open System.Xml.Linq
    open Yaaf.Xml
    open Yaaf.Xmpp.XmlStanzas
    open Yaaf.Xmpp.XmlStanzas.Parsing
    
    let bindNs = "urn:ietf:params:xml:ns:xmpp-bind"
    
    // Check if this is a bind request -> iq stanza
    let isContentBind (stanza : Stanza) = 
        if stanza.Header.StanzaType <> XmlStanzaType.Iq || stanza.Contents.Children
                                                           |> List.length <> 1 then false
        else 
            let child = stanza.Contents.Children |> Seq.head
            match child.Name.NamespaceName with
            | Equals bindNs -> true
            | _ -> false
    
    let isBindElement ns (elem : XElement) = isGenericStanza ns isContentBind elem
    
    // Stream elements
    let parseContentBind (stanza : Stanza) = 
        if stanza.Header.StanzaType <> XmlStanzaType.Iq || stanza.Contents.Children
                                                           |> List.length <> 1 then failwith "expected bind element"
        else 
            let child = stanza.Contents.Children |> Seq.head
            
            let resultElem = 
                match child.Name.NamespaceName, child.Name.LocalName with
                | Equals bindNs, "bind" -> 
                    let innerChilds = child.Elements() |> Seq.toList
                    if innerChilds
                       |> List.length > 1 then failwith "expected zero or one elements"
                    else 
                        match innerChilds |> Seq.tryHead with
                        | None -> BindElement.Empty
                        | Some child -> 
                            match child.Name.NamespaceName, child.Name.LocalName with
                            | Equals bindNs, "jid" -> Jid(JabberId.Parse child.Value)
                            | Equals bindNs, "resource" -> Resource(child.Value)
                            | _ -> failwith "unknown bind child element"
                | _ -> failwith "expected bind element"
            
            let myValidateFail = StanzaException.badRequestValidateFail stanza
            match resultElem with
            | Jid j -> 
                if stanza.Header.Type.IsNone || stanza.Header.Type.Value <> "result" then myValidateFail "expected result type on stanza for jid bind element"
            | Resource _ | Empty -> 
                if stanza.Header.Type.IsNone || stanza.Header.Type.Value <> "set" then myValidateFail "expected set type on stanza for Resource and Empty bind element"
            resultElem
    
    let parseBindCommand ns (elem : XElement) = // parse something within the "stream"
                                                
        parseGenericStanza ns parseContentBind elem
    
    let createBindContentElement (command : BindElement) = 
        match command with
        | Jid jid -> [ jid.FullId, getXName "jid" bindNs |> getXElem ]
        | Resource r -> [ r, getXName "resource" bindNs |> getXElem ]
        | Empty -> []
        |> List.map (fun (value, elem) -> 
               elem.Value <- value
               elem)
        |> getXElemWithChilds (getXName "bind" bindNs)
    
    let bindContentGenerator = ContentGenerator.SimpleGenerator createBindContentElement
    
    let createBindElement id (command : BindElement) = 
        let cType = 
            match command with
            | Resource _ | Empty -> "set"
            | Jid _ -> "result"
        Stanza<_>.CreateGen bindContentGenerator
            { To = None
              From = None
              Id = Some(id)
              Type = Some(cType)
              StanzaType = XmlStanzaType.Iq } command
    
    // element within "feature" advertisement
    let checkIfBind (elem : XElement) = 
        match elem.Name.NamespaceName, elem.Name.LocalName with
        | (Equals bindNs), "bind" -> Some <| ()
        | _ -> None
    
    let createAnnouncementElement() = getXName "bind" bindNs |> getXElem

/// This interface is required to check if the requested resource is already connected,
/// and to disconnect other resources if required
type IResourceManager = 
    
    /// Check if the given jabberid is already connected
    abstract IsConnected : JabberId -> bool Async
    
    /// Disconnect the given jabberid, so the resource is free again
    abstract Disconnect : JabberId -> unit Async
    
    /// Generate a new jabberid resource string
    abstract GenerateResourceId : JabberId -> JabberId

type IBindConfig =
    abstract UseServerGeneratedResource : bool with get
    abstract ForceServerGeneratedResource : bool with get
    abstract ResourceName : string with get
    abstract ResourceManager : IResourceManager with get

type BindConfig =
    {
        UseServerGeneratedResource : bool
        ForceServerGeneratedResource : bool
        ResourceName : string
        ResourceManager : IResourceManager
    } with
    interface IBindConfig with
        member x.UseServerGeneratedResource = x.UseServerGeneratedResource
        member x.ForceServerGeneratedResource = x.ForceServerGeneratedResource
        member x.ResourceName = x.ResourceName
        member x.ResourceManager = x.ResourceManager
    static member OfInterface (x:IBindConfig) =
        {
            UseServerGeneratedResource = x.UseServerGeneratedResource
            ForceServerGeneratedResource = x.ForceServerGeneratedResource
            ResourceName = x.ResourceName
            ResourceManager = x.ResourceManager
        }
    static member Default =
        {
            UseServerGeneratedResource = true
            ForceServerGeneratedResource = true
            ResourceName = ""
            ResourceManager = Unchecked.defaultof<_>
        }
// Defined in StreamFeaturePlugin
//type IBindService = 
//    abstract BoundJid : JabberId option with get
//    abstract RemoteJid : JabberId with get
//    abstract LocalJid : JabberId with get

open ParsingBind
open Yaaf.Xmpp.XmlStanzas
type BindFeature
    (config : IBindConfig, runtimeConfig : IRuntimeConfig, sasl : ISaslService, coreApi : ICoreStreamApi) = 
    let ns = runtimeConfig.StreamNamespace
    let manager = config.ResourceManager
    do
        if runtimeConfig.IsServerSide then
            if obj.ReferenceEquals(null, manager) then
                Configuration.configFail "config.ResourceManager can not be null!"
    let mutable boundJid = None
    let bindTask = new System.Threading.Tasks.TaskCompletionSource<_>()

    let bindCommandToElem sentId (bind:BindElement) =
        let bindStanza = createBindElement sentId bind
        Parsing.createStanzaElement ns bindStanza

    interface IBindService with 
        member x.BoundJid = boundJid
        member x.BindingTask = bindTask.Task

    interface IStreamFeatureHandler with
        member x.PluginService = Service.FromInstance<IBindService,_> x
        
        member x.GetState(featureList) = 
            let maybeFound = 
                featureList
                |> Seq.choose (fun elem -> checkIfBind elem |> Option.map (fun t -> elem, t))
                |> Seq.tryHead
            match maybeFound with
            | None -> Unavailable
            | Some(elem, info) -> 
                if not sasl.IsRemoteAuthenticated || boundJid.IsSome then NegotiatedOrAnnounceOnly elem
                else Available(true, elem)
            
        //
        /// Used when client selects feature
        member x.InitializeFeature () = 
            async { 
                Log.Verb(fun _ -> "starting bind")
                let xmlStream = coreApi.AbstractStream
                //do! context.XmppStream.WriteStanza()
                let sentId = 
                    // context.GenerateNextId()
                    System.Guid.NewGuid().ToString()

                // let bindCommand = Parsing.createBindElement sentId BindElement.Empty
                //do! xmlStream.Write (Parsing.createStanzaElement ns (Stanza.ToRawStanza bindContentGenerator bindCommand))
                do! xmlStream.Write (bindCommandToElem sentId BindElement.Empty)
                let! elem = xmlStream.ReadElement()
                let result = parseBindCommand ns elem
                if result.Header.Id.IsNone || sentId <> result.Header.Id.Value then failwith "expected the result to have the same id"
                match result.Data with
                | Jid jid -> 
                    // 7.3.2.  Restart: must not restart!
                    //let newConfig = config.Clone()
                    //newConfig.JabberId <- jid
                    //do! OpenHandshake.doHandshake newConfig context (context.CoreStream, context.RemoteOpenInfo.From)
                    boundJid <- Some jid
                    bindTask.SetResult jid
                    //do! context.Events.TriggerResourceBound jid
                    //context.ReadFeatures <- false
                    //do! context.Events.TriggerNegotiationProcess()
                | _ -> failwith "expected jid from server"
                //failwith "not implemented"
                Log.Verb(fun _ -> "finished bind")
                return ()
            }
            
        // Server
        // Features can disable itself by returning None
        member x.CreateAnnounceFeatureElement() = 
            // 7.4.  Advertising Support: The server MUST NOT include the resource binding stream feature until after the client has authenticated, typically by means of successful SASL negotiation. 
            if not sasl.IsRemoteAuthenticated || boundJid.IsSome then FeatureInfo.Unavailable
            else FeatureInfo.Available(true, createAnnouncementElement())
            
        /// Used so that the receiving entity can select the selected feature
        member x.IsFeatureSelected elem = isBindElement ns elem
            
        member x.HandleReceivingCommunication (elem) = 
            let xmlStream = coreApi.AbstractStream
            let rec handleCommunicationRecursive (elem, triesLeft) = 
                async { 
                    if triesLeft <= 0 then failwith "not implemented"
                    try 
                        let bindCommand = parseBindCommand ns elem
                        let authJid = 
                            { Localpart = Some sasl.AuthorizedId
                              Domainpart = runtimeConfig.JabberId.Domainpart
                              Resource = None }
                        match bindCommand.Data with
                        | Resource _ // we ignore the request and generate one
                                        // TODO: use the requested jid?
                                        | Empty -> 
                            // generate resource
                            let gen = manager.GenerateResourceId authJid
                            match bindCommand.Header.Id with
                            | Some id -> 
                                do! xmlStream.Write (bindCommandToElem id (BindElement.Jid gen))
                                boundJid <- Some gen
                                bindTask.SetResult gen
                                //do! context.Events.TriggerResourceBound gen
                            | None -> failwith "not implemented"
                        | _ -> failwith "expected request (and not a response) on the server side!"
                        //context.SendFeatures <- false
                        //do! context.Events.TriggerNegotiationProcess()
                        return ()
                    with :? StanzaValidationException as v -> 
                        do! xmlStream.Write (Parsing.createStanzaElement ns v.StanzaToReturn)
                        let! next = xmlStream.ReadElement()
                        return! handleCommunicationRecursive (next, triesLeft - 1)
                }
            handleCommunicationRecursive (elem, 10)
