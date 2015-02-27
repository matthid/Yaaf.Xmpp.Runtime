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
open Yaaf.Xmpp
open Yaaf.Xmpp.Server
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.Features
open Yaaf.Xmpp.Stream
open Test.Yaaf.Xmpp.TestHelper
open System.Threading.Tasks
open Yaaf.IO
open Yaaf.TestHelper
open Test.Yaaf.Xml.XmlTestHelper
open Test.Yaaf.Xmpp
open Test.Yaaf.Xmpp.DevelopmentCertificate
open Foq
open Swensen.Unquote
open Yaaf.DependencyInjection

[<TestFixture>]
type ``Test-Yaaf-Xmpp-XmppClient: Check if XmppClient handles the Runtime properly``() =
    inherit MyTestClass()
             
    let config = RuntimeConfig.Default

    let createRuntime coreApi =
        new XmppRuntime(coreApi, config, NinjectKernelCreator.CreateKernel())

    let createClient coreApi =
        let task = new System.Threading.Tasks.TaskCompletionSource<_>()
        let runtime = createRuntime coreApi
        let client = new XmppClient(runtime, task.Task)
        task, client

    [<Test>]
    member x.``Check if we can send a simple iq stanza`` () =
        Assert.Inconclusive ("Add xmlstream and check if it was written to it")
        let coreApi = Mock<ICoreStreamApi>().Create()
        let task = new System.Threading.Tasks.TaskCompletionSource<_>()
        let task, client = createClient coreApi
        client.WriteElem (XElement.Parse "<iq xmlns='jabber:client' id='blub' type='get' />")
        task.SetResult None
        ()

    // TODO: check if properties (like IsClosed etc) are correct.