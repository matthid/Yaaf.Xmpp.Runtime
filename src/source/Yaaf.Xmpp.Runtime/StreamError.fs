// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp

module KnownStreamNamespaces = 
    let streamNS = "http://etherx.jabber.org/streams"
    /// We use this to replace all occurences of stream namespaces temporarly
    let abstractStreamNS = "https://xmpp.yaaf.de/xmppstream"
    let clientNS = "jabber:client"
    let serverNS = "jabber:server"
    let componentAcceptNS = "jabber:component:accept"
    let componentConnectNS = "jabber:component:connect"

namespace Yaaf.Xmpp.Runtime

open Yaaf.FSharp.Collections
open System
open System.Xml.Linq
open Yaaf.Xml
open Yaaf.Helper
open Yaaf.IO
open Yaaf.Logging
open Yaaf.Xmpp

type XmlStreamError = 
    /// The entity has sent XML that cannot be processed. 
    | BadFormat
    /// The entity has sent a namespace prefix that is unsupported, or has sent no namespace prefix on an element that needs such a prefix (see Section 11.2). 
    | BadNamespacePrefix
    /// The server either (1) is closing the existing stream for this entity because a new stream has been initiated that conflicts with the existing stream, or (2) is refusing a new stream for this entity because allowing the new stream would conflict with an existing stream (e.g., because the server allows only a certain number of connections from the same IP address or allows only one server-to-server stream for a given domain pair as a way of helping to ensure in-order processing as described under Section 10.1). 
    | Conflict
    /// One party is closing the stream because it has reason to believe that the other party has permanently lost the ability to communicate over the stream. The lack of ability to communicate can be discovered using various methods, such as whitespace keepalives as specified under Section 4.4, XMPP-level pings as defined in [XEP‑0199], and XMPP Stream Management as defined in [XEP‑0198].
    | ConnectionTimeout
    /// The value of the 'to' attribute provided in the initial stream header corresponds to an FQDN that is no longer serviced by the receiving entity. 
    | HostGone
    /// The value of the 'to' attribute provided in the initial stream header does not correspond to an FQDN that is serviced by the receiving entity. 
    | HostUnknown
    /// A stanza sent between two servers lacks a 'to' or 'from' attribute, the 'from' or 'to' attribute has no value, or the value violates the rules for XMPP addresses [XMPP‑ADDR]. 
    | ImproperAddressing
    /// The server has experienced a misconfiguration or other internal error that prevents it from servicing the stream. 
    | InternalServerError
    /// The data provided in a 'from' attribute does not match an authorized JID or validated domain as negotiated (1) between two servers using SASL or Server Dialback, or (2) between a client and a server via SASL authentication and resource binding. 
    | InvalidFrom
    /// The stream namespace name is something other than "http://etherx.jabber.org/streams" (see Section 11.2) or the content namespace declared as the default namespace is not supported (e.g., something other than "jabber:client" or "jabber:server"). 
    | InvalidNamespace
    /// The entity has sent invalid XML over the stream to a server that performs validation (see Section 11.4). 
    | InvalidXml
    /// The entity has attempted to send XML stanzas or other outbound data before the stream has been authenticated, or otherwise is not authorized to perform an action related to stream negotiation; the receiving entity MUST NOT process the offending data before sending the stream error. 
    | NotAuthorized
    /// The initiating entity has sent XML that violates the well-formedness rules of [XML] or [XML‑NAMES]. 
    | NotWellFormed
    /// The entity has violated some local service policy (e.g., a stanza exceeds a configured size limit); the server MAY choose to specify the policy in the <text/> element or in an application-specific condition element. 
    | PolicyViolation
    /// The server is unable to properly connect to a remote entity that is needed for authentication or authorization (e.g., in certain scenarios related to Server Dialback [XEP‑0220]); this condition is not to be used when the cause of the error is within the administrative domain of the XMPP service provider, in which case the <internal-server-error/> condition is more appropriate. 
    | RemoteConnectionFailed
    /// The server is closing the stream because it has new (typically security-critical) features to offer, because the keys or certificates used to establish a secure context for the stream have expired or have been revoked during the life of the stream (Section 13.7.2.3), because the TLS sequence number has wrapped (Section 5.3.5), etc. The reset applies to the stream and to any security context established for that stream (e.g., via TLS and SASL), which means that encryption and authentication need to be negotiated again for the new stream (e.g., TLS session resumption cannot be used). 
    | Reset
    /// The server lacks the system resources necessary to service the stream. 
    | ResourceConstraint
    /// The entity has attempted to send restricted XML features such as a comment, processing instruction, DTD subset, or XML entity reference (see Section 11.1).
    | RestrictedXml
    /// The server will not provide service to the initiating entity but is redirecting traffic to another host under the administrative control of the same service provider. The XML character data of the <see-other-host/> element returned by the server MUST specify the alternate FQDN or IP address at which to connect, which MUST be a valid domainpart or a domainpart plus port number (separated by the ':' character in the form "domainpart:port"). If the domainpart is the same as the source domain, derived domain, or resolved IPv4 or IPv6 address to which the initiating entity originally connected (differing only by the port number), then the initiating entity SHOULD simply attempt to reconnect at that address. (The format of an IPv6 address MUST follow [IPv6‑ADDR], which includes the enclosing the IPv6 address in square brackets '[' and ']' as originally defined by [URI].) Otherwise, the initiating entity MUST resolve the FQDN specified in the <see-other-host/> element as described under Section 3.2.
    | SeeOtherHost
    /// The server is being shut down and all active streams are being closed. 
    | SystemShutdown
    /// The error condition is not one of those defined by the other conditions in this list; this error condition SHOULD NOT be used except in conjunction with an application-specific condition. 
    | UndefinedCondition
    /// The initiating entity has encoded the stream in an encoding that is not supported by the server (see Section 11.6) or has otherwise improperly encoded the stream (e.g., by violating the rules of the [UTF‑8] encoding). 
    | UnsupportedEncoding
    /// The receiving entity has advertised a mandatory-to-negotiate stream feature that the initiating entity does not support, and has offered no other mandatory-to-negotiate feature alongside the unsupported feature. 
    | UnsupportedFeature
    /// The initiating entity has sent a first-level child of the stream that is not supported by the server, either because the receiving entity does not understand the namespace or because the receiving entity does not understand the element name for the applicable namespace (which might be the content namespace declared as the default namespace). 
    | UnsupportedStanzaType
    /// The 'version' attribute provided by the initiating entity in the stream header specifies a version of XMPP that is not supported by the server. 
    | UnsupportedVersion
    | UnknownError of string
    
    static member Parse s = 
        match s with
        | "bad-format" -> BadFormat
        | "bad-namespace-prefix" -> BadNamespacePrefix
        | "conflict" -> Conflict
        | "connection-timeout" -> ConnectionTimeout
        | "host-gone" -> HostGone
        | "host-unknown" -> HostUnknown
        | "improper-addressing" -> ImproperAddressing
        | "internal-server-error" -> InternalServerError
        | "invalid-from" -> InvalidFrom
        | "invalid-namespace" -> InvalidNamespace
        | "invalid-xml" -> InvalidXml
        | "not-authorized" -> NotAuthorized
        // Interoperability Note: In RFC 3920, the name of this error condition was "xml-not-well-formed" instead of "not-well-formed". The name was changed because the element name <xml-not-well-formed/> violates the constraint from Section 3 of [XML] that "names beginning with a match to (('X'|'x')('M'|'m')('L'|'l')) are reserved for standardization in this or future versions of this specification". 
        | "xml-not-well-formed" | "not-well-formed" -> NotWellFormed
        | "policy-violation" -> PolicyViolation
        | "remote-connection-failed" -> RemoteConnectionFailed
        | "reset" -> Reset
        | "resource-constraint" -> ResourceConstraint
        | "restricted-xml" -> RestrictedXml
        | "see-other-host" -> SeeOtherHost
        | "system-shutdown" -> SystemShutdown
        | "undefined-condition" -> UndefinedCondition
        | "unsupported-encoding" -> UnsupportedEncoding
        | "unsupported-feature" -> UnsupportedFeature
        | "unsupported-stanza-type" -> UnsupportedStanzaType
        | "unsupported-version" -> UnsupportedVersion
        | _ -> UnknownError s
    
    member s.XmlString = 
        match s with
        | BadFormat -> "bad-format"
        | BadNamespacePrefix -> "bad-namespace-prefix"
        | Conflict -> "conflict"
        | ConnectionTimeout -> "connection-timeout"
        | HostGone -> "host-gone"
        | HostUnknown -> "host-unknown"
        | ImproperAddressing -> "improper-addressing"
        | InternalServerError -> "internal-server-error"
        | InvalidFrom -> "invalid-from"
        | InvalidNamespace -> "invalid-namespace"
        | InvalidXml -> "invalid-xml"
        | NotAuthorized -> "not-authorized"
        // Interoperability Note: In RFC 3920, the name of this error condition was "xml-not-well-formed" instead of "not-well-formed". The name was changed because the element name <xml-not-well-formed/> violates the constraint from Section 3 of [XML] that "names beginning with a match to (('X'|'x')('M'|'m')('L'|'l')) are reserved for standardization in this or future versions of this specification". 
        //| "xml-not-well-formed"
        | NotWellFormed -> "not-well-formed"
        | PolicyViolation -> "policy-violation"
        | RemoteConnectionFailed -> "remote-connection-failed"
        | Reset -> "reset"
        | ResourceConstraint -> "resource-constraint"
        | RestrictedXml -> "restricted-xml"
        | SeeOtherHost -> "see-other-host"
        | SystemShutdown -> "system-shutdown"
        | UndefinedCondition -> "undefined-condition"
        | UnsupportedEncoding -> "unsupported-encoding"
        | UnsupportedFeature -> "unsupported-feature"
        | UnsupportedStanzaType -> "unsupported-stanza-type"
        | UnsupportedVersion -> "unsupported-version"
        | UnknownError s -> s
    member x.GetMessage msg = 
        match msg with
        | Some msg -> msg
        | None -> x.XmlString
/// This class represents errors within the xmpp-stream.
/// When thrown it is a request to the runtime to send the stream error and close the runtime. (can also be used be plugins)
[<System.Serializable>]
type StreamErrorException = 
    // Save in data map
    val private text : string option
    val private extraInfo : XElement list
    val private errorType : XmlStreamError
    inherit Exception
    
    new(msg : XmlStreamError, t : string option, e : XElement list) = 
        { inherit Exception(msg.GetMessage t)
          text = t
          errorType = msg
          extraInfo = e }
    new(msg : XmlStreamError, t : string option, e : XElement list, inner : exn) = 
        { inherit Exception(msg.GetMessage t, inner)
          text = t
          errorType = msg
          extraInfo = e }
    
    new(info : System.Runtime.Serialization.SerializationInfo, context : System.Runtime.Serialization.StreamingContext) = 
        { text = None
          errorType = UnknownError "created from serialization?"
          extraInfo = [] }
    
    //member x.Type with get() = XmlStreamError.Parse x.Message
    member x.Text with get () = x.text
    member x.ExtraInfo with get () = x.extraInfo
    member x.ErrorType with get () = x.errorType

module StreamError = 
    let xmppStreamsNS = "urn:ietf:params:xml:ns:xmpp-streams"
    let create failure = StreamErrorException(failure, None, [])
    let createinner failure inner = StreamErrorException(failure, None, [], inner)
    let createmsg failure msg = StreamErrorException(failure, Some msg, [])
    let createinnermsg failure inner msg = StreamErrorException(failure, Some msg, [], inner)
    let createf failure fmt = Printf.ksprintf (createmsg failure) fmt
    let createinternal msg = createmsg InternalServerError msg
    let createinternalf fmt = Printf.ksprintf createinternal fmt
    let createundefined msg = createmsg UndefinedCondition msg
    let createundefinedf fmt = Printf.ksprintf createinternal fmt
    
    let fail failure = raise <| StreamErrorException(failure, None, [])
    let failinner failure inner = raise <| StreamErrorException(failure, None, [], inner)
    let failmsg failure msg = raise <| StreamErrorException(failure, Some msg, [])
    let failinnermsg failure inner msg = raise <| StreamErrorException(failure, Some msg, [], inner)
    let failf failure fmt = Printf.ksprintf (failmsg failure) fmt
    let failinternal msg = failmsg InternalServerError msg
    let failinternalf fmt = Printf.ksprintf failinternal fmt
    let failundefined msg = failmsg UndefinedCondition msg
    let failundefinedf fmt = Printf.ksprintf failinternal fmt
    
    let parseStreamError (elem : XElement) = 
        let first = elem.Elements() |> Seq.head
        let isTextElem (e : XElement) = e.Name.LocalName = "text" && e.Name.NamespaceName = xmppStreamsNS
        
        let text = 
            elem.Elements()
            |> Seq.filter isTextElem
            |> Seq.tryHead
            |> Option.map (fun e -> 
                   e.Value, 
                   e.Attributes()
                   |> Seq.filter (fun a -> a.Name.LocalName = "lang")
                   |> Seq.map (fun a -> a.Value)
                   |> Seq.tryHead)
        match first.Name.NamespaceName with
        | Equals xmppStreamsNS -> 
            StreamErrorException(XmlStreamError.Parse first.Name.LocalName, text |> Option.map fst, 
                                 elem.Elements()
                                 |> Seq.skip 1
                                 |> Seq.filter (not << isTextElem)
                                 |> Seq.toList)
        | _ -> StreamErrorException(UnknownError "unknown-error-element", None, elem.Elements() |> Seq.toList)
    
    let writeStreamError (exn : StreamErrorException) = 
        [ yield getXName (exn.ErrorType.XmlString) xmppStreamsNS |> getXElem
          match exn.Text with
          | Some s -> yield [ s ] |> getXElemWithChilds (getXName "text" xmppStreamsNS)
          | None -> ()
          yield! exn.ExtraInfo ]
        |> getXElemWithChilds (getXName "error" KnownStreamNamespaces.streamNS)
