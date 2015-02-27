// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Runtime

open Yaaf.FSharp.Control
open FSharpx.Collections
open System.Collections.Generic
open System.Xml.Linq
open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime
open Yaaf.Helper
open Yaaf.IO
open Yaaf.Logging

type ComponentConfig = 
    { Subdomain : string
      Secret : string }
type IComponentsConfig = 
    abstract Components : ComponentConfig list
type ComponentsConfig =
    {
        Components : ComponentConfig list
    } with
    interface IComponentsConfig with
        member x.Components = x.Components
    static member OfInterface (x:IComponentsConfig) =
        {
            Components = x.Components
        }
    static member Default =
        {
            Components = []
        }

module Parsing = 
    open System.Xml.Linq
    open Yaaf.Xml
    
    let isHandshakeElement (elem : StreamElement) = 
        match elem.Name.NamespaceName, elem.Name.LocalName with
        | Equals KnownStreamNamespaces.componentAcceptNS, "handshake" -> true
        | Equals KnownStreamNamespaces.componentConnectNS, "handshake" -> true
        | _ -> false
    
    let createHandshakeElement (ns : string) (sha1 : string option) = 
        getXElemWithChilds (getXName "handshake" ns) [ match sha1 with
                                                       | Some s -> yield s :> obj
                                                       | None -> () ]
    
    let parseHandshake (elem : StreamElement) = 
        let defaultBehaviour() = failwith "expected handshake element"
        match elem.Name.NamespaceName with
        | Equals KnownStreamNamespaces.componentConnectNS | Equals KnownStreamNamespaces.componentAcceptNS -> 
            match elem.Name.LocalName with
            | "handshake" -> elem.Value
            | _ -> defaultBehaviour()
        | _ -> defaultBehaviour()

open Parsing
module ComponentHandshake =
    let calculateSha1 (b : byte array) = 
        use sha1 = new System.Security.Cryptography.SHA1Managed()
        sha1.ComputeHash(b)

    let getBytes (s : string) = System.Text.Encoding.UTF8.GetBytes(s)

    let getHexString (b : byte array) = 
        let sb = new System.Text.StringBuilder()
        for by in b do
            sb.Append(by.ToString("x2")) |> ignore
        sb.ToString()

    let calculateSha1Hex = 
        getBytes
        >> calculateSha1
        >> getHexString

type ComponentNegotiationPlugin
    (runtimeConfig: IRuntimeConfig, coreApi : ICoreStreamApi, openInfo : OpenHandshake.ICoreStreamOpenerService, 
     config : IComponentsConfig, exclusiveSend : IExclusiveSend, registrar : IPluginManagerRegistrar) as x = 
    do
        registrar.RegisterFor<IXmlPipelinePlugin>(x)
    let mutable negotiationComplete = false
    let xmlStream = coreApi.AbstractStream
    let negTask = System.Threading.Tasks.TaskCompletionSource<_>()
    let conTask = negTask.Task.ContinueWith(fun (t:System.Threading.Tasks.Task<unit>) -> (x:>INegotiationService).RemoteJid)
    let negotiateComponent (config : ComponentConfig) = 
        async { 
            let id = openInfo.Info.StreamId
            let secret = config.Secret
            let conc = id + secret
            let sha1 = ComponentHandshake.calculateSha1Hex conc
            if runtimeConfig.IsInitializing then 
                // Appendix G: Notes 5. The handshake value is always supplied by the initiator. 
                do! xmlStream.Write(createHandshakeElement runtimeConfig.StreamType.StreamNamespace (Some sha1))
                // try to receive finishing handshake
                let! result = xmlStream.ReadElement()
                if not <| isHandshakeElement result then StreamError.failf XmlStreamError.BadFormat "expected handshake element"
            else 
                let! result = xmlStream.ReadElement()
                if not <| isHandshakeElement result then StreamError.failf XmlStreamError.BadFormat "expected handshake element"
                let gotSecret = parseHandshake result
                if gotSecret <> sha1 then StreamError.failf XmlStreamError.NotAuthorized "handshake failed: invalid handshake data"
                // all fine, finish handshake
                do! xmlStream.Write(createHandshakeElement runtimeConfig.StreamType.StreamNamespace (None))
            negotiationComplete <- true
            negTask.SetResult()
            return ()
        }

    let streamOpened () = 
        async { 
            let componentConfig = 
                if runtimeConfig.IsInitializing then config.Components.Head
                else 
                    // check opening tag, and find the configuration
                    let info = openInfo.Info.RemoteOpenInfo
                    
                    let domainJid = 
                        match runtimeConfig.StreamType with
                        | ComponentStream true -> // accept stream -> we are the server
                                                  
                            match info.To with
                            | Some d -> d
                            | None -> 
                                StreamError.failf XmlStreamError.ImproperAddressing 
                                    "remote stream header has to contain an to attribute on an jabber:component:accept component stream"
                        | ComponentStream false -> 
                            match info.From with
                            | Some d -> d
                            | None -> 
                                StreamError.failf XmlStreamError.ImproperAddressing 
                                    "remote stream header has to contain an from attribute on an jabber:component:connect component stream"
                        | _ -> Configuration.configFail "unexpected configuration (invalid streamtype with ComponentNegotiation)"
                    
                    let domain = domainJid.FullId
                    let maybeConf = config.Components |> List.tryFind (fun con -> con.Subdomain = domain)
                    match maybeConf with
                    | Some config -> config
                    | None -> StreamError.failf XmlStreamError.HostUnknown "handshake failed: no component found for domain"
            return! negotiateComponent componentConfig
        }
    
    interface INegotiationService with
        member x.IsNegotiationElement elem = false // We hook the stream-opening, so false is completly safe!
        member x.NegotiationCompleted = negotiationComplete
        member x.NegotiationTask = negTask.Task
        member x.ConnectionTask = conTask
        member x.RemoteJid
            with get() = 
                if runtimeConfig.IsInitializing then
                    runtimeConfig.RemoteJabberId.Value
                else
                    match runtimeConfig.StreamType with
                    | ServerStream ->
                        openInfo.Info.RemoteOpenInfo.From.Value
                    | ComponentStream _ ->
                        openInfo.Info.RemoteOpenInfo.To.Value
                    | _ ->
                        Configuration.configFail "unexpected configuration (invalid streamtype with ComponentNegotiation)"

        member x.LocalJid
            with get() = 
                match runtimeConfig.StreamType with
                | ComponentStream _ 
                | ServerStream -> 
                    // no resource binding for servers
                    runtimeConfig.JabberId // -> failwith "not implemented"
                | _ ->
                    Configuration.configFail "unexpected configuration (invalid streamtype with ComponentNegotiation)"


    interface IXmppPlugin with
        member x.PluginService = Service.FromInstance<INegotiationService,_> x
        member x.Name with get () = "ComponentNegotiationPlugin"
    interface IXmlPipelinePlugin with
        member x.StreamOpened () = exclusiveSend.DoWork streamOpened
        member x.ReceivePipeline = Pipeline.emptyPipeline "ComponentNegotiationPlugin empty pipline"
        member x.SendPipeline = Pipeline.emptyPipeline "ComponentNegotiationPlugin empty pipline"
