// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Test.Yaaf.Xml

open Yaaf.TestHelper
open Yaaf.Xml
open System.IO
open System.Xml.Linq
open NUnit.Framework
open FsUnit
open Swensen.Unquote
open Foq


[<TestFixture>]
type ``Test-Yaaf-Xml-Core: Check Conversations``() = 
    inherit Yaaf.TestHelper.MyTestClass()
    
    [<Test>]
    member this.``check that we can convert ElemInfos``() =
        let elemInfo =
            {
                Prefix = ""
                Localname = "stream"    
                Namespace = "yaaf.de/streamNamespace"
                Attributes =
                    [
                        "xmlns", { Prefix = ""; Localname = "xmlns"; Namespace = xmlnsPrefix; Value = "yaaf.de/streamNamespace" }
                        "test1", { Prefix = ""; Localname = "test1"; Namespace = ""; Value = "test1value" }
                        "test2", { Prefix = ""; Localname = "test2"; Namespace = ""; Value = "test2value" }
                    ] |> Map.ofList
            }
        let expected = XElement.Parse "<stream xmlns='yaaf.de/streamNamespace' test1='test1value' test2='test2value' />"
        let xelem = convertElemInfoToXElement elemInfo
        test <@ equalXNode xelem expected @>
        
    [<Test>]
    member this.``check that we can convert ElemInfos with prefix``() =
        let elemInfo =
            {
                Prefix = "stream"
                Localname = "stream"    
                Namespace = "yaaf.de/streamNamespace"
                Attributes =
                    [
                        "test1", { Prefix = ""; Localname = "test1"; Namespace = ""; Value = "test1value" }
                        "test2", { Prefix = ""; Localname = "test2"; Namespace = ""; Value = "test2value" }
                        "xmlns:stream", { Prefix = "xmlns"; Localname = "stream"; Namespace = xmlnsPrefix; Value = "yaaf.de/streamNamespace" }
                    ] |> Map.ofList
            }
        let expected = XElement.Parse "<stream:stream xmlns:stream='yaaf.de/streamNamespace' test1='test1value' test2='test2value' />"
        let xelem = convertElemInfoToXElement elemInfo
        test <@ equalXNode xelem expected @>

    [<Test>]
    member this.``check that we can convert ElemInfos with xmlns namespace``() =
        let elemInfo =
            {
                Prefix = "stream"
                Localname = "stream"    
                Namespace = "yaaf.de/streamNamespace"
                Attributes =
                    [
                        "xmlns", { Prefix = ""; Localname = "xmlns"; Namespace = xmlnsPrefix; Value = "defaultnamespace" }
                        "xmlns:stream", { Prefix = "xmlns"; Localname = "stream"; Namespace = xmlnsPrefix; Value = "yaaf.de/streamNamespace" }
                    ] |> Map.ofList
            }
        let expected = XElement.Parse "<stream:stream xmlns:stream='yaaf.de/streamNamespace' xmlns='defaultnamespace' />"
        let xelem = convertElemInfoToXElement elemInfo
        test <@ equalXNode xelem expected @>

        
    [<Test>]
    member this.``check that invalid ElemInfo elements throw when attribute is missing for stream prefix``() =
        let elemInfo =
            {
                Prefix = "stream"
                Localname = "stream"    
                Namespace = "yaaf.de/streamNamespace"
                Attributes =
                    [
                        "xmlns", { Prefix = ""; Localname = "xmlns"; Namespace = xmlnsPrefix; Value = "defaultnamespace" }
                    ] |> Map.ofList
            }
        // missing attribute for stream prefix
        raises<exn> <@ convertElemInfoToXElement elemInfo @>
        
    [<Test>]
    member this.``check that invalid ElemInfo elements throw when attribute is invalid for stream prefix``() =
        let elemInfo =
            {
                Prefix = "stream"
                Localname = "stream"    
                Namespace = "yaaf.de/streamNamespace"
                Attributes =
                    [
                        "xmlns", { Prefix = ""; Localname = "xmlns"; Namespace = xmlnsPrefix; Value = "defaultnamespace" }
                        "xmlns:other", { Prefix = "xmlns"; Localname = "other"; Namespace = xmlnsPrefix; Value = "yaaf.de/streamNamespace" }
                    ] |> Map.ofList
            }
        // invalid attribute for stream prefix
        raises<exn> <@ convertElemInfoToXElement elemInfo @>
        
    [<Test>]
    member this.``check that invalid ElemInfo elements throw when attribute is invalid for no prefix``() =
        let elemInfo =
            {
                Prefix = ""
                Localname = "stream"    
                Namespace = "yaaf.de/streamNamespace"
                Attributes =
                    [
                        "xmlns:stream", { Prefix = "xmlns"; Localname = "stream"; Namespace = xmlnsPrefix; Value = "yaaf.de/streamNamespace" }
                    ] |> Map.ofList
            }
        // invalid attribute for stream prefix
        raises<exn> <@ convertElemInfoToXElement elemInfo @>
        
    [<Test>]
    member this.``check that invalid ElemInfo elements throw when attribute is different for prefix``() =
        let elemInfo =
            {
                Prefix = "stream"
                Localname = "stream"    
                Namespace = "yaaf.de/streamNamespace"
                Attributes =
                    [
                        "xmlns:stream", { Prefix = "xmlns"; Localname = "stream"; Namespace = xmlnsPrefix; Value = "yaaf.de/otherNamespace" }
                    ] |> Map.ofList
            }
        // invalid attribute for stream prefix
        raises<exn> <@ convertElemInfoToXElement elemInfo @>
        
    [<Test>]
    member this.``check that invalid ElemInfo elements throw when attribute is different for no prefix``() =
        let elemInfo =
            {
                Prefix = ""
                Localname = "stream"    
                Namespace = "yaaf.de/streamNamespace"
                Attributes =
                    [
                        "xmlns", { Prefix = "xmlns"; Localname = "stream"; Namespace = xmlnsPrefix; Value = "yaaf.de/otherNamespace" }
                    ] |> Map.ofList
            }
        // invalid attribute for stream prefix
        raises<exn> <@ convertElemInfoToXElement elemInfo @>

    [<Test>]
    member this.``check that invalid ElemInfo elements throw when no xmlns attribute is given but elem has prefix``() =
        let elemInfo =
            {
                Prefix = "stream"
                Localname = "stream"    
                Namespace = "yaaf.de/streamNamespace"
                Attributes =
                    [
                    ] |> Map.ofList
            }
        // no xmlns attribute!
        raises<exn> <@ convertElemInfoToXElement elemInfo @>

        
    [<Test>]
    member this.``check that valid ElemInfo elements without namespace and without prefix work``() =
        let elemInfo =
            {
                Prefix = ""
                Localname = "stream"    
                Namespace = ""
                Attributes =
                    [
                        "test1", { Prefix = ""; Localname = "test1"; Namespace = ""; Value = "test1value" }
                    ] |> Map.ofList
            }
        let expected = XElement.Parse "<stream test1='test1value' />"
        let xelem = convertElemInfoToXElement elemInfo
        test <@ equalXNode xelem expected @>