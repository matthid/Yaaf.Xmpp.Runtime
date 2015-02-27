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
open Test.Yaaf.Xmpp
open Test.Yaaf.Xmpp.DevelopmentCertificate
open Yaaf.Helper
open Yaaf.Xmpp
open Yaaf.IO
open Yaaf.Xmpp.Server
open Yaaf.Xmpp.Stream
open System.Threading.Tasks
open Yaaf.TestHelper
open Yaaf.Logging
open Foq
open Swensen.Unquote

[<TestFixture>]
type ``Test-Yaaf-Xmpp-Server-XmppServer: Check that sending across entities works``() =
    inherit MyTestClass()

    [<Test>]
    member x.``Check if we can send a simple message stanza to another entity`` () =
        Assert.Inconclusive "Not implemented" 
        //xmppClients.[0].WriteRaw "<message to='test1@nunit.org' from='test0@nunit.org' type='chat'><body>test</body></message>" |> x.WaitProcess
        //let stanza, receiver  = result |> waitTaskI "result" 
        //stanza.Header.Type |> should be (equal (Some "chat"))
        //stanza.Header.StanzaType |> should be (equal XmlStanzas.XmlStanzaType.Message)
        //stanza.Header.From.IsSome |> should be True
        //stanza.Header.From.Value.BareId |> should be (equal "test0@nunit.org")
        //stanza.Header.To.IsSome |> should be True
        //stanza.Header.To.Value.BareId |> should be (equal "test1@nunit.org")

    [<Test>]
    member x.``Check that a correct from header is added to message`` () =
        Assert.Inconclusive "Not implemented" 
        //xmppClients.[0].WriteRaw "<message to='test1@nunit.org' type='chat'><body>test</body></message>"|> x.WaitProcess
        //let stanza, receiver  = result |> waitTaskI "result" 
        //stanza.Header.Type |> should be (equal (Some "chat"))
        //stanza.Header.StanzaType |> should be (equal XmlStanzas.XmlStanzaType.Message)
        //stanza.Header.From.IsSome |> should be True
        //stanza.Header.From.Value.BareId |> should be (equal "test0@nunit.org")
        //stanza.Header.To.IsSome |> should be True
        //stanza.Header.To.Value.BareId |> should be (equal "test1@nunit.org")

    [<Test>]
    member x.``Check that unknown contents are delivered`` () =
        Assert.Inconclusive "Not implemented" 
//        xmppClients.[0].WriteRaw "<message to='test1@nunit.org' type='chat'>
//<body>test</body>
//<unknown xmlns='http://yaaf.de/unknown'>test</unknown>
//</message>" |> x.WaitProcess
//        let stanza, receiver  = result |> waitTaskI "result" 
//        stanza.Header.Type |> should be (equal (Some "chat"))
//        stanza.Header.StanzaType |> should be (equal XmlStanzas.XmlStanzaType.Message)
//        stanza.Header.From.IsSome |> should be True
//        stanza.Header.From.Value.BareId |> should be (equal "test0@nunit.org")
//        stanza.Header.To.IsSome |> should be True
//        stanza.Header.To.Value.BareId |> should be (equal "test1@nunit.org")
//        stanza.Contents.Children |> Seq.length |> should be (equal 2) // body and unknown element

        
    [<Test>]
    member x.``Check that I can send Iq stanzas to myself`` () =
        Assert.Inconclusive "Not implemented" 
//        // This tests the recursive lock strategy in XmppCore -> requestContext from within a context
//        xmppClients.[0].WriteRaw (sprintf "<iq id='ab3fa' to='%s' type='get'>
//<vCard xmlns='vcard-temp' />
//</iq>" boundJid_0.FullId) |> x.WaitProcess
//        let stanza, receiver  = result |> waitTaskI "result" 
//        stanza.Header.Type |> should be (equal (Some "get"))
//        stanza.Header.StanzaType |> should be (equal XmlStanzas.XmlStanzaType.Iq)
//        stanza.Header.From.IsSome |> should be True
//        stanza.Header.From.Value.FullId |> should be (equal boundJid_0.FullId)
//        stanza.Header.To.IsSome |> should be True
//        stanza.Header.To.Value.BareId |> should be (equal "test0@nunit.org")
        
    [<Test>]
    member x.``Check that unknown iq stanzas are responded`` () =
        Assert.Inconclusive "Not implemented" 
//        xmppClients.[0].WriteRaw "<iq to='nunit.org' type='get'>
//<unknown xmlns='http://yaaf.de/unknown'>test</unknown>
//</iq>"      |> x.WaitProcess
//        let stanza, receiver  = result |> waitTaskI "result" 
//        stanza.Header.Type |> should be (equal (Some "error"))
//        stanza.Header.StanzaType |> should be (equal XmlStanzas.XmlStanzaType.Iq)
//        stanza.Header.From.IsSome |> should be True
//        stanza.Header.From.Value.BareId |> should be (equal "nunit.org")
//        stanza.Header.To.IsSome |> should be True
//        stanza.Header.To.Value.BareId |> should be (equal "test0@nunit.org")
//        (fun () -> XmlStanzas.Parsing.handleStanzaErrors "jabber:client" stanza |> ignore)
//            |> should throw typeof<XmlStanzas.ReceivedStanzaException>
            
    [<Test>]
    member x.``Check that unknown contents are delivered and responded by client`` () =
        Assert.Inconclusive "Not implemented" 
//        xmppClients.[0].WriteRaw "<iq to='test1@nunit.org' type='get'>
//<unknown xmlns='http://yaaf.de/unknown'>test</unknown>
//</iq>"      |> x.WaitProcess
//        let stanza, receiver  = result |> waitTaskI "result" 
//        stanza.Header.Type |> should be (equal (Some "error"))
//        stanza.Header.StanzaType |> should be (equal XmlStanzas.XmlStanzaType.Iq)
//        stanza.Header.From.IsSome |> should be True
//        stanza.Header.From.Value.BareId |> should be (equal "test1@nunit.org")
//        stanza.Header.To.IsSome |> should be True
//        stanza.Header.To.Value.BareId |> should be (equal "test0@nunit.org")
//        (fun () -> XmlStanzas.Parsing.handleStanzaErrors "jabber:client" stanza |> ignore)
//            |> should throw typeof<XmlStanzas.ReceivedStanzaException>
       