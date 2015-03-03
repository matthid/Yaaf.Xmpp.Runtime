// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Test.Yaaf.Xmpp

open System
open System.IO
open Yaaf.Xml
open System.Xml.Linq
open NUnit.Framework
open FsUnit
open Yaaf.FSharp.Control

open Yaaf.Sasl

open Yaaf.Xmpp
open Yaaf.Xmpp.Stream
open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.Runtime.OpenHandshake

open Test.Yaaf.Xml.XmlTestHelper
open Test.Yaaf.Xmpp.TestHelper

open Yaaf.TestHelper
open Yaaf.Helper
open Foq
open Swensen.Unquote

[<TestFixture>]
type ``Stream Opening``() = 
    inherit MyXmlTestClass()

    // TODO Simplify me please (IE don't use reader and writer but just hardcode the XElements where possible.

    /// TODO: We have to timeout on this, as this is not enough data for the xmlreader
    [<Test>]
    member this.``Test that we get StreamErrorException when xml is invalid`` () = 
        let reader, writer, d = initTestRaw ()
        let readTask = readOpenXElement reader |> Async.StartAsTask
        
        writer ("< <")
        raises<XmlException> <@ readTask |> waitTask @>

    [<Test>]
    member this.``Test that we fail on stream start-tag (without namespace)`` () = 
        let reader, writer, d = initTestRaw ()
        let readTask = readOpenXElement reader |> Async.StartAsTask
        
        writer ("<stream>")
        try
            readTask |> waitTask |> toOpenInfo |> ignore
        with
        | :? StreamErrorException as error -> error.ErrorType |> should be (equal XmlStreamError.InvalidNamespace)

    [<Test>]
    member this.``Test that we read start tag with xdecl but without namespace`` () = 
        let reader, writer, d = initTestRaw ()
        let readTask = readOpenXElement reader |> Async.StartAsTask
        
        writer ("<?xml version='1.0'?><stream>")
        try
            readTask |> waitTask |> toOpenInfo |> ignore
        with
        | :? StreamErrorException as error -> error.ErrorType |> should be (equal XmlStreamError.InvalidNamespace)

    [<Test>]
    member this.``Test that we read start tag with xdecl`` () = 
        let reader, writer, d = initTestRaw ()
        let readTask = readOpenXElement reader |> Async.StartAsTask
        
        writer ("<?xml version='1.0'?><stream xmlns='http://etherx.jabber.org/streams'>")
        let openInfo = readTask |> waitTask |> toOpenInfo
        openInfo.From |> should be  (equal <|  None)
        openInfo.To |> should be  (equal <|  None)
        openInfo.Version |> should be  (equal <|  None)
        openInfo.Id |> should be (equal <|  None)

    [<Test>]
    member this.``Test that we can parse startElement`` () = 
        let reader, writer, disposeWriter = initTestRaw ()
        use reader = reader
        use disposeWriter = disposeWriter
        let readTask = readOpenXElement reader |> Async.StartAsTask

        writer ("<?xml version='1.0'?>
   <stream:stream
       from='juliet@im.example.com'
       to='im.example.com'
       version='1.0'
       xml:lang='en'
       xmlns='jabber:client'
       xmlns:stream='http://etherx.jabber.org/streams'>")
        let openInfo = readTask |> waitTask |> toOpenInfo
        openInfo.From |> should be (equal <| Some (JabberId.Parse "juliet@im.example.com"))
        openInfo.To |> should be (equal <| Some (JabberId.Parse "im.example.com"))
        openInfo.Version |> should be (equal <| Some (Version.Parse "1.0"))
        openInfo.Id |> should be (equal <|  None)

        
[<TestFixture>]
type ``Stream Elements``() = 
    inherit MyTestClass()
    [<Test>]
    member this.``Test that we can read simple elements after opening the stream`` () = 
        let reader, writer, d = initTestRaw ()
        let readTask = readOpenXElement reader |> Async.StartAsTask
        
        writer ("<?xml version='1.0'?><stream xmlns='http://etherx.jabber.org/streams'>")
        let openInfo = readTask |> waitTask |> toOpenInfo

        writer ("<a/>")
        let elem = readXElementOrClose reader |> Async.StartAsTaskImmediate |> waitTask
        elem.IsSome |> should be True
        let elem = elem.Value
        elem.Name.LocalName |> should be (equal "a")
        writer ("<b />")
        let elem = readXElementOrClose reader |> Async.StartAsTaskImmediate |> waitTask
        elem.IsSome |> should be True
        let elem = elem.Value
        elem.Name.LocalName |> should be (equal "b")
        writer ("<test > <inner /> </test>")
        let elem = readXElementOrClose reader |> Async.StartAsTaskImmediate |> waitTask
        elem.IsSome |> should be True
        let elem = elem.Value
        elem.Name.LocalName |> should be (equal "test")


    [<Test>]
    member this.``Test that we throw errors on invalid xml within data`` () = 
        let reader, writer, d = initTestRaw ()
        let readTask = readOpenXElement reader |> Async.StartAsTask
        
        writer ("<?xml version='1.0'?><stream xmlns='http://etherx.jabber.org/streams'>")
        let openInfo = readTask |> waitTask

        writer ("<a/>")
        let elem = readXElementOrClose reader |> Async.StartAsTaskImmediate |> waitTask
        elem.IsSome |> should be True
        let elem = elem.Value
        elem.Name.LocalName |> should be (equal "a")
        writer ("<b />")
        let elem = readXElementOrClose reader |> Async.StartAsTaskImmediate |> waitTask
        elem.IsSome |> should be True
        let elem = elem.Value
        elem.Name.LocalName |> should be (equal "b")
        // NOTE: This fails on the .net implementation
        writer ("<test > <inner > </test>")

        // TODO: We have to timeout on this, as this is not enough data for the xmlreader
        raises<XmlException> <@ readXElementOrClose reader |> Async.StartAsTaskImmediate |> waitTask @>

    [<Test>]
    member this.``Test that we throw errors directly after opening`` () = 
        let reader, writer, d = initTestRaw ()
        let readTask = readOpenXElement reader |> Async.StartAsTask
        
        writer ("<?xml version='1.0'?><stream xmlns='http://etherx.jabber.org/streams'>")
        let openInfo = readTask |> waitTask

        writer ("<<> >")
        raises<XmlException> <@ readXElementOrClose reader |> Async.StartAsTaskImmediate |> waitTask @>


open Yaaf.Xmpp.XmlStanzas
open Yaaf.Xmpp.XmlStanzas.Parsing

[<TestFixture>]
type ``Stream change Streamnamespace Tests``() = 
    inherit MyTestClass()
    
    [<Test>]
    member this.``Test that we can read and write with different namespaces`` () = 
        let stanza = "<message xmlns='jabber:client' to='juliet@capulet.com'>
  <body>Hello?</body>
  <html xmlns='http://jabber.org/protocol/xhtml-im'>
    <body xmlns='http://www.w3.org/1999/xhtml'>
      <p style='font-weight:bold'>Hello?</p>
    </body>
  </html>
</message>"
        let elem = XElement.Parse stanza
        let stanza = parseStanzaElementNoError "jabber:client" elem

        let elem2 = createStanzaElement "jabber:server" stanza
        let expected = "<message xmlns='jabber:server' to='juliet@capulet.com'>
  <body>Hello?</body>
  <html xmlns='http://jabber.org/protocol/xhtml-im'>
    <body xmlns='http://www.w3.org/1999/xhtml'>
      <p style='font-weight:bold'>Hello?</p>
    </body>
  </html>
</message>"
        let expectedElem = XElement.Parse expected  
        
        let result = equalXNodeAdvanced elem2 expectedElem
        if not result.IsEqual then
            Assert.Fail (result.Message)
            
    [<Test>]
    member this.``Test that we can read and write with different namespaces (namespaces as attributes)`` () = 
        let stanza = "<message xmlns='jabber:client' to='juliet@capulet.com'>
  <test:other xmlns:test='ignore'/>
  <unknown xmlns='some:other'>
    <t:body xmlns:t='jabber:client'></t:body>
  </unknown>
</message>"
        let elem = XElement.Parse stanza
        let stanza = parseStanzaElementNoError "jabber:client" elem

        let elem2 = createStanzaElement "jabber:server" stanza
        let expected = "<message xmlns='jabber:server' to='juliet@capulet.com'>
  <test:other xmlns:test='ignore'/>
  <unknown xmlns='some:other'>
    <t:body xmlns:t='jabber:server'></t:body>
  </unknown>
</message>"
        let expectedElem = XElement.Parse expected  
        
        let result = equalXNodeAdvanced elem2 expectedElem
        if not result.IsEqual then
            Assert.Fail (result.Message)

[<TestFixture>]
type ``Stream XmlStanza Parsing``() = 
    inherit XmlStanzaParsingTestClass()

    [<Test>]
    member this.``Test that we can read and write message Stanzas`` () = 
        let stanza = "<message to='juliet@capulet.com'>
  <body>Hello?</body>
  <html xmlns='http://jabber.org/protocol/xhtml-im'>
    <body xmlns='http://www.w3.org/1999/xhtml'>
      <p style='font-weight:bold'>Hello?</p>
    </body>
  </html>
</message>"
        let info = this.Test stanza
        info.Header.  StanzaType |> should be (equal XmlStanzaType.Message)
        info.Contents.Children |> Seq.length |> should be (equal 2)
        info.Header.  To |> should be (equal (Some <| JabberId.Parse "juliet@capulet.com"))
        info.Header.  From |> should be (equal None)
        info.Header.  Type |> should be (equal None)
        info.Header.  Id |> should be (equal None)
        info.Contents.CustomAttributes |> Seq.length |> should be (equal 0)

         
    [<Test>]
    member this.``Test that we can read and write iq error Stanzas`` () = 
        let stanza = "<iq id='wy2xa82b4' type='error'>
     <error type='modify'>
       <bad-request xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/>
     </error>
   </iq>"
        let info = this.Test stanza
        info.Header.  StanzaType |> should be (equal XmlStanzaType.Iq)
        info.Contents.Children |> Seq.length |> should be (equal 1)
        info.Header.  To |> should be (equal None)
        info.Header.  From |> should be (equal None)
        info.Header.  Type |> should be (equal (Some "error"))
        info.Header.  Id |> should be (equal (Some "wy2xa82b4"))
        info.Contents. CustomAttributes |> Seq.length |> should be (equal 0)
        
         
    [<Test>]
    member this.``Test that validate iq stanzas without type`` () = 
        let stanza = "<iq id='wy2xa82b4'>
     <error type='modify'>
       <bad-request xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/>
     </error>
   </iq>"
        let raw = this.Test stanza
        this.ValidateShouldThrow raw
        
    [<Test>]
    member this.``Test that we can read and write presence Stanzas`` () = 
        let stanza = "<presence from='romeo@example.net/orchard' xml:lang='en'>
  <show>dnd</show>
  <status>Wooing Juliet</status>
  <status xml:lang='cs'>Dvo&#x0159;&#x00ED;m se Julii</status>
</presence>"
        let info = this.Test stanza
        info.Header.  StanzaType |> should be (equal XmlStanzaType.Presence)
        info.Contents.Children |> Seq.length |> should be (equal 3)
        info.Header.  To |> should be (equal None)
        info.Header.  From |> should be (equal (Some <| JabberId.Parse "romeo@example.net/orchard"))
        info.Header.  Type |> should be (equal None)
        info.Header.  Id |> should be (equal None)
        info.Contents.CustomAttributes |> Seq.length |> should be (equal 1)