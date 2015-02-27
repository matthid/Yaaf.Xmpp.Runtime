// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Test.Yaaf.Xmpp.Runtime

open Yaaf.Helper
open Yaaf.TestHelper
open Yaaf.Xmpp.Runtime
open System.IO
open System.Xml.Linq
open NUnit.Framework
open FsUnit
open Swensen.Unquote
open System

/// Specification for the IXmppStream interface
[<TestFixture>]
type TestIXmlStream() = 
    inherit Yaaf.TestHelper.MyTestClass()
    let mutable stream = Unchecked.defaultof<IXmlStream>
    let openInfo = XElement.Parse ("<dummy dummyattr='test' />")
    
    let writeStart elem = stream.WriteStart elem |> Async.StartAsTask |> waitTask
    let writeEnd () = stream.WriteEnd () |> Async.StartAsTask |> waitTask
    let readStart () = stream.ReadStart()  |> Async.StartAsTask |> waitTask
    
    let write elem = stream.Write elem  |> Async.StartAsTask |> waitTask
    let read () = stream.TryRead ()  |> Async.StartAsTask |> waitTask
    let xmlEqual = Yaaf.Xml.Core.equalXNode
    abstract member CreateXmlStream : unit -> IXmlStream
    default x.CreateXmlStream () = 
        let provider = Yaaf.TestHelper.StreamTestProvider()
        provider.Setup()
        Yaaf.Xmpp.IOStreamHelpers.defaultStreamBackend provider.Stream1
    
    override this.Setup() = 
        base.Setup()
        stream <- this.CreateXmlStream()

    override this.TearDown() = 
        base.TearDown()

    [<Test>]
    member this.``check that writing works``() = 
        let elem = (new XElement (XName.Get("Test")))
        write elem

    [<Test>]
    member this.``check that reading works even for manual start end end tag``() = 
        writeStart openInfo 
        writeEnd()
        let data = read ()
        test <@ data.IsSome && Yaaf.Xml.Core.equalXNode data.Value openInfo @>


    [<Test>]
    member this.``check that writing and reading works after opening``() = 
        writeStart openInfo
        let elem = (new XElement (XName.Get("Test")))
        write elem
        let readOpenInfo = readStart()
        test <@ xmlEqual readOpenInfo openInfo @>
        let data = read ()
        test <@ data.IsSome && Yaaf.Xml.Core.equalXNode data.Value elem @>
        
    [<Test>]
    member this.``check that we can restart stream``() = 
        writeStart openInfo
        let readOpenInfo = readStart()
        test <@ xmlEqual readOpenInfo openInfo @>

        writeStart openInfo
        let readOpenInfo = readStart()
        test <@ xmlEqual readOpenInfo openInfo @>

        let elem = (new XElement (XName.Get("Test")))
        write elem
        let data = read ()
        test <@ data.IsSome && Yaaf.Xml.Core.equalXNode data.Value elem @>


    
    [<Test>]
    member this.``check that we can close``() = 
        writeStart openInfo
        let readOpenInfo = readStart()
        test <@ xmlEqual readOpenInfo openInfo @>
        
        let elem = (new XElement (XName.Get("Test")))
        write elem
        let data = read ()
        test <@ data.IsSome && Yaaf.Xml.Core.equalXNode data.Value elem @>
        
        writeEnd ()
        let data = read ()
        test <@ data.IsNone @>

        
    
    [<Test>]
    member this.``check that we can't close more often``() = 
        writeStart openInfo
        let readOpenInfo = readStart()
        test <@ xmlEqual readOpenInfo openInfo @>
        
        let elem = (new XElement (XName.Get("Test")))
        write elem
        let data = read ()
        test <@ data.IsSome && Yaaf.Xml.Core.equalXNode data.Value elem @>

        writeEnd ()
        let data = read ()
        test <@ data.IsNone @>
        raises<InvalidOperationException> <@ writeEnd() @>

    [<Test>]
    member this.``check that we can't write elements with childs as start``() = 
        let invalid = XElement.Parse ("<dummy dummyattr='test' ><inner /></dummy>")
        raises<InvalidOperationException> <@ writeStart invalid @>