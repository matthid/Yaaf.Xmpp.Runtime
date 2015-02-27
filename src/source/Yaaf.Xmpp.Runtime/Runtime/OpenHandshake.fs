// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
module Yaaf.Xmpp.Runtime.OpenHandshake

open System
open System.IO
open Yaaf.Xml
open System.Xml.Linq
open System.Threading.Tasks
open Yaaf.FSharp.Control
open Yaaf.Logging
open Yaaf.Helper
open Yaaf.IO
open Yaaf.Xmpp

type StreamOpenInfo = 
    { From : JabberId option
      To : JabberId option
      Id : string option
      /// Note that only Major and Minor are supported
      Version : Version option
      StreamNamespace : string option }
    
    static member Empty = 
        { From = None
          To = None
          Id = None
          Version = None
          StreamNamespace = Some KnownStreamNamespaces.clientNS }
    
    member x.ResolvedVersion 
        with get () = 
            match x.Version with
            | Some v -> v
            | None -> new Version(0, 9)

let fromOpenInfo (info : StreamOpenInfo) = 
    //do! writer.WriteStartDocumentAsync() |> Task.ofPlainTask
    //do! writer.WriteStartElementAsync("stream", "stream", streamNS) |> Task.ofPlainTask
    let openElem = new XElement(XName.Get("stream", KnownStreamNamespaces.streamNS))
    openElem.Add(XAttribute(XName.Get("stream", xmlnsPrefix), KnownStreamNamespaces.streamNS))

    match info.From with
    | Some from -> 
        openElem.Add(XAttribute(XName.Get("from", ""), from.BareId)) 
        // writer.WriteAttributeStringAsync("", "from", "", from.BareId) |> Task.ofPlainTask
    | None -> ()
    match info.Id with
    | Some id -> 
        openElem.Add(XAttribute(XName.Get("id", ""), id)) 
        //writer.WriteAttributeStringAsync("", "id", "", id) |> Task.ofPlainTask
    | None -> ()
    match info.To with
    | Some toAddress -> 
        openElem.Add(XAttribute(XName.Get("to", ""), toAddress.BareId)) 
        //writer.WriteAttributeStringAsync("", "to", "", toAddress.BareId) |> Task.ofPlainTask
    | None -> ()
    match info.Version with
    | Some version -> 
        openElem.Add(XAttribute(XName.Get("version", ""), sprintf "%d.%d" version.Major version.Minor)) 
        //do! writer.WriteAttributeStringAsync("", "version", "", ) 
        //    |> Task.ofPlainTask
    | None -> ()
    /// 4.7.4.  xml:lang, just default to en for now
    //do! writer.WriteAttributeStringAsync("xml", "lang", "", "en") |> Task.ofPlainTask
    match info.StreamNamespace with
    | Some nameSpace -> 
        openElem.Add(XAttribute(XName.Get("xmlns", ""), nameSpace)) 
        //do! writer.WriteAttributeStringAsync("", "xmlns", "", nameSpace) |> Task.ofPlainTask
    | None -> ()
    openElem

let toOpenInfo (streamElem : XElement) = 
    if streamElem.Name.LocalName <> "stream" then 
        StreamError.failf XmlStreamError.BadFormat "expected stream element"
    if streamElem.Name.NamespaceName <> KnownStreamNamespaces.streamNS then 
        StreamError.failf XmlStreamError.InvalidNamespace "expected stream element with %s namespace" KnownStreamNamespaces.streamNS
    let attrs = streamElem.Attributes() |> Seq.map (fun a -> a.Name.LocalName, a.Value) |> Map.ofSeq
    {   To = 
            attrs
            |> Map.tryFind "to"
            |> Option.map JabberId.Parse
        From = 
            attrs
            |> Map.tryFind "from"
            |> Option.map JabberId.Parse
        Id = 
            attrs
            |> Map.tryFind "id"
        Version = 
            attrs
            |> Map.tryFind "version"
            |> Option.map System.Version.Parse
        StreamNamespace = 
            attrs
            |> Map.tryFind "xmlns" }

/// handshake procedur for the initializing entity
let openHandshakeInitializing (config : IRuntimeConfig) (openData : StreamOpenInfo) (stream : IXmlStream) = 
    async { do! stream.WriteStart (fromOpenInfo openData)
            let! serverData = stream.ReadStart()
            return toOpenInfo serverData } |> Log.TraceMe

let openHandshakePassive (config : IRuntimeConfig) (openData : StreamOpenInfo) (stream : IXmlStream) = 
    async { 
        let openWritten = ref false
        //try
        let! clientDataRaw = stream.ReadStart ()
        let clientData = toOpenInfo clientDataRaw
        // NOTE: Xmpp.Core defines we have to send the stream header in any case, so will check the clientData later
        let newOpenData = 
            { openData with Version = 
                                // 4.7.5. version
                                if (openData.ResolvedVersion < clientData.ResolvedVersion) then openData.Version
                                else clientData.Version
                            From = 
                                match config.StreamType with
                                | ComponentStream true -> clientData.To
                                | ComponentStream false -> None
                                | _ -> openData.From
                            To = 
                                match config.StreamType with
                                | ComponentStream true -> None
                                //| ComponentStream false -> clientData.From
                                | _ -> clientData.From }
        let! writer = stream.WriteStart (fromOpenInfo newOpenData)
        openWritten := true
        return clientData
    }
    //with
    //| :? StreamErrorException as error -> 
    //| exn -> 
    |> Log.TraceMe

let openHandshake (config : IRuntimeConfig) = 
    (// TODO: add some common checks (for example: 4.7.  Stream Attributes, 4.8.  XML Namespaces)
     if config.IsInitializing then openHandshakeInitializing
     else openHandshakePassive) config
type OpenHandShakeInfo =
    {
        OpenInfo : StreamOpenInfo
        RemoteOpenInfo : StreamOpenInfo
        StreamId : string
    }
 
let doHandshake (config : IRuntimeConfig) (stream : IXmlStream) = 
    async { 
        //let config = context.Permanent.Config
        if (config.IsInitializing && config.RemoteJabberId.IsNone) then 
            raise <| ConfigurationException "Configuration Error: RemoteJabberId can not be none when we are the initalizing entity"
        //context.RemoveAll()
        //context.StreamBackend <- stream
        let openInfo = 
            { From = 
                  // read http://xmpp.org/extensions/xep-0114.html and you are blown away!
                  match config.StreamType with
                  | ComponentStream true -> None
                  | ComponentStream false -> config.RemoteJabberId
                  | _ -> Some config.JabberId
              To = 
                  // read http://xmpp.org/extensions/xep-0114.html and you are blown away!
                  match config.StreamType with
                  | ComponentStream true -> Some config.JabberId
                  | ComponentStream false -> None
                  | _ -> config.RemoteJabberId
              Id = 
                  // 4.7.3. id 
                  if config.IsInitializing then None
                  else System.Guid.NewGuid().ToString() |> Some
              Version = 
                  // 4.7.5. version
                  Some(System.Version(1, 0, 0, 0))
              StreamNamespace = Some config.StreamType.StreamNamespace }
        let! remoteOpenInfo = openHandshake config openInfo stream
        let streamId =
            if config.IsServerSide then openInfo.Id.Value else remoteOpenInfo.Id.Value
        return 
            {
                OpenInfo = openInfo
                RemoteOpenInfo = remoteOpenInfo
                StreamId = streamId
            }
        //context.StreamId <- if config.IsServerSide then openInfo.Id.Value
        //                    else remoteOpenInfo.Id.Value
        //context.RemoteOpenInfo <- remoteOpenInfo
        //context.XmppStream <- xmppStream
    }
    |> Log.TraceMe

type ICoreStreamOpenerService = 
    abstract Info : OpenHandShakeInfo with get

// Nobody should ever use this type directly (only the interface above). 
// This makes sure we can replace this in unit tests.
// This makes also sure that we can build custom Opener which provide the same information without breaking plugins.
type internal XmppCoreStreamOpener (config : IRuntimeConfig) =
    let mutable info = None
    interface ICoreStreamOpenerService with
        member x.Info = 
            match info with
            | Some i -> i
            | None -> invalidOp "The stream has to be opened for the info to be available"

    interface IInternalStreamOpener with
        member x.PluginService = Service.FromInstance<ICoreStreamOpenerService, _> x
        member x.OpenStream stream =
            async {
                let! res = doHandshake config stream
                // Provide API for the gathered info.
                info <- Some res
                return ()
            }
        member x.CloseStream stream =
            stream.WriteEnd()