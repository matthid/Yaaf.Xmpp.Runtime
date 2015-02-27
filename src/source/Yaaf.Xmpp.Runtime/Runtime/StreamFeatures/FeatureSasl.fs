// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Runtime.Features

open FSharpx.Collections

open Yaaf.Helper
open Yaaf.Logging
open Yaaf.Sasl
open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime

module SaslParsing = 
    open System
    open System.Xml.Linq
    open Yaaf.Xml
    
    let saslNS = "urn:ietf:params:xml:ns:xmpp-sasl"
    
    let parseSaslFailure (s : string) = 
        match s with
        | "aborted" -> Aborted
        | "account-disabled" -> AccountDisabled
        | "credentials-expired" -> CredentialsExpired
        | "encryption-required" -> EncryptionRequired
        | "incorrect-encoding" -> IncorrectEncoding
        | "invalid-authzid" -> InvalidAuthzid
        | "invalid-mechanism" -> InvalidMechanism
        | "malformed-request" -> MalformedRequest
        | "mechanism-too-weak" -> MechanismTooWeak
        | "not-authorized" -> SaslFailure.NotAuthorized
        | "temporary-auth-failure" -> TemporaryAuthFailure
        | _ -> UnknownSaslFailure s
    
    let writeSaslFailure (t : SaslFailure) = 
        match t with
        | EncryptionRequired -> "encryption-required"
        | CredentialsExpired -> "credentials-expired"
        | AccountDisabled -> "account-disabled"
        | Aborted -> "aborted"
        | IncorrectEncoding -> "incorrect-encoding"
        | InvalidAuthzid -> "invalid-authzid"
        | InvalidMechanism -> "invalid-mechanism"
        | MalformedRequest -> "malformed-request"
        | MechanismTooWeak -> "mechanism-too-weak"
        | SaslFailure.NotAuthorized -> "not-authorized"
        | TemporaryAuthFailure -> "temporary-auth-failure"
        | UnknownSaslFailure s -> s
    
    type SASLCommand = 
        | ClientCommand of SaslClientMessage
        | ServerCommand of SaslServerMessage
    
    let parseSaslCommand (elem : XElement) = 
        let parseData s = 
            match s with
            | Equals null | Equals "" -> None
            | "=" -> Some [||]
            | _ -> Some <| Convert.FromBase64String(s)
        match elem.Name.LocalName with
        | "auth" -> 
            let plain = elem.Attribute(getXName "mechanism" "")
            let initialResponse = parseData elem.Value
            (ClientCommand <| Auth(plain.Value, initialResponse))
        | "abort" -> (ClientCommand Abort)
        | "challenge" -> (ServerCommand <| Challenge(Convert.FromBase64String elem.Value))
        | "response" -> (ClientCommand <| Response(Convert.FromBase64String elem.Value))
        | "success" -> 
            let additionalData = parseData elem.Value
            (ServerCommand <| SaslServerMessage.Success additionalData)
        | "failure" -> 
            let text = 
                elem.Elements()
                |> Seq.filter (fun e -> e.Name.LocalName = "text")
                |> Seq.map (fun e -> e.Value)
                |> Seq.tryHead
            ServerCommand(SaslServerMessage.Failure((elem.Elements() |> Seq.head).Name.LocalName |> parseSaslFailure, text))
        | _ -> failwith "sasl command expected!"
    
    let createSaslCommandElement (sasl : SASLCommand) = 
        let toBase64 optionalData = 
            match optionalData with
            | Some data -> 
                if data |> Array.isEmpty then "="
                else Convert.ToBase64String data
            | None -> ""
        match sasl with
        | ClientCommand command -> 
            match command with
            | Response data -> [ Convert.ToBase64String data ] |> getXElemWithChilds (getXName "response" saslNS)
            | Auth(mech, value) -> 
                [ XAttribute(getXName "mechanism" "", mech) :> obj
                  toBase64 value :> obj ]
                |> getXElemWithChilds (getXName "auth" saslNS)
            | Abort -> getXName "abort" saslNS |> getXElem
        | ServerCommand command -> 
            match command with
            | Challenge data -> [ Convert.ToBase64String data ] |> getXElemWithChilds (getXName "challenge" saslNS)
            | SaslServerMessage.Success data -> [ toBase64 data ] |> getXElemWithChilds (getXName "success" saslNS)
            | SaslServerMessage.Failure(condition, text) -> 
                [ yield getXName (writeSaslFailure condition) saslNS |> getXElem
                  match text with
                  | Some s -> yield getXElemWithChilds (getXName "text" saslNS) [ s ]
                  | None -> () ]
                |> getXElemWithChilds (getXName "failure" saslNS)
    
    let checkIfSaslFeature (elem : XElement) = 
        match elem.Name.NamespaceName, elem.Name.LocalName with
        | (Equals saslNS), "mechanisms" -> 
            Some(elem.Elements()
                 |> Seq.filter (fun e -> e.Name.LocalName = "mechanism")
                 |> Seq.map (fun e -> e.Value)
                 |> Seq.toList)
        | _ -> None
    
    let createAnnouncementElement (mechList : string seq) = 
        mechList
        |> Seq.map (fun mech -> 
               getXName "mechanism" saslNS
               |> getXElem
               |> addChild mech)
        |> getXElemWithChilds (getXName "mechanisms" saslNS)
open SaslParsing

type ISaslConfig =
    abstract ServerMechanism : Yaaf.Sasl.IServerMechanism list with get
    abstract ClientMechanism : Yaaf.Sasl.IClientMechanism list with get

type SaslConfig =
    {
        ServerMechanism : Yaaf.Sasl.IServerMechanism list
        ClientMechanism : Yaaf.Sasl.IClientMechanism list
    } with 
    interface ISaslConfig with
        member x.ServerMechanism = x.ServerMechanism
        member x.ClientMechanism = x.ClientMechanism
    static member OfInterface (x:ISaslConfig) = 
        {
            ServerMechanism = x.ServerMechanism
            ClientMechanism = x.ClientMechanism
        }
    static member Default =
        {
            ServerMechanism = []
            ClientMechanism = []
        }

type ISaslService =
    abstract AuthorizedId : string with get
    abstract IsRemoteAuthenticated : bool with get


type SaslFeature(config : ISaslConfig, tls : ITlsService, coreApi : ICoreStreamApi, features : IStreamFeatureService) = 
    let mutable serverMechList = []
    let mutable usedMech = []
    let mutable isRemoteAuthenticated = false
    let mutable authorizedId = ""
    let mutable externalMech = new Yaaf.Sasl.External.ExternalClient(None)
    let mutable externalMechAvailable = false

    
    interface ISaslService with 
        member x.AuthorizedId = authorizedId
        member x.IsRemoteAuthenticated = isRemoteAuthenticated

    interface IStreamFeatureHandler with
        member x.PluginService = Service.FromInstance<ISaslService,_> x

        member x.GetState(featureList) = 
            let maybeFound = 
                featureList
                |> Seq.choose (fun elem -> checkIfSaslFeature elem |> Option.map (fun t -> elem, t))
                |> Seq.tryHead
            match maybeFound with
            | None -> Unavailable
            | Some(elem, mechlist) -> 
                // TODO add external if tls already authenticated the remote end
                //if config.IsClient && mechlist |> List.exists (fun m -> m = "EXTERNAL") then
                //    externalMechAvailable <- true
                serverMechList <- mechlist
                // XMPP # 6.3.1.  Mandatory-to-Negotiate
                Available(true, elem)
            
        //
        /// Used when client selects feature
        member x.InitializeFeature () = 
            async { 
                let xmlStream = coreApi.AbstractStream
                Log.Verb(fun _ -> "starting sasl auth")

                let currentStream = 
                    match coreApi.CoreStreamHistory.Head with
                    | :? IStreamManager<System.IO.Stream> as mgr -> mgr.PrimitiveStream
                    | _ -> failwith "TLS currently supports only System.IO.Stream as primitive!"
                

                let useMech = 
                    config.ClientMechanism
                    |> List.filter (fun m -> serverMechList |> List.exists (fun mechName -> m.Name = mechName))
                    |> List.rev
                    |> List.tryFind (fun m -> 
                            usedMech
                            |> List.exists (fun mechName -> m.Name = mechName)
                            |> not)
                match useMech with
                | None -> return failwith "no matching mechanism found"
                | Some mech -> 
                    // TODO: 6.3.3.  Mechanism Preferences
                    let rec processNext (saslContext, msg) = 
                        async { 
                            do! xmlStream.Write (createSaslCommandElement (ClientCommand msg))
                            let! elem = xmlStream.ReadElement()
                            let result = parseSaslCommand elem
                            return! match result with
                                    | ClientCommand _ -> failwith "ClientCommand is not expected"
                                    | ServerCommand c -> 
                                        match c with
                                        | Challenge _ -> processNext (mech.GetNextMessage(Some(saslContext, c)))
                                        | SaslServerMessage.Failure(failure, text) -> failwithf "sasl auth failure %A, %A" failure text
                                        | SaslServerMessage.Success(finaldata) -> 
                                            // xmppStream.Dispose()
                                            async.Return(mech.UpdateStream (saslContext, finaldata) currentStream)
                        }
                    let! newStream = processNext (mech.GetNextMessage None)
                    coreApi.SetCoreStream(new IOStreamManager(newStream))
                    isRemoteAuthenticated <- true
                    // NOTE: currently I see no need for this API on the client
                    // authorizedId <- mech.GetAuthorizeId()
                    do! coreApi.OpenStream()
                    do! features.OpenStream true
                    Log.Verb(fun _ -> "Sasl successfully finished!")
                    return ()
            }
            
        // Server
        // Features can disable itself by returning None
        member x.CreateAnnounceFeatureElement() = 
            // Only announce when tls is not mandatory, 6.3.4.  Mechanism Offers
            if isRemoteAuthenticated || (tls.IsEnabled && tls.IsForced && not tls.TlsActive) then FeatureInfo.Unavailable
            else FeatureInfo.Available(true, createAnnouncementElement (config.ServerMechanism |> Seq.map (fun m -> m.Name)))
            
        /// Used so that the receiving entity can select the selected feature
        member x.IsFeatureSelected elem = 
            match elem.Name.NamespaceName with
            | Equals saslNS -> true
            | _ -> false
            
        member x.HandleReceivingCommunication (elem) = 
            async { 
                let xmlStream = coreApi.AbstractStream
                let command = parseSaslCommand elem
                let currentStream = 
                    match coreApi.CoreStreamHistory.Head with
                    | :? IStreamManager<System.IO.Stream> as mgr -> mgr.PrimitiveStream
                    | _ -> failwith "TLS currently supports only System.IO.Stream as primitive!"
                return! 
                    match command with
                    | SASLCommand.ClientCommand c -> 
                        match c with
                        | Auth(mech, initData) -> 
                            match config.ServerMechanism |> Seq.tryFind (fun h -> h.Name = mech) with
                            | Some k ->           
                                let rec nextAuthStep (mechContext, clientMessage) = 
                                    async { 
                                        let (mechContext, serverMessage) = k.GetNextMessage(mechContext, c)
                                        do! xmlStream.Write (createSaslCommandElement (ServerCommand serverMessage))
                                        return! 
                                            match serverMessage with
                                            | Challenge _ -> 
                                                async { 
                                                    let! elem = xmlStream.ReadElement()
                                                    let result = parseSaslCommand elem
                                                    return! 
                                                        match result with
                                                        | ServerCommand _ -> failwith "ServerCommand is not expected"
                                                        | ClientCommand c -> 
                                                            match c with
                                                            | Auth _ -> // Restart sasl auth, BUG: change mech?
                                                                nextAuthStep (None, c)
                                                            | Response _ | Abort -> nextAuthStep (Some mechContext, c)
                                                }
                                            | SaslServerMessage.Failure(failure, text) -> failwithf "Sasl auth failure: %A, %A" failure text
                                            | SaslServerMessage.Success(finaldata) -> 
                                                async { 
                                                    //xmppStream.Dispose()
                                                    let newStream = k.UpdateStream mechContext currentStream
                                                    // 6.3.2.  Restart
                                                    coreApi.SetCoreStream(new IOStreamManager(newStream))
                                                    isRemoteAuthenticated <- true
                                                    let id = k.GetAuthorizeId(mechContext)
                                                    let idParsed = JabberId.Parse(id)
                                                    authorizedId <- 
                                                        match idParsed.Localpart with
                                                        | Some s -> s
                                                        | None -> idParsed.Domainpart
                                                    do! coreApi.OpenStream()
                                                    do! features.OpenStream true
                                                    Log.Verb
                                                        (fun _ -> 
                                                        L "Client finished Sasl authentication successfully as %s (using: %s)!" id authorizedId)
                                                    return ()
                                                }
                                    }
                                nextAuthStep (None, c)
                            | None -> 
                                async { 
                                    do! xmlStream.Write (createSaslCommandElement (ServerCommand(SaslServerMessage.Failure (SaslFailure.InvalidMechanism, None))))
                                    do! coreApi.FailwithStream(new StreamErrorException(XmlStreamError.NotAuthorized, None, []))
                                    return ()
                                }
                        | _ -> failwith "not expected on this point"
                    | _ -> failwith "not expected on this point"
            }

