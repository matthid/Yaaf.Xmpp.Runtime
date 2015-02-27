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
open Yaaf.Xmpp.Runtime.OpenHandshake
open System.Threading.Tasks
open Yaaf.IO
open Yaaf.TestHelper
open Foq
open Swensen.Unquote

[<TestFixture>]
type ``Test-Yaaf-Xmpp-Runtime-OpenHandshake: Check if openHandshake function works property``() =
    inherit MyTestClass()

    // Executes a basic openhandshake with the given client and server setups, returns the element returned by the server
    let baseServerTest clientOpenInfo serverOpenInfo =
        let serverReturned = ref None
        let runtimeConfig =
            Mock<IRuntimeConfig>()
                .Setup(fun x -> <@ x.StreamType @>).Returns(StreamType.ClientStream)
                .Setup(fun x -> <@ x.IsInitializing @>).Returns(false)
                .Create()
        let xmlStream =
            Mock<IXmlStream>()
                .Setup(fun x -> <@ x.ReadStart() @>).Returns(async.Return (fromOpenInfo clientOpenInfo))
                .Setup(fun x -> <@ x.WriteStart(any()) @>)//.Calls<_>((fun arg -> async.Return ()))
                .Calls<OpenElement>(fun start -> 
                        serverReturned := Some (toOpenInfo start)
                        async.Return ())
                .Create()
        let returnedClientInfo = openHandshake runtimeConfig serverOpenInfo xmlStream |> Async.StartAsTask |> waitTask
        test <@ returnedClientInfo = clientOpenInfo @>
        let serverReturnedInfo = !serverReturned
        test <@ serverReturnedInfo.IsSome @>
        serverReturnedInfo.Value
        
    [<Test>]
    member this.``Server should not include version attribute when client did not send one`` () = 
        let clientOpenInfo = StreamOpenInfo.Empty
        let serverOpenInfo = StreamOpenInfo.Empty

        let serverReturnedInfo = baseServerTest clientOpenInfo serverOpenInfo

        test <@ serverReturnedInfo.Version = None @>

    [<Test>]
    member this.``Server should mirror from to to attribute`` () = 
        let myFrom = Some (JabberId.Parse "test@nunit.org")
        let clientOpenInfo = { StreamOpenInfo.Empty with From = myFrom}
        let serverOpenInfo = StreamOpenInfo.Empty
        
        let serverReturnedInfo = baseServerTest clientOpenInfo serverOpenInfo
        
        
        serverReturnedInfo.To |> should be (equal myFrom)

        
    /// 4.9.1.2.  Stream Errors Can Occur During Setup

    [<Test>]
    member this.``Server should close properly on invalid start element`` () = 
        Assert.Inconclusive ("This Xmpp Core specification is currently not properly implemented!")
        let serverOpenInfo = StreamOpenInfo.Empty

        let serverReturned = ref None
        let runtimeConfig =
            Mock<IRuntimeConfig>()
                .Setup(fun x -> <@ x.StreamType @>).Returns(StreamType.ClientStream)
                .Setup(fun x -> <@ x.IsInitializing @>).Returns(false)
                .Create()
        let xmlStream =
            Mock<IXmlStream>()
                .Setup(fun x -> <@ x.ReadStart() @>).Raises(new XmlException("Start tag is invalid"))
                .Setup(fun x -> <@ x.WriteStart(any()) @>)//.Calls<_>((fun arg -> async.Return ()))
                .Calls<OpenElement>(fun start -> 
                        serverReturned := Some (toOpenInfo start)
                        async.Return ())
                .Create()
        // it does of course fail (which is later handled by closing the stream)
        raises<XmlException> <@ openHandshake runtimeConfig serverOpenInfo xmlStream |> Async.StartAsTask |> waitTask @>
        // But we still have to try to WRITE our openInfo! (as specified in XMPP Core)
        let serverReturnedInfo = !serverReturned
        test <@ serverReturnedInfo.IsSome @>