// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

namespace Test.Yaaf.Xmpp

open System.Xml.Linq
open NUnit.Framework
open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime
open Yaaf.TestHelper
open Foq
open System.IO
open Swensen.Unquote
open Yaaf.DependencyInjection
open Yaaf.TestHelper

[<TestFixture>]
type ``Test-Yaaf-Xmpp-XmppClient: Check if XmppClient handles the Runtime properly``() =
    inherit MyTestClass()
             
    let config = RuntimeConfig.Default

    let createRuntime coreApi =
        new XmppRuntime(coreApi, config, SimpleInjectorKernelCreator.CreateKernel())

    let createClient coreApi =
        let task = new System.Threading.Tasks.TaskCompletionSource<_>()
        let runtime = createRuntime coreApi
        let client = new XmppClient(runtime, task.Task)
        task, client

    [<Test>]
    member __.``Check if we can send a simple iq stanza`` () =
        Assert.Inconclusive ("Add xmlstream and check if it was written to it")
        let coreApi = Mock<ICoreStreamApi>().Create()
        let task, client = createClient coreApi
        client.WriteElem (XElement.Parse "<iq xmlns='jabber:client' id='blub' type='get' />")
        task.SetResult None
        ()
                
    [<Test>]
    member __.``check that we can create a XmppClient with RawConnect``() =
        let stream = Mock<Stream>().Create()
        let connectInfo = { LocalJid = JabberId.Parse ""; Login = [] }
        
        let connectData = 
          { RemoteHostname = "yaaf.de"
            Stream = new IOStreamManager(stream)
            RemoteJid = Some connectInfo.LocalJid.Domain
            IsInitializing = true }
        let client =
            XmppClient.RawConnect(
                XmppSetup.CreateSetup()
                |> XmppSetup.addConnectInfo connectInfo connectData
                |> XmppSetup.addCoreClient)
        let exit = client.Exited |> waitTask
        test <@ exit.IsSome @>
        test <@ client.IsCompleted = true @>
        test <@ client.IsClosed = true @>
        test <@ client.IsFaulted = true @>

        client.CloseConnection(true) |> waitTask
        test <@ client.IsCompleted = true @>
        test <@ client.IsClosed = true @>
        test <@ client.IsFaulted = true @>
        ()
        
    [<Test>]
    member __.``check that we can create a XmppClient with RawConnect and MemoryStream``() =
        let stream = new MemoryStream() :> Stream
         
        let connectInfo = { LocalJid = JabberId.Parse ""; Login = [] }
        
        let connectData = 
          { RemoteHostname = "yaaf.de"
            Stream = new IOStreamManager(stream)
            RemoteJid = Some connectInfo.LocalJid.Domain
            IsInitializing = true }
        let client =
            XmppClient.RawConnect(
                XmppSetup.CreateSetup()
                |> XmppSetup.addConnectInfo connectInfo connectData
                |> XmppSetup.addCoreClient)
        let exit = client.Exited |> waitTask
        test <@ exit.IsSome @>
        test <@ client.IsCompleted = true @>
        test <@ client.IsClosed = true @>
        test <@ client.IsFaulted = true @>

        client.CloseConnection(true) |> waitTask
        test <@ client.IsCompleted = true @>
        test <@ client.IsClosed = true @>
        test <@ client.IsFaulted = true @>
        ()
         
    [<Test>]
    member __.``check that Runtime can access IXmppClient``() =
        let stream = new MemoryStream() :> Stream
        let connectInfo = { LocalJid = JabberId.Parse ""; Login = [] }
        let connectData = 
          { RemoteHostname = "yaaf.de"
            Stream = new IOStreamManager(stream)
            RemoteJid = Some connectInfo.LocalJid.Domain
            IsInitializing = true }
        let client =
            XmppClient.RawConnect(
                XmppSetup.CreateSetup()
                |> XmppSetup.addConnectInfo connectInfo connectData
                |> XmppSetup.addCoreClient)
        
        let innerClient = client.Runtime.PluginManager.GetPluginService<IXmppClient>()
        test <@ obj.ReferenceEquals(innerClient, client) @>