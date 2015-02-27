// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.XmlStanzas

open Yaaf.FSharp.Control
open FSharpx.Collections
open System.Collections.Generic
open System.Xml.Linq
open Yaaf.Helper
open Yaaf.IO
open Yaaf.Logging
open Yaaf.Xmpp

type XmlStanzaType = 
    | Iq
    | Message
    | Presence
    override x.ToString() = 
        match x with
        | Iq -> "Iq"
        | Message -> "Message"
        | Presence -> "Presence"

type StanzaHeader = 
    { To : JabberId option
      From : JabberId option
      Id : string option
      Type : string option
      StanzaType : XmlStanzaType } with
    /// Check if to is Some and bareids match
    member x.AddressedTo(jid : JabberId) =  x.To.IsSome && x.To.Value.BareId = jid.BareId
    /// Check if To is None or bareids match
    member x.AddressedToOrNone(jid : JabberId) =  x.To.IsNone || x.To.Value.BareId = jid.BareId
    member x.IsErrorStanza =
        x.Type.IsSome && x.Type.Value = "error"
    member x.AsErrorStanza =
        if x.IsErrorStanza then
            failwith "Already a error stanza!"
        { x with Type = Some "error"
                 From = x.To
                 To = x.From }

[<CustomEquality>][<NoComparison>]
type StanzaContents = 
    { CustomAttributes : XAttribute list
      Children : XElement list }
    static member Empty = 
        { CustomAttributes = []
          Children = [] }
    override x.Equals(y) = 
        match y with 
        | :? StanzaContents as other-> 
            let rec compareLists l1 l2 f =
                match l1, l2 with
                | (head1 :: tail1), (head2 :: tail2) ->
                    let res : Yaaf.Xml.Core.EqualResult = f head1 head2
                    if res.IsEqual then
                        compareLists tail1 tail2 f
                    else res
                | (h :: _), [] 
                | [], (h :: _) -> { IsEqual = false; Message = sprintf "Additional/Missing Content: %O" h }
                | [], [] -> { IsEqual = true; Message = "" }
            let asNode atts =
                new XElement(XName.Get "test", atts |> List.map(fun i -> i :> obj) |> List.toArray)
            let result = compareLists x.Children other.Children Yaaf.Xml.Core.equalXNodeAdvanced
            let finalResult =
                if result.IsEqual then
                    Yaaf.Xml.Core.equalXNodeAdvanced (asNode x.CustomAttributes) (asNode other.CustomAttributes)
                else result
            if not finalResult.IsEqual then
                // Because this code is mainly called in unit tests for pattern matching of mock calls
                // This logging helps in identifying errors.
                Log.Verb(fun _ -> L "result of StanzaContents comparison: %s" finalResult.Message)
            finalResult.IsEqual
        | _ -> false
    override x.GetHashCode() = failwith "not properly implemented"
    //interface System.IComparable with
    //    member x.CompareTo(y) = (match y with :? SpyFunc -> 0 | _ -> failwith "wrong type")


type ContentGenerator<'a> = 
    | AdvancedGenerator of ('a -> StanzaContents)
    | NormalGenerator of ('a -> XElement list)
    | SimpleGenerator of ('a -> XElement)
    
    member x.Generate a = 
        match x with
        | AdvancedGenerator g -> g a
        | NormalGenerator g -> 
            { CustomAttributes = []
              Children = g a }
        | SimpleGenerator g -> 
            { CustomAttributes = []
              Children = [ g a ] }
    
    static member Of(x : 'a -> StanzaContents) = AdvancedGenerator x
    static member Of(x : 'a -> XElement list) = NormalGenerator x
    static member Of(x : 'a -> XElement) = SimpleGenerator x

type StanzaCreator =
    { Header : StanzaHeader
      Contents : StanzaContents }
type IStanza =
    abstract Header : StanzaHeader
    abstract Contents : StanzaContents
    abstract AddContent : XElement -> IStanza
    abstract WithHeader : StanzaHeader -> IStanza
    abstract AsSimple : Stanza
    abstract GetCacheData<'a> : (IStanza -> 'a option) -> 'a option
and [<CustomEquality>][<NoComparison>] Stanza = 
    private { MyHeader : StanzaHeader
              MyContents : StanzaContents
              ContentCache : System.Collections.Concurrent.ConcurrentDictionary<System.Type, obj> } with
  
    override x.Equals(y) = 
        match y with 
        | :? Stanza as other -> 
            // Ignore cache!
            x.MyHeader = other.MyHeader && x.MyContents = other.MyContents
        | _ -> false
    override x.GetHashCode() = failwith "not properly implemented"

    member x.Header = x.MyHeader
    member x.Contents = x.MyContents
    static member OfCreator c =  
        {
            MyHeader = c.Header
            MyContents = c.Contents
            // obj = 'a option
            ContentCache = new System.Collections.Concurrent.ConcurrentDictionary<System.Type, obj>()
        }
    member x.AddContent elem = 
        { x with
            MyContents =
                { x.Contents with Children = x.Contents.Children @ [ elem ]}
            // content added, so we need a new cache.
            ContentCache = new System.Collections.Concurrent.ConcurrentDictionary<System.Type, obj>(x.ContentCache)
        }

    member x.WithHeader header =
        { x with
            MyHeader = header
            // we can keep the cache.
        }
    interface IStanza with
        member x.Header = x.Header
        member x.Contents = x.Contents
        member x.AddContent elem = x.AddContent elem :> IStanza
        member x.WithHeader elem = x.WithHeader elem :> IStanza
        member x.GetCacheData<'a> (gen) =
            x.ContentCache.GetOrAdd(typeof<'a>, fun t -> gen x :> obj)
            :?> 'a option
        member x.AsSimple = x


    member x.AsInterface = x :> IStanza
       
type Stanza<'a> = 
    private { RawStanza : Stanza
              MyData : 'a }
    member x.SimpleStanza = x.RawStanza
    member x.Header = x.RawStanza.Header
    member x.Contents = x.RawStanza.Contents
    member x.Data = x.MyData
    
    static member Create (rawStanza, data)= 
        {
            RawStanza = rawStanza
            MyData = data
        }

    static member Create ((generator : ContentGenerator<'a>), header, data) = 
        Stanza<_>.Create ((Stanza.OfCreator { Header = header; Contents = generator.Generate data }), data)
        
    static member CreateGen (generator : ContentGenerator<'a>) header data = 
        Stanza<_>.Create (generator, header, data)

    member x.BareFrom = 
        if x.Header.From.IsNone then failwith "the given stanza has no from address"
        x.Header.From.Value.BareJid
            
    member x.AddContent elem = 
        { x with
           RawStanza = x.RawStanza.AddContent elem
        }
    
    member x.WithHeader header =
        { x with
            RawStanza = x.RawStanza.WithHeader header
        }
    interface IStanza with
        member x.Header = x.Header
        member x.Contents = x.Contents
        member x.AddContent elem = x.AddContent elem :> IStanza
        member x.WithHeader elem = x.WithHeader elem :> IStanza
        member x.GetCacheData<'t> (gen) = x.SimpleStanza.AsInterface.GetCacheData<'t> (gen)
        member x.AsSimple = x.SimpleStanza
        
    member x.AsInterface = x :> IStanza

[<AutoOpen>]
module StanzaExtensions = 
    type IStanza with
        member stanza.IsEmptyIqResult () = stanza.Header.StanzaType = XmlStanzaType.Iq && stanza.Contents.Children |> List.isEmpty

        member x.As<'a>() = x :?> Stanza<'a>

[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module Stanza =
    let As<'a> (x:IStanza) = x.As<'a>() 
    /// creates an empty iq stanza with no children, stanza is the header of the stanza which we want to respond to.
    let createEmptyIqResult (toJid : JabberId Option) (stanza : StanzaHeader) : Stanza = 
        { Header = 
              { From = stanza.To
                To = toJid
                StanzaHeader.Id = stanza.Id
                StanzaHeader.StanzaType = stanza.StanzaType
                StanzaHeader.Type = Some "result" }
          Contents = StanzaContents.Empty } |> Stanza.OfCreator


/// The "error-type" MUST be one of the following: 
type StanzaErrorType = 
    ///  retry after providing credentials 
    | Auth
    /// do not retry (the error cannot be remedied) 
    | Cancel
    /// proceed (the condition was only a warning) 
    | Continue
    /// retry after changing the data sent 
    | Modify
    /// retry after waiting (the error is temporary) 
    | Wait
    
    member e.XmlString = 
        match e with
        | Auth -> "auth"
        | Cancel -> "cancel"
        | Continue -> "continue"
        | Modify -> "modify"
        | Wait -> "wait"
    
    static member Parse s = 
        match s with
        | "auth" -> Auth
        | "cancel" -> Cancel
        | "continue" -> Continue
        | "modify" -> Modify
        | "wait" -> Wait
        | _ -> raise <| MatchFailureException("unknown StanzaErrorType: " + s, 0, 0)

type StanzaErrorConditon = 
    /// the sender has sent a stanza containing XML that does not conform to the appropriate schema or that cannot be processed 
    /// (e.g., an IQ stanza that includes an unrecognized value of the 'type' attribute, or an element that is qualified by a recognized namespace 
    /// but that violates the defined syntax for the element); the associated error type SHOULD be "modify". 
    | BadRequest
    /// Access cannot be granted because an existing resource exists with the same name or address; the associated error type SHOULD be "cancel". 
    | Conflict
    /// The feature represented in the XML stanza is not implemented by the intended recipient or an intermediate server and therefore the stanza cannot
    ///  be processed (e.g., the entity understands the namespace but does not recognize the element name); the associated error type SHOULD be "cancel" or "modify". 
    | FeatureNotImplemented
    /// The requesting entity does not possess the necessary permissions to perform an action that only certain authorized roles or individuals are allowed to complete (i.e., it typically relates to authorization rather than authentication); the associated error type SHOULD be "auth". 
    | Forbidden
    /// The recipient or server can no longer be contacted at this address, typically on a permanent basis (as opposed to the &lt;redirect/&gt; error condition, which is used for temporary addressing failures); the associated error type SHOULD be "cancel" and the error stanza SHOULD include a new address (if available) as the XML character data of the <gone/> element (which MUST be a Uniform Resource Identifier [URI] or Internationalized Resource Identifier [IRI] at which the entity can be contacted, typically an XMPP IRI as specified in [XMPP‑URI]). 
    | Gone
    /// The server has experienced a misconfiguration or other internal error that prevents it from processing the stanza; the associated error type SHOULD be "cancel". 
    | InternalServerError
    /// <summary>The addressed JID or item requested cannot be found; the associated error type SHOULD be "cancel". </summary>
    /// <remarks>Security Warning: An application MUST NOT return this error if doing so would provide information about the intended recipient's network availability to an entity that is not authorized to know such information (for a more detailed discussion of presence authorization, refer to the discussion of presence subscriptions in [XMPP‑IM]); instead it MUST return a <service-unavailable/> stanza error (Section 8.3.3.19). </remarks>
    | ItemNotFound
    /// <summary>
    /// The sending entity has provided (e.g., during resource binding) or communicated (e.g., in the 'to' address of a stanza) an XMPP address or aspect 
    /// thereof that violates the rules defined in [XMPP‑ADDR]; the associated error type SHOULD be "modify". 
    /// </summary>
    /// <remarks>
    /// Implementation Note: Enforcement of the format for XMPP localparts is primarily the responsibility of the service at which the associated account or entity is located 
    /// (e.g., the example.com service is responsible for returning &lt;jid-malformed/&gt; errors related to all JIDs of the form &lt;localpart@example.com&gt;), 
    /// whereas enforcement of the format for XMPP domainparts is primarily the responsibility of the service that seeks to route a stanza to the service 
    /// identified by that domainpart (e.g., the example.org service is responsible for returning &lt;jid-malformed/&gt; errors related to stanzas that users of 
    /// that service have to tried send to JIDs of the form &lt;localpart@example.com&gt;). However, any entity that detects a malformed JID MAY return this error. 
    /// </remarks>
    | JidMalformed
    /// The recipient or server understands the request but cannot process it because the request does not meet criteria defined by the recipient or server (e.g., a request to subscribe to information that does not simultaneously include configuration parameters needed by the recipient); the associated error type SHOULD be "modify". 
    | NotAcceptable
    /// The recipient or server does not allow any entity to perform the action (e.g., sending to entities at a blacklisted domain); the associated error type SHOULD be "cancel". 
    | NotAllowed
    /// The sender needs to provide credentials before being allowed to perform the action, or has provided improper credentials (the name "not-authorized", which was borrowed from the "401 Unauthorized" error of [HTTP], might lead the reader to think that this condition relates to authorization, but instead it is typically used in relation to authentication); the associated error type SHOULD be "auth". 
    | NotAuthorized
    /// The entity has violated some local service policy (e.g., a message contains words that are prohibited by the service) and the server MAY choose to specify the policy in the <text/> element or in an application-specific condition element; the associated error type SHOULD be "modify" or "wait" depending on the policy being violated. 
    | PolicyViolation
    /// <summary>The intended recipient is temporarily unavailable, undergoing maintenance, etc.; the associated error type SHOULD be "wait". </summary>
    /// <remarks>Security Warning: An application MUST NOT return this error if doing so would provide information about the intended recipient's network availability to an entity that is not authorized to know such information (for a more detailed discussion of presence authorization, refer to the discussion of presence subscriptions in [XMPP‑IM]); instead it MUST return a <service-unavailable/> stanza error (Section 8.3.3.19). </remarks>
    | RecipientUnavailable
    /// <summary>The recipient or server is redirecting requests for this information to another entity, typically in a temporary fashion (as opposed to the &lt;gone/&gt; error condition, which is used for permanent addressing failures); the associated error type SHOULD be "modify" and the error stanza SHOULD contain the alternate address in the XML character data of the <redirect/> element (which MUST be a URI or IRI with which the sender can communicate, typically an XMPP IRI as specified in [XMPP‑URI]). </summary>
    /// <remarks>Security Warning: An application receiving a stanza-level redirect SHOULD warn a human user of the redirection attempt and request approval before proceeding to communicate with the entity whose address is contained in the XML character data of the <redirect/> element, because that entity might have a different identity or might enforce different security policies. The end-to-end authentication or signing of XMPP stanzas could help to mitigate this risk, since it would enable the sender to determine if the entity to which it has been redirected has the same identity as the entity it originally attempted to contact. An application MAY have a policy of following redirects only if it has authenticated the receiving entity. In addition, an application SHOULD abort the communication attempt after a certain number of successive redirects (e.g., at least 2 but no more than 5). </remarks>
    | Redirect
    /// The requesting entity is not authorized to access the requested service because prior registration is necessary (examples of prior registration include members-only rooms in XMPP multi-user chat [XEP‑0045] and gateways to non-XMPP instant messaging services, which traditionally required registration in order to use the gateway [XEP‑0100]); the associated error type SHOULD be "auth". 
    | RegistrationRequired
    ///  A remote server or service specified as part or all of the JID of the intended recipient does not exist or cannot be resolved (e.g., there is no _xmpp-server._tcp DNS SRV record, the A or AAAA fallback resolution fails, or A/AAAA lookups succeed but there is no response on the IANA-registered port 5269); the associated error type SHOULD be "cancel". 
    | RemoteServerNotFound
    /// A remote server or service specified as part or all of the JID of the intended recipient (or needed to fulfill a request) was resolved but communications could not be established within a reasonable amount of time (e.g., an XML stream cannot be established at the resolved IP address and port, or an XML stream can be established but stream negotiation fails because of problems with TLS, SASL, Server Dialback, etc.); the associated error type SHOULD be "wait" (unless the error is of a more permanent nature, e.g., the remote server is found but it cannot be authenticated or it violates security policies). 
    | RemoteServerTimeout
    /// The server or recipient is busy or lacks the system resources necessary to service the request; the associated error type SHOULD be "wait". 
    | ResourceConstraint
    /// <summary>The server or recipient does not currently provide the requested service; the associated error type SHOULD be "cancel". </summary>
    /// <remarks>Security Warning: An application MUST return a <service-unavailable/> stanza error (Section 8.3.3.19) instead of &lt;item-not-found/&gt; (Section 8.3.3.7) or &lt;recipient-unavailable/&gt; (Section 8.3.3.13) if sending one of the latter errors would provide information about the intended recipient's network availability to an entity that is not authorized to know such information (for a more detailed discussion of presence authorization, refer to [XMPP‑IM]).  </remarks>
    | ServiceUnavailable
    /// The requesting entity is not authorized to access the requested service because a prior subscription is necessary (examples of prior subscription include authorization to receive presence information as defined in [XMPP‑IM] and opt-in data feeds for XMPP publish-subscribe as defined in [XEP‑0060]); the associated error type SHOULD be "auth".
    | SubscriptionRequired
    /// The error condition is not one of those defined by the other conditions in this list; any error type can be associated with this condition, and it SHOULD NOT be used except in conjunction with an application-specific condition.
    | UndefinedCondition
    /// The recipient or server understood the request but was not expecting it at this time (e.g., the request was out of order); the associated error type SHOULD be "wait" or "modify".
    | UnexpectedRequest
    /// However, because additional error conditions might be defined in the future, if an entity receives a stanza error condition that it does not understand then it MUST treat the unknown condition as equivalent to &lt;undefined-condition/&gt; (Section 8.3.3.21).
    | UnknownCondition of string
    
    member c.XmlString = 
        match c with
        | BadRequest -> "bad-request"
        | Conflict -> "conflict"
        | FeatureNotImplemented -> "feature-not-implemented"
        | Forbidden -> "forbidden"
        | Gone -> "gone"
        | InternalServerError -> "internal-server-error"
        | ItemNotFound -> "item-not-found"
        | JidMalformed -> "jid-malformed"
        | NotAcceptable -> "not-acceptable"
        | NotAllowed -> "not-allowed"
        | NotAuthorized -> "not-authorized"
        | PolicyViolation -> "policy-violation"
        | RecipientUnavailable -> "recipient-unavailable"
        | Redirect -> "redirect"
        | RegistrationRequired -> "registration-required"
        | RemoteServerNotFound -> "remote-server-not-found"
        | RemoteServerTimeout -> "remote-server-timeout"
        | ResourceConstraint -> "resource-constraint"
        | ServiceUnavailable -> "service-unavailable"
        | SubscriptionRequired -> "subscription-required"
        | UndefinedCondition -> "undefined-condition"
        | UnexpectedRequest -> "unexpected-request"
        | UnknownCondition s -> s
    
    static member Parse s = 
        match s with
        | "bad-request" -> BadRequest
        | "conflict" -> Conflict
        | "feature-not-implemented" -> FeatureNotImplemented
        | "forbidden" -> Forbidden
        | "gone" -> Gone
        | "internal-server-error" -> InternalServerError
        | "item-not-found" -> ItemNotFound
        | "jid-malformed" -> JidMalformed
        | "not-acceptable" -> NotAcceptable
        | "not-allowed" -> NotAllowed
        | "not-authorized" -> NotAuthorized
        | "policy-violation" -> PolicyViolation
        | "recipient-unavailable" -> RecipientUnavailable
        | "redirect" -> Redirect
        | "registration-required" -> RegistrationRequired
        | "remote-server-not-found" -> RemoteServerNotFound
        | "remote-server-timeout" -> RemoteServerTimeout
        | "resource-constraint" -> ResourceConstraint
        | "service-unavailable" -> ServiceUnavailable
        | "subscription-required" -> SubscriptionRequired
        | "undefined-condition" -> UndefinedCondition
        | "unexpected-request" -> UnexpectedRequest
        | s -> UnknownCondition s

type Langcode = { Code : string } with
    static member EN = "en"
    static member OfString s = { Code = s }

// See 8.3.2.  Syntax
type StanzaErrorData = 
    { //To : JabberId option
      //From : JabberId option
      //Id : string option
      // Type : string option, always "error"
      By : string option
      FaultXml : XElement list
      ErrorType : StanzaErrorType
      Condition : StanzaErrorConditon
      /// Text and langcode
      Text : (string * Langcode) option
      ApplicationSpecific : XElement option } with
    static member CreateSimpleErrorData errorType condition =
        { By = None
          FaultXml = []
          ErrorType = errorType
          Condition = condition
          /// Text and langcode
          Text = None
          ApplicationSpecific = None } 

    static member BadRequest = 
        StanzaErrorData.CreateSimpleErrorData StanzaErrorType.Modify StanzaErrorConditon.BadRequest

// StanzaType : XmlStanzaType
type StanzaError = Stanza<StanzaErrorData>

open System
/// Used when we parse a stanza with an error type
[<Serializable>]
type ReceivedStanzaException =
    inherit Exception
    val private errorStanza : StanzaError
    val private rawStanza : IStanza
    new (msg : StanzaError, stanza : IStanza) = { 
        inherit Exception(msg.Data.Condition.XmlString)
        errorStanza = msg
        rawStanza = stanza 
    }
    new (msg : StanzaError, text:string, stanza : IStanza) = {
        inherit Exception(text)
        errorStanza = msg
        rawStanza = stanza 
    } 
    new (info:System.Runtime.Serialization.SerializationInfo, context:System.Runtime.Serialization.StreamingContext) = {
        inherit Exception(info, context)
        errorStanza = Unchecked.defaultof<_>
        rawStanza = Unchecked.defaultof<_>
    }
    member x.ErrorStanza
        with get () = x.errorStanza
    member x.RawStanza
        with get () = x.rawStanza


/// Unable to parse a given stanza.
/// NOTE this does not inherit StanzaException because this exception is thrown within methods where the RawStanza is not available.
/// It will/can be catched and rethrown as StanzaException as soon as the RawStanza is available.
/// Or it will finally be handled in the XmlStanzaPlugin logic.
[<Serializable>]
type StanzaParseException =
    inherit Exception
    //let setElem e =
    //    x.Data.["FailedElementToParse"] <- e
    val elem : Choice<XElement,IStanza>
    new (e : XElement) = { 
        inherit Exception()
        elem = Choice1Of2 e
    }
    new (cause : IStanza) = { 
        inherit Exception()
        elem = Choice2Of2 cause
    }
    new (e : XElement, msg : string) = { inherit Exception(msg); elem = Choice1Of2 e }
    new (cause : IStanza, msg : string) = { 
        inherit Exception(msg)
        elem = Choice2Of2 cause 
    }
    new (e : XElement, msg:string, inner:Exception) = { inherit Exception(msg, inner); elem = Choice1Of2 e }       
    new (cause : IStanza, msg:string, inner:Exception) = { 
        inherit Exception(msg, inner)
        elem = Choice2Of2 cause }       
    new (info:System.Runtime.Serialization.SerializationInfo, context:System.Runtime.Serialization.StreamingContext) = {
        inherit Exception(info, context)
        elem = Unchecked.defaultof<_>
    }
    member x.FailedElementToParse
        with get () =
            x.elem

/// Used when we want to return a stanza error
[<Serializable>]
type StanzaException =
    inherit Exception
    val errorStanza : StanzaError
    new (msg : StanzaError) = { inherit Exception(msg.Data.Condition.XmlString); errorStanza = msg }
    new (msg : StanzaError, txt : string) = { inherit Exception(txt); errorStanza = msg }
    new (msg : StanzaError, txt : string, inner : exn) = { inherit Exception(txt, inner); errorStanza = msg }
    new (msg:StanzaError, inner:Exception) = { inherit Exception(msg.Data.Condition.XmlString, inner); errorStanza = msg }       
    new (info:System.Runtime.Serialization.SerializationInfo, context:System.Runtime.Serialization.StreamingContext) = {
        inherit Exception(info, context)
        errorStanza = Unchecked.defaultof<_>
    }
    member x.ErrorStanza
        with get () = x.errorStanza
/// The stanza could not be validated
[<Serializable>]
type StanzaValidationException  =
    inherit StanzaException
    val elem : StanzaError
    new (e : StanzaError) = { 
        inherit StanzaException(e)
        elem = e
    }
    new (e : StanzaError, msg : string) = { inherit StanzaException(e, msg); elem = e }
    new (e : StanzaError, msg:string, inner:Exception) = { inherit StanzaException(e, msg, inner); elem = e }       
    new (info:System.Runtime.Serialization.SerializationInfo, context:System.Runtime.Serialization.StreamingContext) = {
        inherit StanzaException(info, context)
        elem = Unchecked.defaultof<_>
    }
    member x.StanzaToReturn
        with get () =
            x.elem

[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module StanzaException = 
    open System.Xml.Linq
    open Yaaf.Xml
    
    let stanzasNs = "urn:ietf:params:xml:ns:xmpp-stanzas"
    let createErrorContentFromErrorData (originalStanza : StanzaErrorData) = 
        let ns = KnownStreamNamespaces.abstractStreamNS
        [ yield XAttribute(getXName "type" "", originalStanza.ErrorType.XmlString) :> obj
          if originalStanza.By.IsSome then yield XAttribute(getXName "by" "", originalStanza.By.Value) :> obj
          yield getXName (originalStanza.Condition.XmlString) stanzasNs |> getXElem :> obj
          if originalStanza.Text.IsSome then 
              let text, langCode = originalStanza.Text.Value
              yield [ XAttribute(getXName "lang" xmlNs, langCode) :> obj
                      text :> obj ]
                    |> getXElemWithChilds (getXName "text" stanzasNs) :> obj
          if originalStanza.ApplicationSpecific.IsSome then yield originalStanza.ApplicationSpecific.Value :> obj ]
        |> getXElemWithChilds (getXName "error" ns)
    
    let errorContentGenerator = ContentGenerator.Of createErrorContentFromErrorData
    
    let createSimpleErrorStanza errorType errorCondition (rawElem : IStanza) = 
        Stanza<_>.Create (errorContentGenerator, rawElem.Header.AsErrorStanza, (StanzaErrorData.CreateSimpleErrorData errorType errorCondition))

    let validateFail (err : StanzaError) msg = raise <| StanzaValidationException(err, msg)
    
    let customValidateFail errorType errorCondition (rawElem : IStanza) msg = validateFail (createSimpleErrorStanza errorType errorCondition rawElem) msg
    let createBadRequest (rawElem : IStanza) = createSimpleErrorStanza StanzaErrorType.Modify StanzaErrorConditon.BadRequest rawElem
    let badRequestValidateFail (rawElem : IStanza) msg = validateFail (createBadRequest rawElem) msg
    

/// Used when we want to return a stanza error
type StanzaException with
    static member CreateBadRequest header =
        Stanza<StanzaErrorData>.Create (StanzaException.errorContentGenerator, header, StanzaErrorData.BadRequest)

    static member Create error = StanzaException(error)
    static member Create (errorType, errorCondition, header : StanzaHeader) =
        StanzaException (Stanza<_>.Create (StanzaException.errorContentGenerator, header.AsErrorStanza, (StanzaErrorData.CreateSimpleErrorData errorType errorCondition)))
    static member Create (errorType, errorCondition, rawElem : IStanza) =
        StanzaException.Create(errorType, errorCondition, rawElem.Header)
        
    static member Raise error = raise <| StanzaException.Create(error)
    static member Raise (errorType, errorCondition, header : StanzaHeader) = raise <| StanzaException.Create(errorType, errorCondition, header)
    static member Raise (errorType, errorCondition, rawElem : IStanza) = raise <| StanzaException.Create(errorType, errorCondition, rawElem)

[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module StanzaParseException = 
    let parseExn (elem : IStanza) msg = StanzaParseException(elem, msg)
    let parseExnInner (elem : IStanza) inner (msg : string) = StanzaParseException(elem, msg, inner)
    let parseExnf elem fmt = Printf.ksprintf (parseExn elem) fmt
    let parseExnInnerf elem inner fmt = Printf.ksprintf (parseExnInner elem inner) fmt
    let parseFail (elem : IStanza) msg = raise <| StanzaParseException(elem, msg)
    let parseFailInner (elem : IStanza) inner (msg : string) = raise <| StanzaParseException(elem, msg, inner)
    let parseFailf elem fmt = Printf.ksprintf (parseFail elem) fmt
    let parseFailInnerf elem inner fmt = Printf.ksprintf (parseFailInner elem inner) fmt
    
    let elemExn (elem : XElement) msg = StanzaParseException(elem, msg)
    let elemExnInner (elem : XElement) inner (msg : string) = StanzaParseException(elem, msg, inner)
    let elemExnf elem fmt = Printf.ksprintf (elemExn elem) fmt
    let elemExnInnerf elem inner fmt = Printf.ksprintf (elemExnInner elem inner) fmt
    let elemFail (elem : XElement) msg = raise <| StanzaParseException(elem, msg)
    let elemFailInner (elem : XElement) inner (msg : string) = raise <| StanzaParseException(elem, msg, inner)
    let elemFailf elem fmt = Printf.ksprintf (elemFail elem) fmt
    let elemFailInnerf elem inner fmt = Printf.ksprintf (elemFailInner elem inner) fmt

module Parsing = 
    let stanzasNs = StanzaException.stanzasNs
    
    open System.Xml.Linq
    open Yaaf.Xml

    open StanzaParseException
        
    let replaceNamespace oldNs newNs (elem:XElement) = 
        let copy = new XElement(elem)
        for e in copy.DescendantNodesAndSelf() do
            match e with
            | :? XElement as e ->
                if e.Name.NamespaceName = oldNs then
                    e.Name <- XName.Get(e.Name.LocalName, newNs)
                let atList = e.Attributes() |> Seq.toList
                e.Attributes().Remove()
                
                for a in atList do
                    if not <| System.String.IsNullOrEmpty oldNs && a.Name.NamespaceName = oldNs then
                        e.Add(new XAttribute(XName.Get(e.Name.LocalName, newNs), a.Value))
                    elif a.IsNamespaceDeclaration && a.Value = oldNs then
                        e.Add(new XAttribute(a.Name, newNs))
                    else
                        e.Add(new XAttribute(a))
                ()
            | _ -> ()
        copy
    let cleanFromStreamNamespace ns = replaceNamespace ns KnownStreamNamespaces.abstractStreamNS
    let reAddStreamNamespace ns = replaceNamespace KnownStreamNamespaces.abstractStreamNS ns

    let isStanzaElement ns (elem : StreamElement) = 
        match elem.Name.NamespaceName, elem.Name.LocalName with
        | Equals ns, "iq" | Equals ns, "message" | Equals ns, "presence" -> true
        | _ -> false
    
    let createAttributes (info : IStanza) = 
        let header = info.Header
        [ yield! info.Contents.CustomAttributes
          if header.From.IsSome then yield XAttribute(getXName "from" "", header.From.Value.FullId)
          if header.To.IsSome then yield XAttribute(getXName "to" "", header.To.Value.FullId)
          if header.Id.IsSome then yield XAttribute(getXName "id" "", header.Id.Value)
          if header.Type.IsSome then yield XAttribute(getXName "type" "", header.Type.Value) ]
    
    let createStanzaElement ns (stanza : IStanza) = 
        let childs = (createAttributes stanza |> List.map (fun c -> c :> obj)) |> List.append (stanza.Contents.Children |> List.map (fun c -> c :> obj))
        
        let name = 
            match stanza.Header.StanzaType with
            | Iq _ -> "iq"
            | Message _ -> "message"
            | Presence _ -> "presence"
        getXElemWithChilds (getXName name KnownStreamNamespaces.abstractStreamNS) childs
        |> reAddStreamNamespace ns

    /// parses an streamelement to an RawStanza
    let parseInfo childs stanzaType (elem : StreamElement) : Stanza = 
        let attributes = elem.Attributes() |> Seq.toList
        
        let findAttribute attName = 
            match elem.Attribute(getXName attName "") with
            | null -> None
            | s -> Some s
        
        let fromAttr = findAttribute "from"
        let toAttr = findAttribute "to"
        let typeAttr = findAttribute "type"
        let idAttr = findAttribute "id"
        let parsed = [ fromAttr; toAttr; typeAttr; idAttr ] |> List.choose id
        
        let others = 
            attributes
            |> List.filter (fun a -> 
                   parsed
                   |> List.exists (fun p -> p = a)
                   |> not)
            // don't save the namespace (or we get xmlexceptions for duplicate default namespaces)
            |> List.filter (fun a -> not (System.String.IsNullOrEmpty a.Name.NamespaceName) || a.Name.LocalName <> "xmlns")
        
        let mapAttributeToValue (att : XAttribute) = att.Value
        { Header = 
              { To = 
                    toAttr
                    |> Option.map mapAttributeToValue
                    |> Option.map JabberId.Parse
                From = 
                    fromAttr
                    |> Option.map mapAttributeToValue
                    |> Option.map JabberId.Parse
                Id = idAttr |> Option.map mapAttributeToValue
                Type = typeAttr |> Option.map mapAttributeToValue
                StanzaType = stanzaType }
          Contents = 
              { Children = childs
                CustomAttributes = others } } |> Stanza.OfCreator


    let parseStanzaElementNoError ns (elem : StreamElement) = 
        match elem.Name.NamespaceName with
        | Equals ns -> 
            match elem.Name.LocalName with
            | "iq" | "message" | "presence" as name -> 
                let stanzaType = 
                    match name with
                    | "iq" -> Iq
                    | "message" -> Message
                    | "presence" -> Presence
                    | _ -> invalidOp "invalid stanza type (this should not happen)"

                let cleanElem = cleanFromStreamNamespace ns elem
                let childs = cleanElem.Elements() |> Seq.toList
                let info = parseInfo childs stanzaType cleanElem
                info
            | _ -> invalidOp "expected xml stanza"
        | _ -> invalidOp "expected xml stanza"
    type F =
        abstract member Failwith : string -> 'a
    let parseErrorStanzaData ns (my:F) (errorChild : XElement) (faultXml : XElement list) = 
        // To Prevent looping we throw a ReceivedStanzaException even if we can't fully parse it (my.Failwith)
        match errorChild.Name.NamespaceName, errorChild.Name.LocalName with
        | Equals ns, "error" -> ()
        | _ -> my.Failwith "expected last child to be an error child (within an error stanza)"
        // all other childs are sender xml
        let tryGetValue (s : XAttribute) = Option.fromNullable s |> Option.map (fun s -> s.Value)
        
        let errorType = 
            match errorChild.Attribute(getXName "type" "") |> tryGetValue with
            | None -> my.Failwith "expected type attribute in error child"
            | Some s -> 
                try 
                    StanzaErrorType.Parse s
                with MatchFailureException _ -> my.Failwith "invalid type attribute in error child: expected auth, cancel, continue, modify or wait"
        
        let generator = errorChild.Attribute(getXName "by" "") |> tryGetValue
        let innerChilds = errorChild.Elements() |> Seq.toList
        let innerChildCount = innerChilds |> List.length
        if innerChildCount <= 0 then my.Failwith "expected at least a defined error condition"
        let definedConditionElem = innerChilds |> List.head
        
        let definedCondition = 
            match definedConditionElem.Name.NamespaceName with
            | Equals stanzasNs -> StanzaErrorConditon.Parse definedConditionElem.Name.LocalName
            | _ -> my.Failwith "expected the first error child element to be a defined condition"
        
        let textOrApplicationElem = 
            if (innerChildCount > 1) then 
                // text or application element
                let textOrApplicationElem = 
                    innerChilds
                    |> Seq.skip 1
                    |> Seq.head
                match textOrApplicationElem.Name.NamespaceName, textOrApplicationElem.Name.LocalName with
                | Equals stanzasNs, "text" -> 
                    // text elemt
                    match textOrApplicationElem.Attribute(getXName "lang" xmlNs) |> tryGetValue with
                    | None -> my.Failwith "expected xml:lang attribute in text child"
                    | Some s -> Some <| Choice1Of2(textOrApplicationElem.Value, s)
                | _ -> 
                    // application elem
                    Some <| Choice2Of2 textOrApplicationElem
            else None
        
        let text = 
            match textOrApplicationElem with
            | Some(Choice1Of2(text, lang)) -> Some(text, Langcode.OfString lang)
            | _ -> None
        
        let applicationElem = 
            match textOrApplicationElem with
            | Some(Choice2Of2 elem) -> Some elem
            | Some _ -> 
                // Maybe it is the third?
                innerChilds
                |> Seq.skip 2
                |> Seq.tryHead
            | None -> None
        
        { // Type : string option, always "error"
          By = generator
          FaultXml = faultXml
          ErrorType = errorType
          Condition = definedCondition
          /// Text and langcode
          Text = text
          ApplicationSpecific = applicationElem }
    
    let handleStanzaErrors (rawStanza : Stanza) =
        // To Prevent looping we throw a ReceivedStanzaException even if we can't fully parse it 
        let myFailwith msg = 
            raise <| new ReceivedStanzaException(StanzaException.CreateBadRequest rawStanza.Header, msg, rawStanza)
        if rawStanza.Header.Type.IsSome && rawStanza.Header.Type.Value = "error" then 
            let childs = rawStanza.Contents.Children
            let childCount = childs |> Seq.length
            if childCount <= 0 then myFailwith "expected at least an error children in an error stanza"
            let errorChild = childs |> Seq.last
            
            // Throw
            let errorData = 
                parseErrorStanzaData 
                    KnownStreamNamespaces.abstractStreamNS
                    { new F with member x.Failwith msg = myFailwith msg }
                    errorChild 
                    (childs
                        |> Seq.take (childCount - 1)
                        |> Seq.toList)

            let error : StanzaError = Stanza<_>.Create(rawStanza, errorData)
            raise <| new ReceivedStanzaException(error, rawStanza)
        rawStanza
    
    let validateStanza (rawStanza : Stanza) = 
        // validate stanza
        let myValidateFail = StanzaException.badRequestValidateFail rawStanza
        if rawStanza.Header.StanzaType = XmlStanzaType.Iq then 
            // 8.2.3.  IQ Semantics
            let childCount = rawStanza.Contents.Children |> Seq.length
            match rawStanza.Header.Type with
            | Some t -> 
                if t <> "get" && t <> "set" && t <> "result" && t <> "error" then myValidateFail "iq stanza must have one of the four types [get; set; result; error]!"
                if (t = "set" || t = "get") && childCount <> 1 then myValidateFail "iq stanza with get or set type must have one child element!"
                if (t = "result") && childCount > 1 then myValidateFail "iq stanza with result type must have zero or one child elements!"
            | None -> myValidateFail "iq stanza has to have type attribute"
        rawStanza
    
    
    let parseStanzaElement ns (elem : StreamElement) = 
        elem
        |> parseStanzaElementNoError ns
        |> handleStanzaErrors
        |> validateStanza
    
    let isGenericStanza ns (contentCheck) elem = 
        if isStanzaElement ns elem then 
            let stanza = parseStanzaElement ns elem
            contentCheck stanza
        else false
    
    let parseGenericStanza ns contentParser elem = 
        let stanza = parseStanzaElement ns elem
        let parsed = contentParser stanza
        Stanza<_>.Create (stanza, parsed)
    
    let createGenericStanza ns (contentParser : ContentGenerator<_>) stanza = 
        Stanza<_>.Create (stanza, (contentParser.Generate stanza))


    
    