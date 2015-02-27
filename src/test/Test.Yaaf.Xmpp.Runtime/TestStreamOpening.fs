// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Test.Yaaf.Xmpp.Runtime

open Yaaf.Xml
open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.OpenHandshake
open System.IO
open System.Xml.Linq
open NUnit.Framework
open FsUnit
open Swensen.Unquote
open Yaaf.TestHelper

/// Specification for the IXmppStream interface
[<TestFixture>]
type TestStreamOpening() = 
    inherit Yaaf.TestHelper.MyTestClass()
    
    [<Test>]
    member this.``check that openinfo can be converted to XElement``() = 
        let elem = fromOpenInfo StreamOpenInfo.Empty
        let expected = XElement.Parse (sprintf "<stream:stream xmlns:stream='%s' xmlns='%s' />" KnownStreamNamespaces.streamNS KnownStreamNamespaces.clientNS)
        test <@ equalXNode elem expected @>

    [<Test>]
    member this.``check that XElement can be converted to openinfo``() = 
        let elem = 
            XElement.Parse 
                (sprintf "<stream:stream xmlns:stream='%s' xmlns='%s' />" KnownStreamNamespaces.streamNS KnownStreamNamespaces.clientNS)
        let expected = StreamOpenInfo.Empty
        let actual = toOpenInfo elem
        test <@ actual = expected @>

    [<Test>]
    member this.``check that BOM is not sent``() = 
        let provider = StreamTestProvider()
        provider.Setup()
        let backend = IOStreamHelpers.defaultStreamBackend provider.Stream1
        
        let elem = 
            XElement.Parse 
                (sprintf "<stream:stream xmlns:stream='%s' xmlns='%s' />" KnownStreamNamespaces.streamNS KnownStreamNamespaces.clientNS)
        backend.WriteStart elem |> Async.StartAsTask |> waitTask
        let buff = Array.zeroCreate 3
        let read = provider.Stream1.Read(buff, 0, buff.Length)
        test <@ read = 3 @>
        let expectedBytes1 = System.Text.UTF8Encoding(false).GetBytes "<stream:stream"
        let expectedBytes2 = System.Text.UTF8Encoding(false).GetBytes "<?xml"
        let mutable is1 = true
        let mutable is2 = true
        for i in 0..2 do
            if buff.[i] <> expectedBytes1.[i] then is1 <- false
            if buff.[i] <> expectedBytes2.[i] then is2 <- false
        test <@ is1 || is2 @>

        provider.TearDown()