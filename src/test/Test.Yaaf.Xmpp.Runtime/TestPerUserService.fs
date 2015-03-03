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
open Test.Yaaf.Xmpp

open Yaaf.DependencyInjection
open Yaaf.Xmpp
open Yaaf.Xmpp.Server
open Yaaf.Xmpp.Stream
open Yaaf.Xmpp.Runtime
open System.Threading.Tasks
open Yaaf.IO
open Yaaf.Helper
open Yaaf.TestHelper
open Foq
open Swensen.Unquote
type ITestService =
    abstract Test : string with get, set
type TestService () =
    let mutable test = "init"
    interface ITestService with
        member x.Test with get () = test and set v = test <- v

[<TestFixture>]
type ``Test-Yaaf-Xmpp-Runtime-Server-PerUserService: Check that the per-user service works as expected``() =
    inherit MyTestClass()
    
    [<Test>]
    member this.``Check if we get the same instance with different resources`` () =
        let perUser = XmppServerUserServicePlugin(Mock<IServerApi>().Create(), SimpleInjectorKernelCreator.CreateKernel()) :> IPerUserService
        
        perUser.RegisterService<ITestService, TestService>()
        let noResource = perUser.ForUser (JabberId.Parse "test@nunit.org")
        let noResourceTestService = noResource.GetService<ITestService>()
        noResourceTestService.Test <- "some data"
        test <@  noResource.GetService<ITestService>().Test = "some data" @>
        
        let resource1 = perUser.ForUser (JabberId.Parse "test@nunit.org/resource")
        let resource1TestService = resource1.GetService<ITestService>()
        test <@  resource1TestService.Test = "some data" @>
        resource1TestService.Test <- "once again"

        let resource2 = perUser.ForUser (JabberId.Parse "test@nunit.org/more")
        let resource2TestService = resource2.GetService<ITestService>()
        test <@  resource2TestService.Test = "once again" @>