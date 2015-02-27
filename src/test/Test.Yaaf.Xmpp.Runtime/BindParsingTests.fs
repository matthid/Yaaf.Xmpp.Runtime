// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

namespace Test.Yaaf.Xmpp
open System.Xml
open System.Xml.Linq
open System.IO
open NUnit.Framework
open FsUnit
open Test.Yaaf.Xmpp.TestHelper
open Test.Yaaf.Xml.XmlTestHelper
open Yaaf.Xmpp
open Yaaf.Xmpp.Stream
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.XmlStanzas
open Yaaf.Xmpp.Runtime.Features
open Yaaf.Xmpp.Runtime.OpenHandshake
open System.Threading.Tasks
open Yaaf.IO
open Yaaf.TestHelper
open Foq
open Swensen.Unquote

[<TestFixture>]
type ``Test-Yaaf-Xmpp-Runtime-Features-BindFeature: Check that Parsing works``() =
    inherit XmlStanzaParsingTestClass()
    [<Test>]
    member this.``Check that we can bind empty bind element`` () = 
        let stanza = "<iq id='tn281v37' type='set'>
    <bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/>
   </iq>"
        let info = this.Test stanza
        let elem = ParsingBind.parseContentBind info
        elem |> should be (equal BindElement.Empty)
    [<Test>]
    member this.``Check that empty bind element has the right type`` () = 
        let stanza = "<iq id='tn281v37' type='result'>
    <bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/>
   </iq>"
        let info = this.Test stanza
        (fun () -> ParsingBind.parseContentBind info |> ignore) 
            |> should throw typeof<StanzaValidationException>
    [<Test>]
    member this.``Check that jid bind element has the right type`` () = 
        let stanza = "<iq id='tn281v37' type='set'>
    <bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'>
      <jid>
        juliet@im.example.com/4db06f06-1ea4-11dc-aca3-000bcd821bfb
      </jid>
    </bind>
   </iq>"
        let info = this.Test stanza
        (fun () -> ParsingBind.parseContentBind info |> ignore) 
            |> should throw typeof<StanzaValidationException>
        let stanza = "<iq id='tn281v37' type='get'>
    <bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'>
      <jid>
        juliet@im.example.com/4db06f06-1ea4-11dc-aca3-000bcd821bfb
      </jid>
    </bind>
   </iq>"
        let info = this.Test stanza
        (fun () -> ParsingBind.parseContentBind info |> ignore) 
            |> should throw typeof<StanzaValidationException>
    [<Test>]
    member this.``Check that resource bind element has the right type`` () = 
        let stanza = "<iq id='wy2xa82b4' type='result'>
     <bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'>
       <resource>balcony</resource>
     </bind>
   </iq>"
        let info = this.Test stanza
        (fun () -> ParsingBind.parseContentBind info |> ignore) 
            |> should throw typeof<StanzaValidationException>
    [<Test>]
    member this.``Check that created resource bind element has the right type`` () = 
        let elem = BindElement.Resource "test"
        let stanza = ParsingBind.createBindElement "id" elem
        stanza.Header.Type |> should be (equal (Some "set"))
    [<Test>]
    member this.``Check that created jid bind element has the right type`` () = 
        let elem = BindElement.Jid (JabberId.Parse "test@domain.org")
        let stanza = ParsingBind.createBindElement "id" elem
        stanza.Header.Type |> should be (equal (Some "result"))
    [<Test>]
    member this.``Check that created empty bind element has the right type`` () = 
        let elem = BindElement.Empty
        let stanza = ParsingBind.createBindElement "id" elem
        stanza.Header.Type |> should be (equal (Some "set"))