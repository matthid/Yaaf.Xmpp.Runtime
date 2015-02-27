// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Test.Yaaf.Xml

open System.IO
open NUnit.Framework
open FsUnit
open System.Xml
open System.Xml.Linq
open Test.Yaaf.Xml.XmlTestHelper
open Yaaf.Helper
open Yaaf.Xml
open Yaaf.IO
open Yaaf.TestHelper
open Yaaf.Xml.AsyncXmlReader

[<TestFixture>]
type AsyncXmlReaderTest_Helpers() = 
    inherit MyXmlTestClass()
    [<Test>]
    member x.``Check that XmlReader returns finished stuff``() = ()
