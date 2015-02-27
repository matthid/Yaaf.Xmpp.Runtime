// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

namespace Test.Yaaf.Xmpp
open System
open System.IO
open Mono.System.Xml
open System.Xml.Linq
open NUnit.Framework
open FsUnit
open FSharp.Control
open Test.Yaaf.Xmpp.TestHelper
open Yaaf.Xmpp
open Yaaf.Xml
open Yaaf.IO
open Yaaf.Helper
open Yaaf.TestHelper
open Yaaf.Xmpp.Stream
open Yaaf.Xmpp.Runtime.OpenHandshake

[<TestFixture>]
type ``Given the xmpp core implementation``() = 
    [<Test>]
    member this.``test infiniteStream implementation`` () = 
        let stream = Stream.infiniteStream()
        let data = [| 1;2;3;4;5;6;7;8 |]
        stream.Write data |> Async.RunSynchronously
        let read = stream.Read () |> Async.RunSynchronously
        read.IsSome |> should be True
        read.Value |> should be (equal data)
    [<Test>]
    member this.``test crossStream implementation`` () = 
        let s1, s2 = Stream.crossStream (Stream.infiniteStream()) (Stream.infiniteStream())
        let server = s1
        let client = s2
        let data = [| 1;2;3;4;5;6;7;8 |]
        server.Write data |> Async.RunSynchronously
        let read = client.Read () |> Async.RunSynchronously
        read.IsSome |> should be True
        read.Value |> should be (equal data)
    [<Test>]
    member this.``test crossStream implementation (readfirst)`` () = 
        let s1, s2 = Stream.crossStream (Stream.infiniteStream()) (Stream.infiniteStream())
        let server = s1
        let client = s2
        let data = [| 1;2;3;4;5;6;7;8 |]
        let readTask = client.Read () |> Async.StartAsTask
        
        async {
            do! Async.Sleep 500
            do! server.Write data } |> Async.RunSynchronously
        let read = readTask.Result
        read.IsSome |> should be True
        read.Value |> should be (equal data)

    [<Test>]
    member this.``test fromInterface implementation`` () = 
        let s1, s2 = Stream.crossStream (Stream.infiniteStream()) (Stream.infiniteStream())
        let server = Stream.fromInterfaceSimple s1
        let client = Stream.fromInterfaceSimple s2
        let data = [| for i in 1uy..8uy do yield i |]
        server.Write(data,0,data.Length)
        let buffer = Array.zeroCreate data.Length
        let read = client.Read (buffer, 0, buffer.Length)
        read |> should be (equal data.Length)
        data |> should be (equal buffer)
        
    [<Test>]
    member this.``test fromInterface async implementation`` () = 
        let s1, s2 = Stream.crossStream (Stream.infiniteStream()) (Stream.infiniteStream())
        let server = Stream.fromInterfaceSimple s1
        let client = Stream.fromInterfaceSimple s2
        let data = [| for i in 1uy..8uy do yield i |]
        server.WriteAsync(data, 0, data.Length) |> Task.ofPlainTask |> Task.await |> Async.RunSynchronously
        let buffer = Array.zeroCreate data.Length
        let read = client.ReadAsync (buffer, 0, buffer.Length) |> Task.await |> Async.RunSynchronously
        read |> should be (equal data.Length)
        data |> should be (equal buffer)

    [<Test>]
    member this.``test fromInterface Async implementation`` () = 
        let s1, s2 = Stream.crossStream (Stream.infiniteStream()) (Stream.infiniteStream())
        let server = Stream.fromInterfaceSimple s1
        let client = Stream.fromInterfaceSimple s2
        let data = [| for i in 1uy..8uy do yield i |]
        server.AsyncWrite(data,0,data.Length) |> Async.RunSynchronously
        let buffer = Array.zeroCreate data.Length
        let read = client.AsyncRead (buffer, 0, buffer.Length)|> Async.RunSynchronously
        read |> should be (equal data.Length)
        data |> should be (equal buffer)
    [<Test>]
    member this.``test fromInterface Async implementation with multiple reads`` () = 
        let s1, s2 = Stream.crossStream (Stream.infiniteStream()) (Stream.infiniteStream())
        let server = Stream.fromInterfaceSimple s1
        let client = Stream.fromInterfaceSimple s2
        let data = [| for i in 1uy..8uy do yield i |]
        server.AsyncWrite(data,0,data.Length) |> Async.RunSynchronously
        let buffer = Array.zeroCreate (data.Length - 1)
        let read1 = client.AsyncRead (buffer, 0, buffer.Length)|> Async.RunSynchronously
        read1 |> should be (equal buffer.Length)        
        server.AsyncWrite(data,0,4) |> Async.RunSynchronously
        let read2 = client.AsyncRead (buffer, 0, buffer.Length)|> Async.RunSynchronously
        read2 |> should be (equal 1)
        let read3 = client.AsyncRead (buffer, 0, buffer.Length)|> Async.RunSynchronously
        read3 |> should be (equal 4)
    [<Test>]
    member this.``test stream implementation`` () = 
        let s1, s2 = Stream.crossStream (Stream.infiniteStream()) (Stream.infiniteStream())
        let server = Stream.fromInterfaceSimple s1
        let client = Stream.fromInterfaceSimple s2
        
        let openInfo = StreamOpenInfo.Empty
        let writer = XmlWriter.Create(client, new XmlWriterSettings(Async = true))
        writeOpenElement (fromOpenInfo openInfo) writer |> Async.StartAsTask |> waitTask
        writer.Flush()
        let reader = createFixedReader server (new XmlReaderSettings(Async = true))
        let readOpenInfo = readOpenXElement reader |> Async.StartAsTask |> waitTask
        openInfo |> should be (equal (toOpenInfo readOpenInfo))
    
    [<Test>]
    member this.``test stream opening`` () = 
        let s1, s2 = Stream.crossStream (Stream.infiniteStream()) (Stream.infiniteStream())
        let server = Stream.fromInterfaceSimple s1
        let client = Stream.fromInterfaceSimple s2
        ()


        (*
    [<Test>]
    member this.``simple connect and exit`` () = 
        let s1, s2 = Stream.crossStream (Stream.infiniteStream()) (Stream.infiniteStream())
        
        let clientStream, serverStream =
            Stream.fromInterface s1, Stream.fromInterface s2
            
        let client = new XmppInitializingClient()
        let openInfo = {
            From = None
            To = None
            Id = None
            Version = None }
        let server = 
            new XmppServer(
                openInfo, 
                {
                    TslOptions = None
                    Mechanism = Map.empty
                })
        let clientTask = 
          async {
            do! server.AcceptClient(serverStream)
            return () } |> Async.StartAsTask
        let serverTask = 
          async {
            let! info,features = client.Connect(clientStream, openInfo)
            return () } |> Async.StartAsTask
        clientTask.Wait()
        let task = client.Disconnect() |> Async.StartAsTask
        serverTask.Wait()
        task.Wait()
    [<Test>]
    member this.``simple connect and error`` () = 
        let s1, s2 = Stream.crossStream (Stream.infiniteStream()) (Stream.infiniteStream())
        
        let clientStream, serverStream =
            Stream.fromInterface s1, Stream.fromInterface s2
            
        let client = new XmppInitializingClient()
        let openInfo = {
            From = None
            To = None
            Id = None
            Version = None }
        let server = new XmppServer(openInfo, {
                TslOptions = None
                Mechanism = Map.empty
            })
        let clientTask = 
          async {
            do! server.AcceptClient(serverStream)
            return () } |> Async.StartAsTask
        let serverTask = 
          async {
            let! info,features = client.Connect(clientStream, openInfo)
            return () } |> Async.StartAsTask
        clientTask.Wait()
        
        let task = client.Failwith (new StreamErrorException(StreamError.UnknownError "test", Some "unknown error text", [])) |> Async.StartAsTask
        serverTask.Wait()
        task.Wait()
        *)