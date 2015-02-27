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
type ``Test-Yaaf-Xmpp-XmppServer: Check that components can send``() = 
    inherit MyTestClass()
    
    [<Test>]
    member x.``Check if we can send a simple message stanza to another entity``() = 
        Assert.Inconclusive "Not implemented" 
        //xmppClients.[1]
        //    .WriteRaw "<message to='test0@nunit.org' from='blub@comp0.nunit.org' type='chat'><body>test</body></message>" |> x.WaitProcess
        //serverNegotiatedClient.[1] |> waitTaskI "client 1 connected to server"
        //xmppClients.[0]
        //    .WriteRaw "<message from='test0@nunit.org' to='blub@comp0.nunit.org' type='chat'><body>test</body></message>" |> x.WaitProcess
        //let stanza, receiver = result_0 |> waitTaskI "result"
        //stanza.Header.Type |> should be (equal (Some "chat"))
        //stanza.Header.StanzaType |> should be (equal XmlStanzas.XmlStanzaType.Message)
        //stanza.Header.From.IsSome |> should be True
        //stanza.Header.From.Value.BareId |> should be (equal "blub@comp0.nunit.org")
        //stanza.Header.To.IsSome |> should be True
        //stanza.Header.To.Value.BareId |> should be (equal "test0@nunit.org")
        //let stanza, receiver = result_1 |> waitTaskI "result"
        //stanza.Header.Type |> should be (equal (Some "chat"))
        //stanza.Header.StanzaType |> should be (equal XmlStanzas.XmlStanzaType.Message)
        //stanza.Header.From.IsSome |> should be True
        //stanza.Header.From.Value.BareId |> should be (equal "test0@nunit.org")
        //stanza.Header.To.IsSome |> should be True
        //stanza.Header.To.Value.BareId |> should be (equal "blub@comp0.nunit.org")
    
    [<Test>]
    member x.``Check if from is checked (invalid from)``() = 
        Assert.Inconclusive "Not implemented" 
        //xmppClients.[1]
        //    .WriteRaw "<message to='test0@nunit.org' from='blub@invalid.nunit.org' type='chat'><body>test</body></message>" |> x.WaitProcess
        //serverNegotiatedClient.[1] |> waitTaskI "client 1 connected to server"
        //let error = clientExitedEvents.[1] |> waitTaskI "stream closed, invalid from"
        //match error with
        //| None -> failwith "should receive invalid-from error"
        //| Some exn -> 
        //    match exn with
        //    | :? StreamFailException as fail -> 
        //        let error = fail.InnerException :?> StreamErrorException
        //        error.ErrorType |> should be (equal XmlStreamError.InvalidFrom)
        //    | _ -> failwith "expected StreamFailException"
    
    [<Test>]
    member x.``Check if from is checked (improper addressing)``() = 
        Assert.Inconclusive "Not implemented" 
        // Wait for negotiation events, so that server can actually deliver the message
        //xmppClients.[1].WriteRaw "<message to='test0@nunit.org' type='chat'><body>test</body></message>" |> x.WaitProcess
        //serverNegotiatedClient.[1] |> waitTaskI "client 1 connected to server"
        //let error = clientExitedEvents.[1] |> waitTaskI "stream closed, improper from"
        //match error with
        //| None -> failwith "should receive improper-addressing error"
        //| Some exn -> 
        //    match exn with
        //    | :? StreamFailException as fail -> 
        //        let error = fail.InnerException :?> StreamErrorException
        //        error.ErrorType |> should be (equal XmlStreamError.ImproperAddressing)
        //    | _ -> failwith "expected StreamFailException"
            
    [<Test>]
    member x.``Check if to is checked (host gone)``() = 
        Assert.Inconclusive "Not implemented" 
        // Wait for negotiation events, so that server can actually deliver the message
        //xmppClients.[1].WriteRaw "<message to='test0@unknown.nunit.org' from='blub@comp0.nunit.org' type='chat'><body>test</body></message>" |> x.WaitProcess
        //serverNegotiatedClient.[1] |> waitTaskI "client 1 connected to server"
        //let error = clientExitedEvents.[1] |> waitTaskI "stream closed, improper from"
        //match error with
        //| None -> failwith "should receive host-unknown error"
        //| Some exn -> 
        //    match exn with
        //    | :? StreamFailException as fail -> 
        //        let error = fail.InnerException :?> StreamErrorException
        //        error.ErrorType |> should be (equal XmlStreamError.HostUnknown)
        //    | _ -> failwith "expected StreamFailException"
            
    [<Test>]
    member x.``Check if to is checked (improper-addressing)``() = 
        Assert.Inconclusive "Not implemented" 
        // Wait for negotiation events, so that server can actually deliver the message
        //xmppClients.[1].WriteRaw "<message from='blub@comp0.nunit.org' type='chat'><body>test</body></message>" |> x.WaitProcess
        //serverNegotiatedClient.[1] |> waitTaskI "client 1 connected to server"
        //let error = clientExitedEvents.[1] |> waitTaskI "stream closed, improper from"
        //match error with
        //| None -> failwith "should receive host-unknown error"
        //| Some exn -> 
        //    match exn with
        //    | :? StreamFailException as fail -> 
        //        let error = fail.InnerException :?> StreamErrorException
        //        error.ErrorType |> should be (equal XmlStreamError.ImproperAddressing)
        //    | _ -> failwith "expected StreamFailException"