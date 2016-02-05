// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

namespace Test.Yaaf.Xmpp

open System.IO
open Mono.System.Xml
open Yaaf.FSharp.Control

open NUnit.Framework
open FsUnit

open Yaaf.Logging
open Yaaf.Logging.AsyncTracing
open Yaaf.Xml
open Yaaf.IO
open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.OpenHandshake
open Yaaf.Xmpp.Stream
open Yaaf.Xmpp.StreamHelpers
open Yaaf.Xmpp
open Yaaf.Helper
module TestHelper =
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

open TestHelper

open Yaaf.TestHelper
open Yaaf.Xmpp.XmlStanzas
open Yaaf.Xmpp.XmlStanzas.Parsing

[<TestFixture>]
type XmlStanzaParsingTestClass() =
    inherit MyTestClass()
    let getElem stanzaString =
        let reader, writer, d = initTestRaw ()
        let readTask = readOpenXElement reader |> Async.StartAsTask

        writer ("<?xml version='1.0'?><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>")
        let openInfo = readTask |> waitTask
        let readTask = readXElementOrClose reader |> Async.StartAsTask

        writer (stanzaString)
        let elem = readTask |> waitTask
        elem.IsSome |> should be True
        elem.Value
    let test stanzaString =
        let elem = getElem stanzaString
        let stanza = parseStanzaElementNoError "jabber:client" elem
        if stanza.Header.Type.IsSome && stanza.Header.Type.Value = "error" then
            (fun () -> parseStanzaElement "jabber:client" elem |> ignore)
                |> should throw typeof<ReceivedStanzaException>

        let elem2 = createStanzaElement "jabber:client" stanza
        let result = equalXNodeAdvanced elem2 elem
        if not result.IsEqual then
            Assert.Fail (result.Message)
        stanza
    let validateShouldThrow stanza =
        (fun () -> validateStanza stanza |> ignore) |> should throw typeof<StanzaValidationException>

    let genericTest (contentGen:ContentGenerator<'a>) stanzaString (stanza:Stanza<'a>) =
        let origElem = getElem stanzaString
        let newElem = createStanzaElement "jabber:client" stanza
        let result = equalXNodeAdvanced newElem origElem
        if not result.IsEqual then
            Assert.Fail (result.Message)

    member x.Test stanzaString =
        test stanzaString
    member x.GenericTest contentGen stanzaString stanza =
        genericTest contentGen stanzaString stanza
    member x.ValidateShouldThrow stanza =
        validateShouldThrow stanza

module DevelopmentCertificate =

    let develCertfileText = "-----BEGIN CERTIFICATE-----
MIIDKzCCApSgAwIBAgIJAMdXRrIvvGzEMA0GCSqGSIb3DQEBBQUAMHoxCzAJBgNV
BAYTAkRFMRAwDgYDVQQIDAdCYXZhcmlhMQ0wCwYDVQQKDARZYWFmMRAwDgYDVQQL
DAdZYWFmIENBMRAwDgYDVQQDDAd5YWFmLmRlMSYwJAYJKoZIhvcNAQkBFhdtYXR0
aGkuZEBnb29nbGVtYWlsLmNvbTAeFw0xNDAyMTUyMTIzMzFaFw0xNTAyMTUyMTIz
MzFaMIG3MQswCQYDVQQGEwJERTEQMA4GA1UECAwHQmF2YXJpYTETMBEGA1UEBwwK
RGVnZ2VuZG9yZjEZMBcGA1UECgwQWWFhZiBEZXZlbG9wbWVudDEeMBwGA1UECwwV
WWFhZiBYbXBwIERldmVsb3BtZW50MR4wHAYDVQQDDBV4bXBwLnlhYWYuZGV2ZWxv
cG1lbnQxJjAkBgkqhkiG9w0BCQEWF21hdHRoaS5kQGdvb2dsZW1haWwuY29tMIGf
MA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCticsDnzVp3sp3Ukmlq3WtnKEhusHU
S3zIRghzcQj/JVftNLaT9qlX2uEsJF/0aPKYD56iQ4nUysmAKbBpWLp9E/pFKR9G
xhPcbkDiyk04ZGChm4Dac/lgIKmfRrEjo3/egLKMGH38rtvM0IcZ0aTyOXckHUng
x6akB+mF7ODbKwIDAQABo3sweTAJBgNVHRMEAjAAMCwGCWCGSAGG+EIBDQQfFh1P
cGVuU1NMIEdlbmVyYXRlZCBDZXJ0aWZpY2F0ZTAdBgNVHQ4EFgQU1thdektdDilt
+IdZTou7fGQp500wHwYDVR0jBBgwFoAUCtg9TX/YhfrG9WRUlzI3xTjYg9gwDQYJ
KoZIhvcNAQEFBQADgYEAoA2xxBgGKZc9CpV/XT4D7+m9mnQ3jdmOzLdtpKzB2YmT
vY4nPIBemRhaJN7lQQEa9fhT0Zv6lBvj8jwK/XHVYJZSe3hqOx31wN4tfKILyTFV
lRFiTqJLebUfXDqhyF5jF9VNU2B8fhqz3puhQi0mdpNklSggRzNFFOB90vLorBk=
-----END CERTIFICATE-----"
    let develKeyfileText = "-----BEGIN PRIVATE KEY-----
MIICdQIBADANBgkqhkiG9w0BAQEFAASCAl8wggJbAgEAAoGBAK2JywOfNWneyndS
SaWrda2coSG6wdRLfMhGCHNxCP8lV+00tpP2qVfa4SwkX/Ro8pgPnqJDidTKyYAp
sGlYun0T+kUpH0bGE9xuQOLKTThkYKGbgNpz+WAgqZ9GsSOjf96AsowYffyu28zQ
hxnRpPI5dyQdSeDHpqQH6YXs4NsrAgMBAAECgYAo4bA4zzXXFgweZf1BkQ3s81wm
RQfKimoACDePco6LBPIcyHFGlDI6py6qpnsQafTUi8F0OnLq9UbY8XlEqAlucHva
c7QFRdEDrObF9GL9nCCgCYMe9Ralw+r1x9zyI8T8IKbna704TNMatOxnf36zqdmk
VNZD6AZZecevOcfD0QJBANoFkinlqqEpyUT0E/IUQarUQiUOJrqStTudcmzOPED5
Mk5oOa8TXg4Fh/NTHLNIfvb1+EtvBxVKh2JgTkSQkxkCQQDLxIgGfxYaXSQ5l5Nq
DoP2jB8XauJp91eyUCW1HQWEmUdWgOgZDW595K+dVGo5zM2WBRsyf3VCKnm7fLLI
e0zjAkBuN4DDs3pGDSTVufpXWAw2eyWRLA1CJqZ+I8NT5BKr2g6neqMmscjLl9o5
lVud+tlMqd5C7DcNeWblwb/vg5MJAkBEcim29O15wZuvdMjhsSqGoJ65AQA41Aqz
LNTdt3fpCIu79OUBtU9OHokW8goUjETqhaCTH9lFdnsZjVOIoFI9AkAZ3UE+5NmM
LxqN3z1XzU+kZrP+r4DHDUTk8OCqm8uUMoswKpqeb+UtxStCs589Y3srfu01XoeQ
p0hBzjWte8HO
-----END PRIVATE KEY-----"  

    open Yaaf.Xmpp.Server
    let serverCertificate = 
        ServerCertificateData.OpenSslText(develKeyfileText, develCertfileText, "")

module Task =
    let checkNotExited (t:Task<_>) = 
        if t.Wait(1) then // exited when it should not
            match t.Result with
            | Some e -> raise (new System.Exception("XmppClient exited unexpectedly!", e))
            | None -> failwith ("XmppClient exited unexpectedly!")
            
    let checkNotTriggered (t:Task<_>) f = 
        if t.Wait(1) then // exited when it should not
            f t.Result 
            failwith ("Task should not finish!")
