// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

namespace Test.Yaaf.Xmpp
open System
open System.IO
open Mono.System.Xml
open System.Xml.Linq
open NUnit.Framework
open FsUnit
open FSharp.Control
open Test.Yaaf.Xmpp.TestHelper
open Yaaf.Xmpp
open Yaaf.Xml
open Yaaf.Xmpp.Stream
open Yaaf.Logging

    
[<TestFixture>]
type JabberIdTest() = 
    [<Test>]
    member this.``Test Simple JabberId parsing``  () = 
        let jabberId = "user@domain.com/resource"
        let parsed = JabberId.Parse jabberId
        parsed.Domainpart |> should be (equal "domain.com")
        parsed.Localpart |> should be (equal (Some "user"))
        parsed.Resource |> should be (equal (Some "resource"))
        parsed.BareId |> should be (equal "user@domain.com")
        parsed.FullId |> should be (equal jabberId)
    [<Test>]
    member this.``Test JabberId parsing without resource``  () = 
        let jabberId = "user@domain.com"
        let parsed = JabberId.Parse jabberId
        parsed.Domainpart |> should be (equal "domain.com")
        parsed.Localpart |> should be (equal (Some "user"))
        parsed.Resource |> should be (equal None)
        parsed.BareId |> should be (equal "user@domain.com")
        parsed.FullId |> should be (equal jabberId)
    [<Test>]
    member this.``Test JabberId parsing without Localpart``  () = 
        let jabberId = "domain.com"
        let parsed = JabberId.Parse jabberId
        parsed.Domainpart |> should be (equal "domain.com")
        parsed.Localpart |> should be (equal None)
        parsed.Resource |> should be (equal None)
        parsed.BareId |> should be (equal "domain.com")
        parsed.FullId |> should be (equal jabberId)
        
    [<Test>]
    member this.``Test JabberId parsing with domain and resource``  () = 
        let jabberId = "domain.com/resource"
        let parsed = JabberId.Parse jabberId
        parsed.Domainpart |> should be (equal "domain.com")
        parsed.Localpart |> should be (equal None)
        parsed.Resource |> should be (equal (Some "resource"))
        parsed.BareId |> should be (equal "domain.com")
        parsed.FullId |> should be (equal jabberId)
    [<Test>]
    member this.``Test JabberId isSpecialOf (1)``  () = 
        let jabberId1 = "domain.com/resource"
        let jabberId2 = "domain.com"
        let j1 = JabberId.Parse jabberId1
        let j2 = JabberId.Parse jabberId2
        j1.IsSpecialOf j2 |> should be True
    [<Test>]
    member this.``Test JabberId isSpecialOf (2)``  () = 
        let jabberId1 = "domain.com/resource"
        let jabberId2 = "domain2.com"
        let j1 = JabberId.Parse jabberId1
        let j2 = JabberId.Parse jabberId2
        j1.IsSpecialOf j2 |> should be False
    [<Test>]
    member this.``Test JabberId isSpecialOf (3)``  () = 
        let jabberId1 = "user@domain.com"
        let jabberId2 = "domain.com"
        let j1 = JabberId.Parse jabberId1
        let j2 = JabberId.Parse jabberId2
        j1.IsSpecialOf j2 |> should be True
    [<Test>]
    member this.``Test JabberId isSpecialOf (4)``  () = 
        let jabberId1 = "user@domain.com"
        let jabberId2 = "domain2.com"
        let j1 = JabberId.Parse jabberId1
        let j2 = JabberId.Parse jabberId2
        j1.IsSpecialOf j2 |> should be False
    [<Test>]
    member this.``Test JabberId isSpecialOf (5)``  () = 
        let jabberId1 = "user@domain.com/resource"
        let jabberId2 = "user@domain.com"
        let j1 = JabberId.Parse jabberId1
        let j2 = JabberId.Parse jabberId2
        j1.IsSpecialOf j2 |> should be True
    [<Test>]
    member this.``Test JabberId isSpecialOf (6)``  () = 
        let jabberId1 = "user@domain.com/resource"
        let jabberId2 = "user@domain2.com"
        let j1 = JabberId.Parse jabberId1
        let j2 = JabberId.Parse jabberId2
        j1.IsSpecialOf j2 |> should be False
    [<Test>]
    member this.``Test JabberId isSpecialOf (7)``  () = 
        let jabberId1 = "user@domain.com/resource"
        let jabberId2 = "user1@domain.com"
        let j1 = JabberId.Parse jabberId1
        let j2 = JabberId.Parse jabberId2
        j1.IsSpecialOf j2 |> should be False
    [<Test>]
    member this.``Test JabberId isSpecialOf (8)``  () = 
        let jabberId1 = "user@domain.com/resource"
        let jabberId2 = "domain.com"
        let j1 = JabberId.Parse jabberId1
        let j2 = JabberId.Parse jabberId2
        j1.IsSpecialOf j2 |> should be True
    [<Test>]
    member this.``Test JabberId isSpecialOf (9)``  () = 
        let jabberId1 = "user@domain.com/resource"
        let jabberId2 = "domain2.com"
        let j1 = JabberId.Parse jabberId1
        let j2 = JabberId.Parse jabberId2
        j1.IsSpecialOf j2 |> should be False


