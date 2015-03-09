// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Test.Yaaf.Xmpp.Runtime

open Yaaf.DependencyInjection
open Yaaf.Helper
open Yaaf.TestHelper
open Yaaf.Xml
open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.OpenHandshake
open System.IO
open System.Xml.Linq
open NUnit.Framework
open FsUnit
open Swensen.Unquote
open Foq
open System

type ITestService = interface end
type TestService () = 
    interface ITestService

type TestPlugin () =
    interface IXmppPlugin with
        member x.Name = "TestPlugin"
        member x.PluginService = Service.FromInstance<ITestService,_> (new TestService())
type ITestService2 = 
    abstract member Service1 : ITestService
type TestService2 (test1 : ITestService) = 
    interface ITestService2 with
        member x.Service1 = test1
        
type TestPluginThatDependsOnITestService (test : ITestService) =
    interface IXmppPlugin with
        member x.Name = "TestPluginThatDependsOnITestService"
        member x.PluginService = Service.None

type TestPlugin2 (test : ITestService, mgr : IServicePluginManager<IXmppPlugin>, delivery : ILocalDelivery, coreApi : ICoreStreamApi) =
    interface IXmppPlugin with
        member x.Name = "TestPlugin"
        member x.PluginService = IService.FromTupleOption (typeof<ITestService2>, new TestService2(test) :> obj)

/// Check that the runtime class works as expected.
[<TestFixture>]
type ``Test-Yaaf-Xmpp-Runtime-XmppRuntime: Check that the runtime behaves as expected``() = 
    inherit Yaaf.TestHelper.MyTestClass()
    
    let config = RuntimeConfig.Default
    let createRuntime coreApi =
        new XmppRuntime(coreApi, config, SimpleInjectorKernelCreator.CreateKernel())

    [<Test>]
    member this.``check that connection setup calls the relevant methods``() = 
        let xmlStream =
            Mock<IXmlStream>()
                .Setup(fun x -> <@ x.TryRead() @>).Returns(async.Return None)
                .Create()
        let coreApi =
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.OpenStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.CoreStreamHistory @>).Returns([])
                .Setup(fun x -> <@ x.CloseStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.AbstractStream @>).Returns(xmlStream)
                .Create()

        let runtime = createRuntime coreApi

        let stream = 
            Mock<IStreamManager>()
                .Setup(fun x -> <@ x.IsClosed @>).Returns(false)
                .Create()
                
        Mock.Verify(<@ coreApi.SetCoreStream(stream) @>, Times.never)
        Mock.Verify(<@ coreApi.OpenStream() @>, Times.never)
        Mock.Verify(<@ coreApi.CloseStream() @>, Times.never)

        let result = runtime.Connect(stream) |> waitTask
        test <@ result = None @>

        Mock.Verify(<@ coreApi.SetCoreStream(stream) @>, Times.exactly 1)
        Mock.Verify(<@ coreApi.OpenStream() @>, Times.exactly 1)
        Mock.Verify(<@ coreApi.CloseStream() @>, Times.atleast 1)

        
    [<Test>]
    member this.``check that plugins are called``() = 
        let originalElem = XElement(XName.Get("Dummy", ""))
        let xmlStream =
            Mock<IXmlStream>()
                .Setup(fun x -> <@ x.TryRead() @>)
                .ReturnsInOrder([async.Return (Some originalElem); async.Return None])
                .Create()
        let coreApi =
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.OpenStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.CoreStreamHistory @>).Returns([])
                .Setup(fun x -> <@ x.CloseStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.AbstractStream @>).Returns(xmlStream)
                .Create()
        let processedElem = XElement(XName.Get("DummyProcessed", ""))
        let receivePipeline =
            Mock<IPipeline<StreamElement>>()
                .Setup(fun x -> <@ x.Modify(any()) @>).Returns({ Element = processedElem; IgnoreElement = false})
                .Setup(fun x -> <@ x.HandlerState(any()) @>).Returns(HandlerState.ExecuteAndHandle)
                .Setup(fun x -> <@ x.Process(any()) @>).Returns(Task.returnM ())
                .Setup(fun x -> <@ x.ProcessSync(any()) @>).Returns(async.Return ())
                .Create()
        let sendPipeline =
            Mock<IPipeline<StreamElement>>()
                .Create()
        let xmlPlugin =
            Mock<IXmlPipelinePlugin>()
                .Setup(fun x -> <@ x.StreamOpened() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.ReceivePipeline @>).Returns(receivePipeline)
                .Setup(fun x -> <@ x.SendPipeline @>).Returns(sendPipeline)
                .Create()
        let runtime = createRuntime coreApi
        
        let xmlPluginMgr = runtime.PluginManager.GetPluginService<IXmlPluginManager>()
        xmlPluginMgr.RegisterPlugin(xmlPlugin)

        let stream = Mock<IStreamManager>().Create()
        let result = runtime.Connect(stream) |> waitTask
        test <@ result = None @>
        Mock.Verify(<@ xmlPlugin.StreamOpened() @>, Times.exactly 1)
        Mock.Verify(<@ receivePipeline.Modify(any()) @>, Times.exactly 1)
        Mock.Verify(<@ receivePipeline.HandlerState(any()) @>, Times.exactly 1)
        Mock.Verify(<@ receivePipeline.Process(any()) @>, Times.exactly 1)
        Mock.Verify(<@ receivePipeline.ProcessSync(any()) @>, Times.exactly 1)
        
    [<Test>]
    member this.``check stream history is closed (in right order)``() = 
        Assert.Inconclusive ("This feature is not available")


    [<Test>]
    member this.``check plugins are created properly``() = 
        let coreApi =
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.CoreStreamHistory @>).Returns([])
                .Setup(fun x -> <@ x.CloseStream() @>).Returns(async.Return())
                .Create()
        let kernel = SimpleInjectorKernelCreator.CreateKernel()
        use runtime = new XmppRuntime(coreApi, config, kernel)
        // TestPlugin2 shouldn't work because ITestService is not available!
        raises<ConfigurationException>
            (<@ runtime.PluginManager.RegisterPlugin<TestPlugin2>() @>)

        runtime.PluginManager.RegisterPlugin<TestPlugin>()
        let service = runtime.PluginManager.GetPluginService<ITestService>()
        // now it should work
        runtime.PluginManager.RegisterPlugin<TestPlugin2>()
        let service2 = runtime.PluginManager.GetPluginService<ITestService2>()
        test <@ obj.ReferenceEquals(service, service2.Service1) @>
        // main kernel is not modified, exception is not wrapped because we access the kernel directly
        raises<DependencyException>(<@ kernel.Get<TestPlugin2>() @>)
        ()
    
    [<Test>]
    member this.``check that plugins with invalid services are rejected``() =
        let coreApi =
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.CoreStreamHistory @>).Returns([])
                .Create()
        let plugin =
            Mock<IXmppPlugin>()
                .Setup(fun x -> <@ x.PluginService @>).Returns(IService.FromTupleOption (typeof<ITestService2>, new TestService() :> obj))
                .Create()
        let runtime = createRuntime coreApi
        // Is it rejected?
        raises<ConfigurationException> <@ runtime.PluginManager.RegisterPlugin(plugin) @>
        
        // Check that is was not added!
        test <@ runtime.PluginManager.GetPlugins() |> Seq.isEmpty @>
        raises<ConfigurationException> <@ runtime.PluginManager.GetPluginService<ITestService2>() @>
        
    [<Test>]
    member this.``check that plugins with invalid services are rejected by helper method``() =
        // Is it rejected?
        raises<ConfigurationException> <@ IService.FromInstance<ITestService2> (new TestService()) @>
        // This should work
        test <@ IService.FromInstance<ITestService> (new TestService()) |> Seq.isEmpty |> not @>

        
    [<Test>]
    member this.``check that plugins can depend on services provided by the streamopener``() =
        let coreApi1 =
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.CoreStreamHistory @>).Returns([])
                .Create()
        let runtime1 = createRuntime coreApi1
        //raises<ConfigurationException> <@ runtime1.PluginManager.RegisterPlugin<TestPluginThatDependsOnITestService>() @>
        
        // now opener provides ITestService
        let coreApi2 =
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.CoreStreamHistory @>).Returns([])
                .Setup(fun x -> <@ x.PluginService @>).Returns(Service.FromInstance<ITestService,_> (new TestService()))
                .Create()
        let runtime2 = createRuntime coreApi2
        runtime2.PluginManager.RegisterPlugin<TestPluginThatDependsOnITestService>()
        
    [<Test>]
    member this.``check that runtime shuts down properly when OpenStream fails``() = 
        let xmlStream =
            Mock<IXmlStream>()
                //.Setup(fun x -> <@ x.TryRead() @>).ReturnsInOrder([async.Return (Some originalElem); async.Return None])
                .Setup(fun x -> <@ x.Write(any()) @>).Returns(async.Return ())
                .Create()
        let coreApi =
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.OpenStream() @>).Raises(StreamError.create XmlStreamError.BadFormat)
                .Setup(fun x -> <@ x.CoreStreamHistory @>).Returns([])
                .Setup(fun x -> <@ x.CloseStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.AbstractStream @>).Returns(xmlStream)
                .Create()
        let runtime = createRuntime coreApi
        let shutdown = runtime.PluginManager.GetPluginService<IRuntimeShutdown>()
        let isShutdownCalled = ref false
        let task = shutdown.RuntimeTask.ContinueWith (fun (t:System.Threading.Tasks.Task<_>) -> isShutdownCalled := true)
        let stream = Mock<IStreamManager>().Create()
        let exn = runtime.Connect(stream) |> waitTask
        test <@ exn.IsSome && isType<StreamErrorException> exn.Value @>
        task |> waitTask
        test <@ isShutdownCalled.Value @>
        Mock.Verify(<@ coreApi.CloseStream() @>, Times.atleastonce)
        
    [<Test>]
    member this.``check that runtime shuts down properly when Read fails``() = 
        let xmlStream =
            Mock<IXmlStream>()
                .Setup(fun x -> <@ x.TryRead() @>).Raises(StreamError.create XmlStreamError.BadFormat)
                .Setup(fun x -> <@ x.Write(any()) @>).Returns(async.Return ())
                .Create()
        let coreApi =
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.OpenStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.CoreStreamHistory @>).Returns([])
                .Setup(fun x -> <@ x.CloseStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.AbstractStream @>).Returns(xmlStream)
                .Create()
        let runtime = createRuntime coreApi
        let shutdown = runtime.PluginManager.GetPluginService<IRuntimeShutdown>()
        let isShutdownCalled = ref false
        let t = shutdown.RuntimeTask.ContinueWith (fun (t:System.Threading.Tasks.Task<_>) -> isShutdownCalled := true)

        let stream = Mock<IStreamManager>().Create()
        let exn = runtime.Connect(stream) |> waitTask
        test <@ exn.IsSome && isType<StreamErrorException> exn.Value @>
        t |> waitTask
        test <@ isShutdownCalled.Value @>
        Mock.Verify(<@ xmlStream.Write(any()) @>, Times.atleastonce) //Error was written!
        Mock.Verify(<@ coreApi.CloseStream() @>, Times.atleastonce)

          
        
    [<Test>]
    member this.``check that runtime shuts down properly when Read requests normal shutdown``() = 
        let xmlStream =
            Mock<IXmlStream>()
                .Setup(fun x -> <@ x.TryRead() @>).Raises(new StreamNormalFinishedException("Just a normal shutdown"))
                .Setup(fun x -> <@ x.Write(any()) @>).Returns(async.Return ())
                .Create()
        let coreApi =
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.OpenStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.CoreStreamHistory @>).Returns([])
                .Setup(fun x -> <@ x.CloseStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.AbstractStream @>).Returns(xmlStream)
                .Create()
        let runtime = createRuntime coreApi
        let shutdown = runtime.PluginManager.GetPluginService<IRuntimeShutdown>()
        let isShutdownCalled = ref false
        let t = shutdown.RuntimeTask.ContinueWith (fun (t:System.Threading.Tasks.Task<_>) -> isShutdownCalled := true)

        let stream = Mock<IStreamManager>().Create()
        let result = runtime.Connect(stream) |> waitTask
        test <@ result = None @>
        t |> waitTask
        test <@ isShutdownCalled.Value @>
        Mock.Verify(<@ coreApi.CloseStream() @>, Times.atleastonce)

        
    [<Test>]
    member this.``check that runtime shuts down properly when the stream was finished unexpected``() = 
        let xmlStream =
            Mock<IXmlStream>()
                .Setup(fun x -> <@ x.TryRead() @>).Raises(new StreamFinishedException("A plugin tried to read but the stream was already shutdown"))
                .Setup(fun x -> <@ x.Write(any()) @>).Returns(async.Return ())
                .Create()
        let coreApi =
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.OpenStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.CoreStreamHistory @>).Returns([])
                .Setup(fun x -> <@ x.CloseStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.AbstractStream @>).Returns(xmlStream)
                .Create()
        let runtime = createRuntime coreApi
        let shutdown = runtime.PluginManager.GetPluginService<IRuntimeShutdown>()
        let isShutdownCalled = ref false
        let t = shutdown.RuntimeTask.ContinueWith (fun (t:System.Threading.Tasks.Task<_>) -> isShutdownCalled := true)
        
        let stream = Mock<IStreamManager>().Create()
        let exn = runtime.Connect(stream) |> waitTask
        test <@ exn.IsSome && isType<StreamFinishedException> exn.Value @>
        t |> waitTask
        test <@ isShutdownCalled.Value @>
        Mock.Verify(<@ coreApi.CloseStream() @>, Times.atleastonce)
        
    [<Test>]
    member this.``check that runtime shuts down properly when we read the end``() = 
        let xmlStream =
            Mock<IXmlStream>()
                .Setup(fun x -> <@ x.TryRead() @>).Returns(async.Return None)
                .Setup(fun x -> <@ x.Write(any()) @>).Returns(async.Return ())
                .Create()
        let coreApi =
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.OpenStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.CoreStreamHistory @>).Returns([])
                .Setup(fun x -> <@ x.CloseStream() @>).Returns(async.Return ())
                .Setup(fun x -> <@ x.AbstractStream @>).Returns(xmlStream)
                .Create()
        let runtime = createRuntime coreApi
        let shutdown = runtime.PluginManager.GetPluginService<IRuntimeShutdown>()
        let isShutdownCalled = ref false
        let t = shutdown.RuntimeTask.ContinueWith (fun (t:System.Threading.Tasks.Task<_>) -> isShutdownCalled := true)
        
        let stream = Mock<IStreamManager>().Create()
        let result = runtime.Connect(stream) |> waitTask
        test <@ result = None @>
        t |> waitTask
        test <@ isShutdownCalled.Value @>
        Mock.Verify(<@ coreApi.CloseStream() @>, Times.atleastonce)

