// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

module Test.Yaaf.Xmpp.TestHelper
open System.IO
open Mono.System.Xml
open Yaaf.FSharp.Control

open NUnit.Framework
open FsUnit

open Yaaf.Logging
open Yaaf.Xml
open Yaaf.IO
open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.OpenHandshake
open Yaaf.Xmpp.Stream
open Yaaf.Xmpp.StreamHelpers
open Yaaf.Xmpp
open Yaaf.Helper

module NUnitFastNegotiation =
    let handlerName = "NunitFastNegotiationHandler"
    let fastNegNs = "http://xmpp.yaaf.de/fastNegotiation"
    type Command = {
        Command : string
        Value : string }
    open System.IO  
    open System.Text  
    open System.Xml 
    open System.Xml.Linq
    open System.Runtime.Serialization

    let getCommandElem command = 
        [
            new XAttribute(getXName "command" "", command.Command) :> obj
            command.Value :> obj
        ] |> getXElemWithChilds (getXName "nunitcommand" fastNegNs)
    let isCommandElem (elem:XElement) = 
        match elem.Name.NamespaceName, elem.Name.LocalName with
        | Equals fastNegNs, Equals "nunitcommand" -> true
        | _ -> false
    let parseCommandElem (elem:XElement) = 
        if isCommandElem elem then
            { Command = elem |> forceAttrValue (getXName "command" "")
              Value = elem.Value }
        else failwith "expected command element"

    let createBackendOverIStream (xmlStream:IStream<XElement>) = 
        { new IXmlStream with
            member x.ReadEnd(): Async<unit> = 
                failwith "Not implemented yet"
            
            member x.ReadStart(): Async<OpenElement> = 
                async {
                    let! res = xmlStream.Read()
                    match res with
                    | Some elem ->
                        //return elem
                        Log.Verb (fun _ -> L "reading: %s" (elem.ToString()))
                        let command = parseCommandElem elem
                        assert (command.Command = "openstream")
                        return XElement.Parse command.Value
                    | None -> return failwith "expected openstream"
                }
            
            member x.TryRead(): Async<StreamElement option> = 
                async {
                    let! res = xmlStream.Read()
                    match res with
                    | Some elem ->
                        Log.Verb (fun _ -> L "reading: %s" (elem.ToString()))
                        if isCommandElem elem then
                            let command = parseCommandElem elem
                            if command.Command = "finishstream" then 
                                return None
                            else
                                return failwith "expected stream element (not command element)"
                        else
                            return Some elem
                    | None -> return None
                }
            
            member x.Write(elem: StreamElement): Async<unit> = 
                async {
                    Log.Verb (fun _ -> L "writing: %s" (elem.ToString()))
                    do! xmlStream.Write elem
                }
            
            member x.WriteEnd(): Async<unit> = 
                failwith "Not implemented yet"
            
            member x.WriteStart(openData: OpenElement): Async<unit> = 
                async {
                    let serialized = openData.ToString()
                    let command =
                        { Command = "openstream"; Value = serialized }
                    let elem = getCommandElem command
                    Log.Verb (fun _ -> L "writing: %s" (elem.ToString()))
                    do! xmlStream.Write elem
                }
        }
type NUnitStreamManager (stream, finishStream) =
    inherit StreamManager<IStream<System.Xml.Linq.XElement>>(stream, NUnitFastNegotiation.createBackendOverIStream)
    override x.CloseStreamOverride () =
        async {
            finishStream ()
            return ()
        }

let CreateNunitStreamManager stream finishStream = NUnitStreamManager(stream, finishStream) :> IStreamManager

let initTestRaw () = 
    let s1 = Stream.infiniteStream()
    let stream = Stream.fromInterface s1

    let writer = new StreamWriter(stream)
    writer.AutoFlush <- true
    let reader = createFixedReader stream (new XmlReaderSettings(Async = true))
    reader, (fun (s:string) -> writer.Write(s); writer.Flush()),
    { new System.IDisposable with
        member x.Dispose() = writer.Dispose() }
        
let initTest levelWriter levelReader = 
    let mem = new MemoryStream()
    let writerSettings = new XmlWriterSettings(Async = true)
    writerSettings.ConformanceLevel <- levelWriter
    let writer = XmlWriter.Create(mem, writerSettings)
    writer,
    (fun () -> 
      async {
        do! writer.FlushAsync() |> Task.ofPlainTask
        writer.Dispose()
        mem.Position <- 0L 
        let settings =  new XmlReaderSettings(Async = true)
        settings.ConformanceLevel <- levelReader 
        // this works because end of stream
        let reader = createFixedReader mem settings
        //reader.Rea
        //do! reader.MoveToContentAsync() |> Task.ofPlainTask
        return reader } |> Async.RunSynchronously)

open Yaaf.Xmpp.Stream
open Yaaf.TestHelper

[<TestFixture>]
type SimpleHelperMethodTests() =
    inherit MyTestClass()

    [<Test>]
    member this.``Check that guardAsync works`` () = 
        (fun () ->
            guardAsync
                (fun () -> async {
                        do! Async.Sleep 10
                        failwithf "testfail"
                        do! Async.Sleep 10
                    }) |> Async.RunSynchronously)
            |> should throw typeof<System.Exception>
            
    [<Test>]
    member this.``Check that guardAsync doesn't guards XmlException`` () = 
        (fun () ->
            guardAsync
                (fun () -> async {
                        do! Async.Sleep 10
                        raise <| new System.Xml.XmlException("test")
                        do! Async.Sleep 10
                    }) |> Async.RunSynchronously)
            |> should throw typeof<System.Xml.XmlException>
    [<Test>]
    member this.``Check that guardAsync guards Yaaf.Xmpp.XmlException`` () = 
        (fun () ->
            guardAsync
                (fun () -> async {
                        do! Async.Sleep 10
                        raise <| new XmlException("test")
                        do! Async.Sleep 10
                    }) |> Async.RunSynchronously)
            |> should throw typeof<StreamErrorException>