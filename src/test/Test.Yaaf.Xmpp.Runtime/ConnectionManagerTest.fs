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
type ``Test-Yaaf-Xmpp-Server-ConnetionManager: Check that the ConnectionManager works properly``() =
    inherit MyTestClass()
    let refreshTime = 50
    let waitRefresh () = System.Threading.Thread.Sleep refreshTime
    [<Test>]
    member x.``Test that component connections are shown`` () =
      let mgr = new ConnectionManager("yaaf.de")
      let cancel = new TaskCompletionSource<_>()
      let acceptComponent = 
        Mock<IXmppClient>()
          .Setup(fun c -> <@ c.ConnectTask @>).Returns(Task.FromResult (JabberId.Parse "component.yaaf.de"))
          .Setup(fun c -> <@ c.StreamType @>).Returns(StreamType.ComponentStream true)
          .Setup(fun c -> <@ c.Exited @>).Returns(cancel.Task)
          .Setup(fun c -> <@ c.CloseConnection(any()) @>).Calls<bool>(fun force -> cancel.SetResult None; Task.FromResult ())
          .Create()
      test <@ mgr.FilterConnections(IsComponent) |> Seq.isEmpty @>
      mgr.RegisterIncommingConnection(acceptComponent)
      waitRefresh()
      test <@ mgr.FilterConnections(IsComponent) |> Seq.toList = [ acceptComponent ] @>
      cancel.SetResult None
      waitRefresh()
      test <@ mgr.FilterConnections(IsComponent) |> Seq.isEmpty @>
     
      let cancel = new TaskCompletionSource<_>() 
      let outgoingComponent = 
        Mock<IXmppClient>()
          .Setup(fun c -> <@ c.ConnectTask @>).Returns(Task.FromResult (JabberId.Parse "component2.yaaf.de"))
          .Setup(fun c -> <@ c.StreamType @>).Returns(StreamType.ComponentStream false)
          .Setup(fun c -> <@ c.Exited @>).Returns(cancel.Task)
          .Setup(fun c -> <@ c.CloseConnection(any()) @>).Calls<bool>(fun force -> cancel.SetResult None; Task.FromResult ())
          .Create()
          
      mgr.RegisterIncommingConnection(outgoingComponent)
      
      waitRefresh()
      test <@ mgr.FilterConnections(IsComponent) |> Seq.toList = [ outgoingComponent ] @>
      
      let acceptComponent = 
        Mock<IXmppClient>()
          .Setup(fun c -> <@ c.ConnectTask @>).Returns(Task.FromResult (JabberId.Parse "component.yaaf.de"))
          .Setup(fun c -> <@ c.StreamType @>).Returns(StreamType.ComponentStream true)
          .Setup(fun c -> <@ c.Exited @>).Returns(cancel.Task)
          .Setup(fun c -> <@ c.CloseConnection(any()) @>).Calls<bool>(fun force -> cancel.SetResult None; Task.FromResult ())
          .Create()
      
      mgr.RegisterIncommingConnection(acceptComponent)
      waitRefresh()
      test <@ mgr.FilterConnections(IsComponent) |> Seq.toList = [ acceptComponent; outgoingComponent ] @>
      
      cancel.SetResult None
      waitRefresh()
      test <@ mgr.FilterConnections(IsComponent) |> Seq.isEmpty @>