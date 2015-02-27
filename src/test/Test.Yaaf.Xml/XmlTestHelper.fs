// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
module Test.Yaaf.Xml.XmlTestHelper
open Yaaf.Helper
open NUnit.Framework
open Mono.System.Xml
open System.Xml.Linq
open Yaaf.TestHelper
open Yaaf.Xml
open FsUnit

type MyXmlTestClass() = 
    inherit MyStreamTestClass()
    let mutable xmlReader1 = Unchecked.defaultof<_>
    let mutable xmlWriter1 = Unchecked.defaultof<_>
    let mutable worker = None
    override this.Setup() = 
        base.Setup()
        worker <- Some (new WorkerThread())
        worker.Value.SetWorker()
        xmlReader1 <- createFixedReader this.Stream1 (new XmlReaderSettings(Async = true))
        xmlWriter1 <- XmlWriter.Create(this.Stream1, new XmlWriterSettings(Async = true))
    

    override this.TearDown() = 
        worker.Value.Dispose()
        worker <- None
        base.TearDown()
    member x.XmlReader1 with get () = xmlReader1
    member x.XmlWriter1 with get () = xmlWriter1
//[<Test>]
//member x.``Check XmlTestClass Setup and TearDown`` () = ()
