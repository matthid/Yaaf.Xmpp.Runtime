// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Test.Yaaf.Xmpp.Runtime.Features

open Yaaf.TestHelper
open Yaaf.Xml
open Yaaf.Xmpp
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.Features
open System.IO
open System.Xml.Linq
open NUnit.Framework
open FsUnit
open Swensen.Unquote
open Foq


[<TestFixture>]
type TestBindFeature() = 
    inherit Yaaf.TestHelper.MyTestClass()

    [<Test>]
    member this.``check that ResourceManager is only required on the server side``() =
        let bindConfig = Mock<IBindConfig>().Create()
        let runtimeConfig_server1 = 
            Mock<IRuntimeConfig>()
                .Setup(fun x -> <@ x.StreamType @>).Returns(StreamType.ServerStream)
                .Create()
        let saslService = Mock<ISaslService>().Create()
        let coreApi = Mock<ICoreStreamApi>().Create()

        raises<ConfigurationException> <@ BindFeature(bindConfig, runtimeConfig_server1, saslService, coreApi) @>
        
        let runtimeConfig_server2 = 
            Mock<IRuntimeConfig>()
                .Setup(fun x -> <@ x.StreamType @>).Returns(StreamType.ClientStream)
                .Setup(fun x -> <@ x.IsInitializing @>).Returns(false)
                .Create()
                
        raises<ConfigurationException> <@ BindFeature(bindConfig, runtimeConfig_server2, saslService, coreApi) @>

        let runtimeConfig_client = 
            Mock<IRuntimeConfig>()
                .Setup(fun x -> <@ x.StreamType @>).Returns(StreamType.ClientStream)
                .Setup(fun x -> <@ x.IsInitializing @>).Returns(true)
                .Create()
        let bindFeature = BindFeature(bindConfig, runtimeConfig_client, saslService, coreApi)
        ()