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
open Test.Yaaf.Xmpp.TestHelper
open Test.Yaaf.Xml.XmlTestHelper
open Test.Yaaf.Xmpp
open Test.Yaaf.Xmpp.DevelopmentCertificate
open Yaaf.Xmpp
open Yaaf.Xmpp.Server
open Yaaf.Xmpp.Stream
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.OpenHandshake
open Yaaf.Xmpp.Runtime.ComponentHandshake
open System.Threading.Tasks
open Yaaf.IO
open Yaaf.Helper
open Yaaf.TestHelper
open Foq
open Swensen.Unquote

[<TestFixture>]
type ``Test-Yaaf-Xmpp-Runtime-ComponentHandshake-ComponentNegotiationPlugin: Check that the plugin works properly``() =
    inherit MyTestClass()
    
    let baseTest domain streamId serverComponents clientComponent =
        let runtimeConfigClient = 
            Mock<IRuntimeConfig>()
                .Setup(fun x -> <@ x.IsInitializing @>).Returns(true)
                .Setup(fun x -> <@ x.RemoteJabberId @>).Returns(Some domain)
                .Setup(fun x -> <@ x.StreamType @>).Returns(StreamType.ComponentStream true)
                .Create()
                
        let runtimeConfigServer = 
            Mock<IRuntimeConfig>()
                .Setup(fun x -> <@ x.IsInitializing @>).Returns(false)
                .Setup(fun x -> <@ x.StreamType @>).Returns(StreamType.ComponentStream true)
                .Create()
                
        let configClient = 
            Mock<IComponentsConfig>()
                .Setup(fun x -> <@ x.Components @>).Returns([clientComponent])
                .Create()
                
        let configServer = 
            Mock<IComponentsConfig>()
                .Setup(fun x -> <@ x.Components @>).Returns(serverComponents : ComponentConfig list)
                .Create()
        // This is very weird but true, see http://xmpp.org/extensions/xep-0114.html#nt-idp1519648
        let clientOpenInfo = { StreamOpenInfo.Empty with To = Some (JabberId.Parse clientComponent.Subdomain) }
        let serverOpenInfo = { StreamOpenInfo.Empty with From = Some (JabberId.Parse clientComponent.Subdomain) }
        let openInfoServer =
            {
                RemoteOpenInfo = clientOpenInfo
                OpenInfo = serverOpenInfo
                StreamId = streamId
            }
        let openInfoClient =
            {
                RemoteOpenInfo = serverOpenInfo
                OpenInfo = clientOpenInfo
                StreamId = streamId
            }
        let openInfoServiceServer =
            Mock<ICoreStreamOpenerService>()
                .Setup(fun x -> <@ x.Info @>).Returns(openInfoServer)
                .Create()
        let openInfoServiceClient =
            Mock<ICoreStreamOpenerService>()
                .Setup(fun x -> <@ x.Info @>).Returns(openInfoClient)
                .Create()
        let readServer = TaskCompletionSource<StreamElement>()
        let writtenServer = TaskCompletionSource<_>()
        let xmlStreamServer =
            Mock<IXmlStream>()
                //.Setup(fun x -> <@ x.TryRead() @>).Returns(async.Return None)
                .Setup(fun x -> <@ x.TryRead() @>)
                //.Calls<unit>(fun ()-> 
                .ReturnsFunc(fun () ->
                    async {
                        let! read = readServer.Task |> Task.await
                        return Some read
                    })
                .Setup(fun x -> <@ x.Write(any()) @>)
                .Calls<StreamElement>(fun elem -> 
                    async {
                        writtenServer.SetResult(elem)
                        return ()
                    })
                .Create()
                    
        let readClient = TaskCompletionSource<StreamElement>()
        let writtenClient = TaskCompletionSource<_>()
        let xmlStreamClient =
            Mock<IXmlStream>()
                //.Setup(fun x -> <@ x.TryRead() @>).Returns(async.Return None)
                .Setup(fun x -> <@ x.TryRead() @>)
                //.Calls<unit>(fun () -> 
                .ReturnsFunc(fun () ->
                    async {
                        let! read = readClient.Task |> Task.await
                        return Some read
                    })
                .Setup(fun x -> <@ x.Write(any()) @>)
                .Calls<StreamElement>(fun elem -> 
                    async {
                        writtenClient.SetResult(elem)
                        return ()
                    })
                .Create()
        
        let coreApiServer = 
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.AbstractStream @>).Returns(xmlStreamServer)
                .Create()
                
        let coreApiClient = 
            Mock<ICoreStreamApi>()
                .Setup(fun x -> <@ x.AbstractStream @>).Returns(xmlStreamClient)
                .Create()
    
        let exclusiveSend = 
            Mock<IExclusiveSend>()
                .Setup(fun x -> <@ x.DoWork (any()) @>)
                .Calls<unit -> Async<unit>>(fun work -> work ())
                .Create()
        let registrar =
            Mock<IPluginManagerRegistrar>()
                .Setup(fun x -> <@ x.RegisterFor<IXmlPipelinePlugin> (any()) @>)
                .Returns(())
                .Create()
        let serverPlugin = new ComponentNegotiationPlugin(runtimeConfigServer, coreApiServer, openInfoServiceServer, configServer, exclusiveSend, registrar) :> IXmlPipelinePlugin
        let clientPlugin = new ComponentNegotiationPlugin(runtimeConfigClient, coreApiClient, openInfoServiceClient, configClient, exclusiveSend, registrar) :> IXmlPipelinePlugin
        let negServiceServer = serverPlugin :?> INegotiationService
        let negServiceClient = clientPlugin :?> INegotiationService
        let serverTask = serverPlugin.StreamOpened() |> Async.StartAsTask
        let clientTask = clientPlugin.StreamOpened() |> Async.StartAsTask
        
        // the initiating entity (in this case the client) write the first entry
        let writtenOnClient = writtenClient.Task |> waitTask
        readServer.SetResult writtenOnClient
        // server finishes
        serverTask |> waitTask
        let writtenOnServer = writtenClient.Task |> waitTask
        readClient.SetResult writtenOnServer
        // client finishes
        clientTask |> waitTask
        
        // Negotiation should be completed
        test <@ negServiceServer.NegotiationCompleted @>
        test <@ negServiceClient.NegotiationCompleted @>
        let inline isTaskOk (task:Task)=
            task.IsCompleted && not task.IsFaulted && not task.IsCanceled
        test <@ isTaskOk negServiceServer.NegotiationTask @>
        test <@ isTaskOk negServiceClient.NegotiationTask @>
        test <@ isTaskOk negServiceServer.ConnectionTask @>
        test <@ isTaskOk negServiceClient.ConnectionTask @>
        ()

    let baseNUnitTest = baseTest (JabberId.Parse "nunit.org")
    let baseStreamIdTest = baseNUnitTest "StreamIdConstant"

    [<Test>]
    member this.``Check if invalid secret doesn't work`` () =
        let serverComponents = [ { Subdomain = "comp.nunit.org"; Secret = "unit#test!secret" }]
        let clientComponent = { Subdomain = "comp.nunit.org"; Secret = "unit#test!secre" }
        
        raises<StreamErrorException> <@ baseStreamIdTest serverComponents clientComponent @>
        
    [<Test>]
    member this.``Check if invalid domain doesn't work`` () =
        let serverComponents = [ { Subdomain = "comp.nunit.org"; Secret = "unit#test!secret" }]

        let clientComponent = { Subdomain = "other.nunit.org"; Secret = "unit#test!secret" }
        raises<StreamErrorException> <@ baseStreamIdTest serverComponents clientComponent @>
      
    [<Test>]
    member this.``Check that comp negotiation works`` () = 
        let clientComponent = { Subdomain = "comp.nunit.org"; Secret = "unit#test!secret" }

        let serverComponents = [ { Subdomain = "comp.nunit.org"; Secret = "unit#test!secret" }]
        baseStreamIdTest serverComponents clientComponent
  