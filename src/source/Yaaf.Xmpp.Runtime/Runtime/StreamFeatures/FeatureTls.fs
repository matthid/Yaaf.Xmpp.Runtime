// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Runtime.Features

open FSharpx.Collections
open Yaaf.FSharp.Control
open Yaaf.Helper
open Yaaf.IO
open Yaaf.Logging
open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime

module TlsParsing = 
    open System.Xml.Linq
    open Yaaf.Xml
    
    type TLSCommand = 
        | Start
        /// 5.4.2.3. Proceed Case: If the proceed case occurs, the receiving entity MUST return a <proceed/> element qualified by the 'urn:ietf:params:xml:ns:xmpp-tls' namespace.
        | Proceed
        /// 5.4.2.2. Failure Case: If the failure case occurs, the receiving entity MUST return a <failure/> element qualified by the 'urn:ietf:params:xml:ns:xmpp-tls' namespace, close the XML stream, and terminate the underlying TCP connection. 
        | Failure

    let tlsNS = "urn:ietf:params:xml:ns:xmpp-tls"
    
    let parseTlsCommand (elem : XElement) = // parse something within the "stream"
                                            
        let defaultBehaviour() = failwith "expected feature list"
        match elem.Name.NamespaceName with
        | Equals tlsNS -> 
            match elem.Name.LocalName with
            | "starttls" -> Start
            | "failure" -> Failure
            | "proceed" -> Proceed
            | _ -> defaultBehaviour()
        | _ -> defaultBehaviour()
    
    let createTlsCommandElement command = 
        let name = 
            match command with
            | Start -> "starttls"
            | Failure -> "failure"
            | Proceed -> "proceed"
        getXName name tlsNS |> getXElem
    
    let checkIfTlsFeature (elem : XElement) = 
        let hasRequiredElem() = elem.Elements() |> Seq.exists (fun item -> item.Name.LocalName = "required")
        match elem.Name.NamespaceName, elem.Name.LocalName with
        | (Equals tlsNS), "starttls" -> Some <| hasRequiredElem()
        | _ -> None
    
    let createAnnouncementElement (required : bool) = 
        let t = getXName "starttls" tlsNS |> getXElem
        if required then t |> addChild (getXName "required" tlsNS |> getXElem)
        else t

type ITlsConfig =
    abstract EnableTls : bool with get
    abstract ForceTls : bool with get
    abstract ServerCertificateValidationCallback : System.Net.Security.RemoteCertificateValidationCallback with get
    abstract ClientCertificateSelectionCallback : System.Net.Security.LocalCertificateSelectionCallback with get
    abstract TlsHostname : string with get
    abstract Certificates : System.Security.Cryptography.X509Certificates.X509CertificateCollection with get
    abstract SslProtocols : System.Security.Authentication.SslProtocols with get
    abstract CheckRevocation : bool with get
    abstract Certificate : System.Security.Cryptography.X509Certificates.X509Certificate with get
    abstract ClientCertificateRequired : bool with get
type TlsConfig =
    {
        EnableTls : bool 
        ForceTls : bool 
        ServerCertificateValidationCallback : System.Net.Security.RemoteCertificateValidationCallback
        ClientCertificateSelectionCallback : System.Net.Security.LocalCertificateSelectionCallback
        TlsHostname : string
        Certificates : System.Security.Cryptography.X509Certificates.X509CertificateCollection
        SslProtocols : System.Security.Authentication.SslProtocols
        CheckRevocation : bool
        Certificate : System.Security.Cryptography.X509Certificates.X509Certificate
        ClientCertificateRequired : bool
    } with
    interface ITlsConfig with
        member x.EnableTls = x.EnableTls
        member x.ForceTls = x.ForceTls
        member x.ServerCertificateValidationCallback = x.ServerCertificateValidationCallback
        member x.ClientCertificateSelectionCallback = x.ClientCertificateSelectionCallback
        member x.TlsHostname = x.TlsHostname
        member x.Certificates = x.Certificates
        member x.SslProtocols = x.SslProtocols
        member x.CheckRevocation = x.CheckRevocation
        member x.Certificate = x.Certificate 
        member x.ClientCertificateRequired = x.ClientCertificateRequired
    static member OfInterface (x : ITlsConfig) =
        {
            EnableTls = x.EnableTls
            ForceTls = x.ForceTls
            ServerCertificateValidationCallback = x.ServerCertificateValidationCallback
            ClientCertificateSelectionCallback = x.ClientCertificateSelectionCallback
            TlsHostname = x.TlsHostname
            Certificates = x.Certificates
            SslProtocols = x.SslProtocols
            CheckRevocation = x.CheckRevocation
            Certificate = x.Certificate 
            ClientCertificateRequired = x.ClientCertificateRequired
        }
    static member Default =
        let defaultServerCertificateValidationCallback = System.Net.Security.RemoteCertificateValidationCallback(fun _ _ _ _ -> true)
        let defaultClientCertificateValidationCallback = System.Net.Security.LocalCertificateSelectionCallback(fun a b c d e -> null)
        {
            EnableTls = true
            ForceTls = true
            ServerCertificateValidationCallback = defaultServerCertificateValidationCallback
            ClientCertificateSelectionCallback = null
            TlsHostname = ""
            Certificates = new System.Security.Cryptography.X509Certificates.X509CertificateCollection()
            SslProtocols = System.Security.Authentication.SslProtocols.Tls
            CheckRevocation = false
            Certificate = null
            ClientCertificateRequired = false
        }
                                                       
type ITlsService =
    abstract IsEnabled : bool with get
    abstract IsForced : bool with get
    abstract TlsActive : bool with get
    abstract IsRemoteAuthenticated : bool with get

open TlsParsing
type TlsFeature(config : ITlsConfig, coreApi : ICoreStreamApi, features : IStreamFeatureService) = 
    let mutable tlsActive = false
    let isEnabled = config.EnableTls
    let isForced = config.ForceTls
    let mutable tlsAuthenticated = false
    interface ITlsService with 
        member x.IsEnabled = isEnabled
        member x.IsForced = isForced
        member x.TlsActive = tlsActive
        member x.IsRemoteAuthenticated = tlsAuthenticated
    interface IStreamFeatureHandler with
        member x.PluginService = Service.FromInstance<ITlsService,_> x
        // context.AddContextChange onContextChange
        member x.GetState(featureList) = 
            if tlsActive then Unavailable // already active
            else 
                let maybeFound = 
                    featureList
                    |> Seq.choose (fun elem -> checkIfTlsFeature elem |> Option.map (fun t -> elem, t))
                    |> Seq.tryHead
                match maybeFound with
                | None -> 
                    if config.ForceTls then failwith "tls was forced by config but no tls advertisement from server was sent."
                    Unavailable
                | Some(elem, mandatory) -> 
                    if (not config.EnableTls && mandatory) then failwith "Tls was disabled by configuration but is mandatory by the serer"
                    Available(mandatory, elem)
            
        //
        /// Used when client selects feature
        member x.InitializeFeature () = 
            async { 
                Log.Verb (fun _ -> "starting Starttls")
                if tlsActive then failwith "tsl already started"
                let xmlStream = coreApi.AbstractStream
                do! xmlStream.Write (createTlsCommandElement TLSCommand.Start)
                let! elem = xmlStream.ReadElement()
                let result = parseTlsCommand elem
                match result with
                | TLSCommand.Start -> failwith "starttsl is not expected"
                | TLSCommand.Proceed -> ()
                | TLSCommand.Failure -> failwith "tsl connection failed"
                // Prevent forward buffering
                //xmppStream.Dispose()
                //reader.Close()
                Log.Verb(fun _ -> "Proceed startls")
                let currentStream = 
                    match coreApi.CoreStreamHistory.Head with
                    | :? IStreamManager<System.IO.Stream> as mgr -> mgr.PrimitiveStream
                    | _ -> failwith "TLS currently supports only System.IO.Stream as primitive!"
                let sslStream = 
                    new System.Net.Security.SslStream(currentStream, true, config.ServerCertificateValidationCallback, config.ClientCertificateSelectionCallback)
                do! sslStream.AuthenticateAsClientAsync(config.TlsHostname, config.Certificates, config.SslProtocols, config.CheckRevocation) |> Task.ofPlainTask
                //|> Task.ofPlainTask
                if not sslStream.CanRead || not sslStream.CanWrite then failwith "tsl handshake failed"
                //do! emptyStream sslStream
                
                coreApi.SetCoreStream(new IOStreamManager(sslStream))
                tlsActive <- true
                do! coreApi.OpenStream()
                do! features.OpenStream true
                return ()
            }
            |> Log.TraceMe
            
        // Server
        // Features can disable itself by returning None
        member x.CreateAnnounceFeatureElement() = 
            if tlsActive then FeatureInfo.Unavailable
            else FeatureInfo.Available(config.ForceTls, createAnnouncementElement config.ForceTls)
            
        /// Used so that the receiving entity can select the selected feature
        member x.IsFeatureSelected elem = 
            match elem.Name.NamespaceName with
            | Equals tlsNS -> true
            | _ -> false
            
        member x.HandleReceivingCommunication(elem) = 
            async { 
                let xmlStream = coreApi.AbstractStream
                do! xmlStream.Write (createTlsCommandElement TLSCommand.Proceed)
                
                let currentStream = 
                    match coreApi.CoreStreamHistory.Head with
                    | :? IStreamManager<System.IO.Stream> as mgr -> mgr.PrimitiveStream
                    | _ -> failwith "TLS currently supports only System.IO.Stream as primitive!"

                let sslStream = 
                    new System.Net.Security.SslStream(currentStream, true, config.ServerCertificateValidationCallback, config.ClientCertificateSelectionCallback)
                do! sslStream.AuthenticateAsServerAsync(config.Certificate, config.ClientCertificateRequired, config.SslProtocols, config.CheckRevocation) |> Task.ofPlainTask
                
                coreApi.SetCoreStream(new IOStreamManager(sslStream))
                tlsActive <- true
                do! coreApi.OpenStream()
                do! features.OpenStream true
                // Check if the other entity is already authenticated
                // 6.3.4.  Mechanism Offers, for BIND
                //newContext.Tls_IsRemoteAuthenticated <- true
                return ()
            }
            |> Log.TraceMe
