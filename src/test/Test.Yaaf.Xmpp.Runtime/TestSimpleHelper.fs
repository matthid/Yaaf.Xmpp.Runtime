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
open Yaaf.Xml
open Yaaf.IO
open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.OpenHandshake
open Yaaf.Xmpp.Stream
open Yaaf.Xmpp.StreamHelpers
open Yaaf.Xmpp
open Yaaf.TestHelper

open NUnit.Framework


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
