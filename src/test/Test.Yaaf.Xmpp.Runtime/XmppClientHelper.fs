// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Test.Yaaf.Xmpp
open Yaaf.FSharp.Control
open Yaaf.Xmpp
open Yaaf.Helper
open Yaaf.Logging
open Yaaf.TestHelper
open Yaaf.Xmpp.XmlStanzas
open System.Threading.Tasks

//[<AutoOpen>]
//module XmppClientExtensions = 
//    type IXmppClient with
//        member x.WriteRaw (s:System.String) = 
//            let elem = System.Xml.Linq.XElement.Parse s
//            let nsElem = Yaaf.Xmpp.XmlStanzas.Parsing.replaceNamespace "" x.Config.StreamNamespace elem
//            x.WriteElem nsElem
//        member x.WriteRawUnsafe s = 
//            x.WriteRaw s |> Async.RunSynchronously
//        member x.WriteElemUnsafe s = 
//            x.WriteElem s |> Async.RunSynchronously
//
//    let Event_ElementReceivedRawEvent = XmppEvent.create<StreamElement> "Yaaf.XmppTest.Events.ElementReceivedRaw"
//    type IEventManager with
//        member x.AddElementReceivedRaw = x.AddEventHandlerSimple Event_ElementReceivedRawEvent
//        member x.TriggerElementReceivedRaw h = 
//            x.TriggerEvent Event_ElementReceivedRawEvent h
//open XmppClientExtensions
//type NUnitStreamHandler (config : XmppReadonlyConfig) =
//    interface IXmppStreamHandler with
//        member x.Name with get() = "NUnitStreamHandler"
//        member x.FilterNamespace with get() = ""
//        member x.Init context = ()
//        member x.UpdateConfig config = ()
//        // We do only work at stream opening
//        member x.HandleElement (elem, context) =
//            async {
//                do! context.Events.TriggerElementReceivedRaw elem
//                return HandlerResult.Unhandled
//            }
module NUnitHelper =
    //let NUnitHandler =Yaaf.Xmpp.Handler.Core.createStreamHandlerInitalizer "NUnitStreamHandler"  (fun (config) -> new NUnitStreamHandler(config) :> IXmppStreamHandler) : IXmppStreamHandlerInitalizer
    let FromObservable obs = 
        obs
        |> AsyncSeq.ofObservableBuffered
        |> AsyncSeq.cache

    //let ElemReceiver (client:IXmppClient) =
    //    let obSource = ObservableSource()
    //    client.ContextManager.RequestContextSync(fun context ->
    //        context.Events.AddElementReceivedRaw(fun (context, elem) -> obSource.Next (elem); async.Return TriggerResult.Continue))
    //    //obSource.Completed,
    //    obSource.AsObservable
    //    |> FromObservable
    //
    //let StanzaReceiver (client:IXmppClient) =
    //    let obSource = ObservableSource()
    //    client.ContextManager.RequestContextSync(fun context ->
    //        context.Events.AddRawStanzaReceived(fun (context, elem) -> obSource.Next (elem); async.Return TriggerResult.Continue))
    //    //obSource.Completed,
    //    obSource.AsObservable 
    //    |> FromObservable

    let tryHeadImpl (s:AsyncSeq<_>) =   
        async {
            let! t = s
            return
                match t with
                | Nil -> None
                | Cons(data, next) ->
                    Some(data, next)
        } 
    let tryHead (s:AsyncSeq<_>) =   
        async {
            let! t = tryHeadImpl s
            return t |> Option.map fst
        } 
    let head (s:AsyncSeq<_>) =   
        async {
            let! t = tryHead s
            return
                match t with
                | None -> failwith "no head found"
                | Some s -> s
        } 
    let readNext (s:AsyncSeq<_>) = 
        async {
            Log.Verb (fun _ -> L "NUnitHelper: start waiting for next element!")
            let! t = tryHeadImpl s
            Log.Verb (fun _ -> L "NUnitHelper: got %A as next element!" (t |> Option.map fst))
            return
                match t with
                | None -> failwith "no next found!"
                | Some d -> d
        } 
    let readNextTask (s:AsyncSeq<_>) = readNext s |> Async.StartAsTask

    let NEXT (result:Task<'a * AsyncSeq<'a>>) = 
        let stanza, receiver  = result |> waitTaskIT "result" (defaultTimeout * 2)
        stanza, readNextTask receiver


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
