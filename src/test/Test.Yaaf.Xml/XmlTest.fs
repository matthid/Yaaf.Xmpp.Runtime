// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Test.Yaaf.Xml

open System.IO
open NUnit.Framework
open FsUnit
open System.Xml
open System.Xml.Linq
open Test.Yaaf.Xml.XmlTestHelper
open Yaaf.IO
open Yaaf.Helper
open Yaaf.Xml
open Yaaf.TestHelper
open Swensen.Unquote

type MonoXmlException = Mono.System.Xml.XmlException

type YaafXmlException = Yaaf.Xml.Core.XmlException

[<TestFixture>]
type XmlStreamParser() = 
    inherit MyXmlTestClass()
    
    [<Test>]
    member x.``Check that XmlReader returns finished stuff``() = 
        let reader = x.XmlReader1
        let read = reader.ReadAsync() |> Async.StartAsTask
        x.Write1("<startElem att='test'>")
        read.Wait(-1) |> should be True
        read.Result |> should be True
        reader.NodeType |> should be (equal XmlNodeType.Element)
        reader.Name |> should be (equal "startElem")
        reader.AttributeCount |> should be (equal 1)
        reader.MoveToFirstAttribute() |> should be True
        reader.Name |> should be (equal "att")
        reader.Value |> should be (equal "test")
    
    [<Test>]
    member this.``Check that XmlReader throws invalid xml``() = 
        let read = this.XmlReader1.ReadAsync() |> Async.StartAsTask
        this.Write1("<startElem < att='test'>")
        (fun () -> 
        read
        |> waitTask
        |> ignore)
        |> should throw typeof<MonoXmlException>
    
    [<Test>]
    member this.``Check that XmlReader throws when there is an invalid end-element (readXElementOrClose)``() = 
        let read = readXElementOrClose (this.XmlReader1) |> Async.StartAsTask
        this.Write1("<startElem> <inner> </startElem>")
        (fun () -> 
        read
        |> waitTask
        |> ignore)
        |> should throw typeof<YaafXmlException>
    
    /// I had a bug where the above would not fail but this would (buffersizes...)
    [<Test>]
    member this.``Check that XmlReader throws when there is an invalid end-element (readXElementOrClose) _2``() = 
        let read = readXElementOrClose (this.XmlReader1) |> Async.StartAsTask
        this.Write1("<test > <inner > </test>")
        (fun () -> 
        read
        |> waitTask
        |> ignore)
        |> should throw typeof<YaafXmlException>

[<TestFixture>]
type XmlXElementLoadAsync() = 
    inherit MyXmlTestClass()
    
    [<Test>]
    member this.``Check that XmlReader throws when there is an invalid end-element``() = 
        let read = XElement.LoadAsync(this.XmlReader1) |> Async.StartAsTask
        this.Write1("<startElem> <inner> </startElem>")
        (fun () -> 
        read
        |> waitTask
        |> ignore)
        |> should throw typeof<MonoXmlException>
    
    /// I had a bug where the above would not fail but this would (buffersizes...)
    [<Test>]
    member this.``Check that XmlReader throws when there is an invalid end-element _2``() = 
        let read = XElement.LoadAsync(this.XmlReader1) |> Async.StartAsTask
        this.Write1("<test > <inner > </test>")
        (fun () -> 
        read
        |> waitTask
        |> ignore)
        |> should throw typeof<MonoXmlException>
    
    /// I had a bug where the above would not fail but this would (buffersizes...)
    [<Test>]
    member this.``Check that XmlReader throws when there is an invalid end-element _2 with finish``() = 
        let read = XElement.LoadAsync(this.XmlReader1) |> Async.StartAsTask
        this.Write1("<test > <inner > </test>")
        this.FinishStream1()
        (fun () -> 
        read
        |> waitTask
        |> ignore)
        |> should throw typeof<MonoXmlException>
    
    [<Test>]
    member this.``Check that XmlReader throws when there is an invalid end-element simple``() = 
        let read = XElement.LoadAsync(this.XmlReader1) |> Async.StartAsTask
        this.Write1("<s > <i > </s>")
        (fun () -> 
        read
        |> waitTask
        |> ignore)
        |> should throw typeof<MonoXmlException>
        
    
[<TestFixture>]
type XmlElemInfoTests() = 
    inherit MyXmlTestClass()

    [<Test>]
    member this.``Check that we can read open XElement``() = 
        let read = readOpenXElement(this.XmlReader1) |> Async.StartAsTask
        this.Write1("<s att='23'>")
        let elem =
            read
            |> waitTask
        let expected = XElement.Parse("<s att='23'/>")
        
        test <@ equalXNode elem expected @>
        
    [<Test>]
    member this.``Check that we can read open XElement with preserving prefix``() = 
        let read = readOpenElement(this.XmlReader1) |> Async.StartAsTask
        this.Write1("<pref:s att='23' xmlns:pref='test'>")
        let elem =
            read
            |> waitTask
        let xelem = convertElemInfoToXElement elem
        let expected = XElement.Parse("<pref:s att='23' xmlns:pref='test'/>")
        test <@ elem.Prefix = "pref" @>
        test <@ equalXNode xelem expected @>

    [<Test>]
    member this.``Check that we only read the open element``() = 
        let read = readOpenElement(this.XmlReader1) |> Async.StartAsTask
        this.Write1("<pref:s att='23' xmlns:pref='test'><inner/></pref:s>")
        let elem =
            read
            |> waitTask
        let xelem = convertElemInfoToXElement elem
        let expected = XElement.Parse("<pref:s att='23' xmlns:pref='test'/>")
        test <@ elem.Prefix = "pref" @>
        test <@ equalXNode xelem expected @>


        
    [<Test>]
    member this.``Check that we can write the open Element``() = 
        let expected = XElement.Parse( "<pref:s att='23' xmlns:pref='test' />")
        writeOpenElement expected this.XmlWriter1
        |> Async.StartAsTask
        |> waitTask
        this.XmlWriter1.FlushAsync() |> Task.ofPlainTask |> waitTask
        this.FinishStream1()
        
        let readData = this.Reader1.ReadToEnd()
        let reader = new StringReader(readData) :> TextReader
        let settings = new XmlReaderSettings(Async = true)
        let xmlReader = Mono.System.Xml.XmlReader.Create(reader, settings) |> fromXmlReader
        let actual = 
            readOpenXElement xmlReader 
            |> Async.StartAsTask
            |> waitTask

        test <@ equalXNode expected actual @>