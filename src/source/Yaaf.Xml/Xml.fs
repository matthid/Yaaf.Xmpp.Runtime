// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
[<AutoOpen>]
module Yaaf.Xml.Core
    
open System
open Mono.System.Xml
open System.Xml.XPath
open System.Xml.Linq

open Yaaf.Helper
open Yaaf.FSharp.Control
open FSharpx.Collections

open Yaaf.Helper
open Yaaf.Logging
open Yaaf.Logging.AsyncTracing

let xmlNs = "http://www.w3.org/XML/1998/namespace"
let xmlnsPrefix = "http://www.w3.org/2000/xmlns/"

type XmlReaderSettings = Mono.System.Xml.XmlReaderSettings
type XmlNodeType = Mono.System.Xml.XmlNodeType
type XmlWriter = Mono.System.Xml.XmlWriter
type XmlWriterSettings = Mono.System.Xml.XmlWriterSettings
type ReaderConformanceLevel = Mono.System.Xml.ConformanceLevel
type WriterConformanceLevel = Mono.System.Xml.ConformanceLevel

/// This is the internal interface we use
type XmlReader = 
    inherit IDisposable
    abstract member Settings : XmlReaderSettings with get
    abstract member BaseURI : String with get
    abstract member EOF : bool with get
    abstract member ReadAsync : unit -> Async<bool>
    abstract member NodeType : XmlNodeType with get
    abstract member MoveToContentAsync : unit -> Async<XmlNodeType>
    abstract member LocalName : string with get
    abstract member NamespaceURI : string with get
    abstract member MoveToFirstAttribute : unit -> bool
    abstract member MoveToNextAttribute : unit -> bool
    abstract member MoveToAttribute : int -> unit
    abstract member MoveToElement : unit -> bool
    abstract member IsEmptyElement : bool with get
    abstract member Value : string with get
    abstract member Name : string with get
    abstract member GetAttribute : string -> string
    abstract member Prefix : string with get
    abstract member AttributeCount : int with get
// there is no way back
let fromXmlReader (reader:Mono.System.Xml.XmlReader) = 
    { new XmlReader with 
        member x.Settings with get() = reader.Settings
        member x.BaseURI  with get() = reader.BaseURI
        member x.EOF with get() = reader.EOF
        member x.ReadAsync () = 
            match reader with
            | :? Yaaf.Xml.AsyncXmlReader.LazyMonoXmlTextReader as l -> l.MyRead()
            | _ -> reader.ReadAsync() |> Task.await
        member x.NodeType with get() = reader.NodeType
        member x.MoveToContentAsync () = 
            match reader with
            | :? Yaaf.Xml.AsyncXmlReader.LazyMonoXmlTextReader as l -> l.MyMoveToContent()
            | _ -> reader.MoveToContentAsync() |> Task.await
        member x.LocalName with get() = reader.LocalName
        member x.NamespaceURI with get() = reader.NamespaceURI
        member x.MoveToFirstAttribute () = reader.MoveToFirstAttribute()
        member x.MoveToNextAttribute () = reader.MoveToNextAttribute()
        member x.MoveToAttribute i = reader.MoveToAttribute i
        member x.MoveToElement () = reader.MoveToElement()
        member x.IsEmptyElement with get() = reader.IsEmptyElement
        member x.Value with get() = reader.Value
        member x.Name with get() = reader.Name
        member x.GetAttribute att = reader.GetAttribute att
        member x.Prefix with get() = reader.Prefix
        member x.AttributeCount with get() = reader.AttributeCount
        member x.Dispose() = reader.Dispose()
    }
[<Serializable>]
type XmlException =     
    inherit Exception
    new () = { inherit Exception() }
    new (msg : string) = { inherit Exception(msg) }
    new (msg:string, inner:Exception) = { inherit Exception(msg, inner) }       
    new (info:System.Runtime.Serialization.SerializationInfo, context:System.Runtime.Serialization.StreamingContext) = {
        inherit Exception(info, context)
    }

[<AutoOpen>]
module LinqExtensions = 
    module HelpersRead = 
        let defineDefault (o:LoadOptions) (s:XmlReaderSettings) =
            s.DtdProcessing <- DtdProcessing.Prohibit
            s.IgnoreWhitespace <- (o &&& LoadOptions.PreserveWhitespace) = (LanguagePrimitives.EnumOfValue 0)
            s.Async <- true
            s.ConformanceLevel <- ConformanceLevel.Auto
            s
        let newSettingsFromReader (o:LoadOptions) (r:XmlReader) = 
            let s = if r.Settings <> null then r.Settings.Clone () else new XmlReaderSettings ()
            s |> defineDefault o
        let binding = 
            System.Reflection.BindingFlags.Instance ||| 
            System.Reflection.BindingFlags.NonPublic ||| 
            System.Reflection.BindingFlags.Public ||| 
            System.Reflection.BindingFlags.FlattenHierarchy
        let trySetInternalProperty (name:string) (value:obj) (o:obj) = 
            try
                let t = o.GetType()
                let prop = t.GetProperty(name, binding)
                if prop <> null then
                    prop.SetValue(o, value)
            with e ->
                ()
        let fillLineInfoAndBaseUri (o:LoadOptions) (r:XmlReader) (ob:XObject) = 
                if ((o &&& LoadOptions.SetLineInfo) <> LoadOptions.None) then
                    match r :> obj with
                    | :? IXmlLineInfo as li ->
                        if li.HasLineInfo () then
                            ob |> trySetInternalProperty "LineNumber" li.LineNumber
                            ob |> trySetInternalProperty "LinePosition" li.LinePosition
                    | _ -> ()
                        
                if ((o &&& LoadOptions.SetBaseUri) <> LoadOptions.None) then
                    ob |> trySetInternalProperty "BaseUri" r.BaseURI
                    
        let rec xContainerReadContentFrom (isFinished:XmlReader -> bool) (options:LoadOptions) (reader:XmlReader) (c:XContainer) = async {   
            let rec readNext () = async {
                if not reader.EOF then
                    let! found = reader.ReadAsync ()
                    if not <| isFinished reader then
                        let! next = xNodeReadFrom true options reader
                        c.Add (next:XNode)                
                        return! readNext() }
            do! readNext()
            return () }
            
        and xElementLoadCore (current:bool) (o:LoadOptions) (r:XmlReader) = async {
            if not current then
                let! found = r.ReadAsync()
                assert found
            let! res = r.MoveToContentAsync()
            if (r.NodeType <> XmlNodeType.Element) then
                raise <| new InvalidOperationException ("The XmlReader must be positioned at an element")
            let name = XName.Get (r.LocalName, r.NamespaceURI)
            let e = new XElement (name)
            e |> fillLineInfoAndBaseUri o r
            
            if (r.MoveToFirstAttribute ()) then
                let rec nextAttribute() = 
                    // not sure how current Orcas behavior makes sense here though ...
                    if (r.LocalName = "xmlns" && r.NamespaceURI = XNamespace.Xmlns.NamespaceName) then
                        e.SetAttributeValue (XNamespace.None.GetName ("xmlns"), r.Value)
                    else
                        e.SetAttributeValue (XName.Get (r.LocalName, r.NamespaceURI), r.Value)
                        e.LastAttribute |> fillLineInfoAndBaseUri o r
                    if r.MoveToNextAttribute () then
                        nextAttribute()
                nextAttribute()
                r.MoveToElement () |> ignore
            
            if (not r.IsEmptyElement) then
                let isFinished (reader:XmlReader) = 
                    if (reader.NodeType = XmlNodeType.EndElement) then
                        if XName.Get (reader.LocalName, reader.NamespaceURI) <> name then
                            raise <| new XmlException("invalid formatted xml, endelement while trying to")
                        true
                    else
                        false
                do! e |> xContainerReadContentFrom isFinished o r
                // We are at a endElement Node
                // assert that 
                assert (r.NodeType = XmlNodeType.EndElement)
                assert (r.LocalName = e.Name.LocalName)
                assert (r.NamespaceURI = e.Name.NamespaceName)
                //r.ReadEndElement ()
            
            return e }        
        and xNodeReadFrom (current:bool) (o:LoadOptions) (r:XmlReader) : Async<XNode> = async {
            if not current then
                let! found = r.ReadAsync()
                assert found
                
            return! 
                match r.NodeType with
                //| XmlNodeType.XmlDeclaration -> async {
                //    // XDeclaration is no XNode...
                //    return XDeclaration(r.GetAttribute "version", r.GetAttribute "encoding", r.GetAttribute "standalone") :> XNode
                //    }
                | XmlNodeType.Element -> async {
                    let! elem = xElementLoadCore true o r
                    return elem :> XNode }               
                | XmlNodeType.Whitespace
                | XmlNodeType.SignificantWhitespace
                | XmlNodeType.Text -> async {
                    let t = new XText (r.Value)
                    t |> fillLineInfoAndBaseUri o r
                    return t :> XNode } 
                | XmlNodeType.CDATA -> async {
                    let t = new XCData (r.Value)
                    t |> fillLineInfoAndBaseUri o r
                    return t :> XNode } 
                | XmlNodeType.ProcessingInstruction -> async {
                    let t = new XProcessingInstruction (r.Name, r.Value)
                    t |> fillLineInfoAndBaseUri o r
                    return t :> XNode } 
                | XmlNodeType.Comment -> async {
                    let t = new XComment (r.Value)
                    t |> fillLineInfoAndBaseUri o r
                    return t :> XNode } 
                | XmlNodeType.DocumentType -> async {
                    let t = 
                        new XDocumentType (
                            r.Name, r.GetAttribute ("PUBLIC"),
                            r.GetAttribute ("SYSTEM"), r.Value)
                    t |> fillLineInfoAndBaseUri o r
                    return t :> XNode } 
                | _ -> invalidOp (sprintf "Node type %O is not supported" r.NodeType) }
    open HelpersRead  
    type System.Xml.Linq.XElement with
        static member LoadAsync (reader:XmlReader,o:LoadOptions) =
            //use r = XmlReader.Create(reader, reader |> newSettingsFromReader o)
            reader |> xElementLoadCore true o
        static member LoadAsync (reader:XmlReader) =
            XElement.LoadAsync(reader, LoadOptions.None)
    type System.Xml.Linq.XNode with
        static member ReadFromAsync (reader:XmlReader,o:LoadOptions) =
            //use r = XmlReader.Create(reader, reader |> newSettingsFromReader o)
            reader |> xNodeReadFrom true o 
        static member ReadFromAsync (reader:XmlReader) =
            XNode.ReadFromAsync(reader, LoadOptions.None)
        static member ReadFromNextAsync (reader:XmlReader,o:LoadOptions) =
            //use r = XmlReader.Create(reader, reader |> newSettingsFromReader o)
            reader |> xNodeReadFrom false o 
        static member ReadFromNextAsync (reader:XmlReader) =
            XNode.ReadFromNextAsync(reader, LoadOptions.None)
            
    module HelpersWrite =        
        let xElementLookupPrefix (ns:string) (w:XmlWriter) (e:XElement) : string =
            let prefix =
                if ns.Length > 0 
                then 
                    match e.GetPrefixOfNamespace (XNamespace.Get ns) with 
                    | null -> w.LookupPrefix (ns)
                    | _ as e -> e
                else String.Empty
            match e.Attributes ()
                |> Seq.filter (fun a -> a.IsNamespaceDeclaration && a.Value = ns)
                |> Seq.tryHead with
            | Some a ->
                if a.Name.Namespace = XNamespace.Xmlns then
                    a.Name.LocalName
                else prefix
            | None -> prefix
        let xCDataWriteTo (writer:XmlWriter) (e:XCData) = async {
                let start = ref 0
                let sb = ref (null:System.Text.StringBuilder)
                let value = e.Value
                for i in 0 .. value.Length - 3 do
                    if (value.[i] = ']' && value.[i + 1] = ']' && value.[i + 2] = '>') then
                        if !sb = null then
                            sb := new System.Text.StringBuilder ()
                        (!sb).Append (value, !start, i - !start) |> ignore
                        (!sb).Append ("]]&gt;") |> ignore
                        start := i + 3
                    
                if (!start <> 0 && !start <> value.Length) then
                    (!sb).Append (value, !start, value.Length - !start) |> ignore
                do! writer.WriteCDataAsync (if !sb = null then value else (!sb).ToString())|> Task.ofPlainTask
                return () }
        let xCommentWriteTo (writer:XmlWriter) (c:XComment) = async {
            do! writer.WriteCommentAsync (c.Value) |> Task.ofPlainTask }
        let xProcessingInstructionWriteTo (writer:XmlWriter) (p:XProcessingInstruction) = async {
            do! writer.WriteProcessingInstructionAsync (p.Target, p.Data) |> Task.ofPlainTask }
        let xTextWriteTo (writer:XmlWriter) (c:XText) = async { 
            let value = c.Value
            let task =
                if (value.Length > 0 && value |> Seq.forall (fun c -> c = ' ' || c = '\t' || c = '\r' || c = '\n')) then
                    writer.WriteWhitespaceAsync (value)
                else
                    writer.WriteStringAsync (value)
            do! task |> Task.ofPlainTask }
            
        let rec xNodeWriteTo (writer:XmlWriter) (node:XNode) =
            match node with
            | :? XElement as e -> xElementWriteTo writer e
            | :? XCData as c -> xCDataWriteTo writer c
            | :? XComment as c -> xCommentWriteTo writer c
            | :? XText as t -> xTextWriteTo writer t
            | :? XProcessingInstruction as p -> xProcessingInstructionWriteTo writer p            
            | _ -> failwithf "Unknown node type %O" (node.GetType())
        and xElementWriteTo (writer:XmlWriter) (e:XElement) = async {            
            let createDummyNamespace =
                let createdNS = ref 0
                (fun (atts:XAttribute seq) (isAttr:bool) ->
                    let checkConflict (name:string) =
                        atts 
                            |> Seq.forall (fun a -> a.Name.LocalName <> name || a.Name.NamespaceName = XNamespace.Xmlns.NamespaceName)
                    if (not isAttr && checkConflict "xmlns")
                    then String.Empty
                    else
                    let p = ref (null:string)
                    let doWhile() =
                        createdNS := (!createdNS + 1)
                        p := "p" + ((!createdNS).ToString())
                        // check conflict
                        not <| checkConflict !p
                    while doWhile() do ignore None
                    !p
                )
            let name = e.Name
            let prefix = 
                match e |> xElementLookupPrefix name.NamespaceName writer with
                | null ->  ""
                    //createDummyNamespace (e.Attributes()) false
                | _ as p -> p
                
            do! writer.WriteStartElementAsync (prefix, name.LocalName, name.Namespace.NamespaceName) |> Task.ofPlainTask

            for (a:XAttribute) in e.Attributes () do
                if a.IsNamespaceDeclaration then
                    if (a.Name.Namespace = XNamespace.Xmlns) then
                        do! writer.WriteAttributeStringAsync ("xmlns", a.Name.LocalName, XNamespace.Xmlns.NamespaceName, a.Value)
                            |> Task.ofPlainTask
                    else
                        do! writer.WriteAttributeStringAsync ("", "xmlns", null, a.Value)
                            |> Task.ofPlainTask
                else 
                    let apfix = 
                        match e |> xElementLookupPrefix (a.Name.NamespaceName) writer with
                        | null -> createDummyNamespace (e.Attributes ()) true
                        | _ as p -> p
                    do! writer.WriteAttributeStringAsync (apfix, a.Name.LocalName, a.Name.Namespace.NamespaceName, a.Value)
                        |> Task.ofPlainTask

            for node in e.Nodes () do
                do! node |> xNodeWriteTo writer
            
            if (e.Nodes() |> Seq.isEmpty) then
                do! writer.WriteEndElementAsync ()|> Task.ofPlainTask
            else
                do! writer.WriteFullEndElementAsync ()|> Task.ofPlainTask
            return() }
    open HelpersWrite
    type System.Xml.Linq.XNode with
        member node.WriteToAsync (writer:XmlWriter) =        
            xNodeWriteTo writer node
                
type Attribute = {
    Prefix : string
    Localname : string
    Namespace : string
    Value : string }
    
type ElemInfo = {
    Prefix : string
    Localname : string    
    Namespace : string
    Attributes : Map<string,Attribute> }
    
module internal Helpers =
    let xmlFail msg = raise <| XmlException msg
    let xmlFailInner inner (msg:string) = raise <| XmlException(msg, inner)
    let xmlFailf fmt = Printf.ksprintf xmlFail fmt
    let xmlFailInnerf inner fmt = Printf.ksprintf (xmlFailInner inner) fmt
    let guardAsync (f:unit->Async<'a>) = async {
        try
            return! f()
        with
        | :? System.Xml.XmlException as xml ->  
            return
                xmlFailInnerf xml "%s" xml.Message 
        | :? Mono.System.Xml.XmlException as xml->  
            return
                xmlFailInnerf xml "%s" xml.Message } |> Log.TraceMe
    let guard (f : unit -> 'a) =
        guardAsync (fun () -> async.Return (f())) |> Async.RunSynchronously

    let getAttributeValue (a:Attribute) = a.Value

    let readCurrentElementSimple (reader:XmlReader) = 
        { Prefix = reader.Prefix; Localname = reader.LocalName; Namespace = reader.NamespaceURI; Attributes = Map.empty }
    let readCurrentAttributeSimple (xmlDecl: bool) (reader:XmlReader) = 
        { Prefix = reader.Prefix; Localname = (if xmlDecl then reader.Name else reader.LocalName); Namespace = reader.NamespaceURI; Value = reader.Value }

    let readAttributesOfCurrentElement (xmlDecl: bool) (reader:XmlReader) =
        let attributes =
            Seq.init 
                reader.AttributeCount 
                (fun i -> 
                    reader.MoveToAttribute i
                    reader |> readCurrentAttributeSimple xmlDecl)
            |> Seq.toList
            |> Seq.map (fun a -> (if System.String.IsNullOrEmpty a.Namespace then a.Localname else a.Namespace + ":" + a.Localname), a)
            |> Map.ofSeq
        let moved = reader.MoveToElement() 
        //assert moved 
        attributes
       
    /// Read a XML Declaration from the given reader
    /// Throws System.Xml.XmlException the the declaration is invalid and Yaaf.Xmpp.Xml.XmlException when no declaration was found
    let readXmlDecl (reader:XmlReader) = async {
        let! found = reader.ReadAsync()
        if not found then return xmlFailf "could not read an XmlDecl from reader"
        return
            match reader.NodeType with
            | XmlNodeType.XmlDeclaration -> 
                let atts = reader |> readAttributesOfCurrentElement true
                (try
                    atts |> Map.find "version" |> getAttributeValue
                 with  
                 | :? System.Collections.Generic.KeyNotFoundException as k ->
                    xmlFailf "version attribute was not found in xmldoc element"),     
                (match atts |> Map.tryFind "encoding" with
                | Some s -> s |> getAttributeValue
                | None -> "utf8"),
                match atts |> Map.tryFind "standalone" with
                | Some s -> s |> getAttributeValue
                | None -> "no"
            | _ -> xmlFailf "XmlDecl expected!" } |> Log.TraceMe
        
    /// Read any opening element with all attributes (like <elem att="wer" att2="blub" >) ... without anything else.
    /// Ignores comments, XmlDeclaration or whitespace 
    /// Throws System.Xml.XmlException the the xml is invalid and Yaaf.Xmpp.Xml.XmlException when no element but something else was found
    let readOpenElement (reader:XmlReader) = async {
        let rec readElem () = async {
            let! found = reader.ReadAsync()
            if not found then return xmlFailf "could not read an element from reader"
            return!
                match reader.NodeType with
                | XmlNodeType.Element -> 
                    reader |> readCurrentElementSimple |> async.Return
                | XmlNodeType.SignificantWhitespace
                | XmlNodeType.Comment
                | XmlNodeType.CDATA
                | XmlNodeType.Whitespace ->
                    readElem() // ignore and try again
                | XmlNodeType.XmlDeclaration ->
                    readElem() // ignore xdeclaration
                | _ -> xmlFailf "element expected!" }
        let! elem = readElem()    
        return
            { elem with Attributes = reader |> readAttributesOfCurrentElement false } }
    
    /// Asynchronously reads a complete XElement or an end tag from the given reader, note the end tag has to match the start tag 
    /// (it is assumed that readOpenElement has been used before)
    /// Throws System.Xml.XmlException the the xml is invalid (which also is true when the end tag doesn't match the previous readOpenElement call) 
    /// and Yaaf.Xmpp.Xml.XmlException when no element or endelement was found but something else was found
    let readXElementOrClose (reader:XmlReader) =  
        async {
            let rec readElem () = async {
                let! found = reader.ReadAsync()
                if not found then return xmlFailf "could not read an element from reader"
                return!
                    match reader.NodeType with
                    | XmlNodeType.Element -> async {
                        let! elem = XElement.LoadAsync reader
                        return Some elem }
                    | XmlNodeType.XmlDeclaration
                    | XmlNodeType.SignificantWhitespace
                    | XmlNodeType.Comment
                    | XmlNodeType.CDATA
                    | XmlNodeType.Whitespace ->
                        readElem() // ignore and try again
                    | XmlNodeType.EndElement -> async {
                        //let endElem = reader |> readCurrentElementSimple
                        return None }
                    | _ -> xmlFailf "Element or EndElement expected (got %O)!" reader.NodeType }
            let! elem = readElem()   
            // No attributes on endelement! 
            return elem 
        } 
        |> Log.TraceMe

    let convertElemInfoToXElement (e : ElemInfo) =
        let elem = XElement(XName.Get(e.Localname, e.Namespace))
        let mutable hasPrefixNamespace = false
        if System.String.IsNullOrEmpty e.Namespace && System.String.IsNullOrEmpty e.Prefix then
            // No attribute required
            hasPrefixNamespace <- true
        for a in e.Attributes do
            let xname =
                if a.Value.Localname = "xmlns" && System.String.IsNullOrEmpty a.Value.Prefix then
                    if System.String.IsNullOrEmpty e.Prefix && e.Namespace = a.Value.Value then
                        // this better is the required namespace
                        hasPrefixNamespace <- true
                    XName.Get("xmlns", "")
                else XName.Get(a.Value.Localname, a.Value.Namespace)
            if (a.Value.Prefix = "xmlns") then
                if a.Value.Localname = e.Prefix && a.Value.Value = e.Namespace then
                    hasPrefixNamespace <- true
            elem.Add(new XAttribute(xname, a.Value.Value))
        if not hasPrefixNamespace then
            xmlFail "Prefix namespace was not found!"
        elem.ToString() |> ignore // does additional checks
        elem
    
    let writeOpenElem (elem:XElement) (writer:XmlWriter) =
        async {
            if elem.HasElements then invalidOp "XElement used for opening must not contain child elements!"
            let prefix = elem.GetPrefixOfNamespace(elem.Name.Namespace)
            do! writer.WriteStartElementAsync(prefix, elem.Name.LocalName, elem.Name.NamespaceName) |> Task.ofPlainTask
            for a in elem.Attributes() do
                do! writer.WriteAttributeStringAsync(elem.GetPrefixOfNamespace(a.Name.Namespace), a.Name.LocalName, a.Name.NamespaceName, a.Value)
                    |> Task.awaitPlain

            do! writer.WriteRawAsync " " |> Task.awaitPlain
        } |> Log.TraceMe
            
    let writeCloseElem (writer : XmlWriter) = 
        async { 
            //try
                do! writer.WriteEndElementAsync() |> Task.awaitPlain
                return ()
            //with
            //| :? InvalidOperationException as e ->
            //    return xmlFailInner e "There is no more open element to close!"
        } |> Log.TraceMe

open Helpers 
let getXName name ns = XName.Get(name, ns)
let getXElem n = XElement(n:XName)
let tryXAttr n (e:XElement) = match e.Attribute(n:XName) with null -> None | l -> Some l
let tryXAttrValue n (e:XElement) = tryXAttr n e |> Option.map (fun a -> a.Value)
let forceAttr n (e:XElement) = 
    match tryXAttr n e with None -> xmlFailf "Attribute %O was not found" n | Some l -> l
let forceAttrValue n (e:XElement) = forceAttr n e |> (fun a -> a.Value)

let getXElemWithChilds n (childs:'a seq) = XElement((n:XName), childs |> Seq.cast<obj> |> Seq.toArray)
let addChild child (e:'a when 'a :> XContainer) = e.Add(child:obj); e
let addChilds (childs:'a seq) (e:'a when 'a :> XContainer) = e.Add(childs |> Seq.cast<obj> |> Seq.toArray); e

let readXmlDecl r = guardAsync (fun () -> Helpers.readXmlDecl r) 
let readOpenElement r = guardAsync (fun () -> Helpers.readOpenElement r) 
let convertElemInfoToXElement e = guard (fun () -> convertElemInfoToXElement e)
let readOpenXElement r = async { let! d = guardAsync (fun () -> Helpers.readOpenElement r) 
                                 return convertElemInfoToXElement d } 
let writeOpenElement e w = guardAsync (fun () -> Helpers.writeOpenElem e w) 
let writeCloseElement w = guardAsync (fun () -> Helpers.writeCloseElem w) 

let readXElementOrClose r = guardAsync (fun () -> Helpers.readXElementOrClose r) 

let writeElem (node:XNode) writer = guardAsync (fun () -> node.WriteToAsync writer)

let getAttributeValue (a:Attribute) = guard (fun () -> Helpers.getAttributeValue a)

type XNodeNormalized = 
    | XElement of XElementNormalized
    | XText of string
    | RemoveMe
and XElementNormalized = {
    Attributes : XAttribute list
    Name : string * string
    Nodes : XNodeNormalized list }

let rec normalize (node:XNode) =
    match node with
    | :? XElement as elem -> XNodeNormalized.XElement <| normalizeElem elem
    | :? XText as elem -> 
        let value = elem.Value //.Trim()
        if String.IsNullOrWhiteSpace value then
            RemoveMe
        else XNodeNormalized.XText value
    | _ -> failwithf "unknown XNode type %O" (node.GetType())
and normalizeElem (elem:XElement) =
  { Attributes = 
        elem.Attributes() 
        |> Seq.sortBy (fun a -> a.Name.NamespaceName + a.Name.LocalName)
        |> Seq.filter (fun a -> not (a.IsNamespaceDeclaration && a.Name.LocalName = "xmlns"))
        |> Seq.toList
    Name = elem.Name.NamespaceName, elem.Name.LocalName
    Nodes = elem.Nodes() |> Seq.map normalize |> Seq.filter (fun n -> n <> RemoveMe) |> Seq.toList }
type EqualResult =
  { IsEqual : bool
    Message : string }
let equalXNodeAdvanced node1 node2 = 
    let Nnode1 = normalize node1
    let Nnode2 = normalize node2
    let equalAttribute (e:XAttribute) (a:XAttribute) =
        if  
            a.Name.LocalName = e.Name.LocalName &&
            a.Name.NamespaceName = e.Name.NamespaceName &&
            a.Value = e.Value then
            { IsEqual = true; Message = "" }
        else
            { IsEqual = false; Message = sprintf "XAttributes don't match %A <> %A" e a }
    let rec equalNode n1 n2 =
        if n1 = RemoveMe || n2 = RemoveMe then failwith "RemoveMe Elements should have been stripped"
        match n1 with            
        | XElement e1 ->
            match n2 with
            | XElement e2 -> equalElem e1 e2 : EqualResult
            | _ -> { IsEqual = false; Message = sprintf "XElement typed node (%A) doesn't match the given node %A" e1 n2 }
        | XText t1 ->
            match n2 with
            | XText t2 -> 
                if t1 = t2 then
                    { IsEqual = true; Message = "" }
                else
                    { IsEqual = false; Message = sprintf "XText nodes don't match %s <> %A" t1 t2 }
            | _ -> { IsEqual = false; Message = sprintf "XText typed node (%A) doesn't match the given node %A" t1 n2  }
        | RemoveMe -> failwith "RemoveMe Elements should have been stripped"
    and equalElem e1 e2 =
        if e2.Name <> e1.Name then { IsEqual = false; Message = sprintf "XElement name %A doesn't match %A" e1.Name e2.Name } : EqualResult
        else
        let addNodes s =                 
            Seq.append (s |> Seq.map Some) (Seq.initInfinite (fun i -> None))
        let myzip left right =
            Seq.zip (addNodes left) (addNodes right)
            |> Seq.takeWhile (fun (e,a) -> e.IsSome || a.IsSome)
        let equalAttributes =  
            myzip e1.Attributes e2.Attributes
                |> Seq.map 
                    (fun (expect, actual) -> 
                        match expect with
                        | Some e -> 
                            match actual with
                            | Some a -> 
                                equalAttribute e a
                            | None -> 
                                { IsEqual = false; Message = sprintf "attribute %A was not found" e }
                        | None -> 
                            { IsEqual = false; Message = sprintf "additional attribute %A was found" actual.Value })
                |> Seq.tryFind(fun e -> e.IsEqual = false)
        if equalAttributes.IsSome then equalAttributes.Value
        else
            let equalInnerNodes =
                myzip e1.Nodes e2.Nodes
                    |> Seq.map
                        (fun (expect, actual) ->                        
                            match expect with
                            | Some e -> 
                                match actual with
                                | Some a -> 
                                    equalNode e a
                                | None -> failwithf  "node %A was not found" e
                            | None -> failwithf  "additional node %A was found" actual)
                    |> Seq.tryFind(fun e -> e.IsEqual = false)
            if equalInnerNodes.IsSome then equalInnerNodes.Value
            else { IsEqual = true; Message = "" }
    equalNode Nnode1 Nnode2
let equalXNode n1 n2 = 
    (equalXNodeAdvanced n1 n2).IsEqual

open System.IO

open Yaaf.IO
let createFixedReader (stream:Stream) (settings:XmlReaderSettings) =
    //settings.CheckCharacters <- false
    new Yaaf.Xml.AsyncXmlReader.LazyMonoXmlTextReader(stream, settings) |> fromXmlReader
    //XmlReader.Create(stream, settings) |> fromXmlReader
(*
    let finish, (queue : IStream<byte array>) = limitedStream()
    let asInterface = toInterface 2024 stream
    let queueWriter = new StreamWriter(fromInterface queue)
    let disposed = ref false
    let rec readerTask currentTask =
     if !disposed then async.Return () else
      async {
        
        let readTask =
            match currentTask with
            | Some s -> s
            | None ->
                 Async.StartAsTask(asInterface.Read())
        do! Async.Sleep 1000
        if not !disposed then
            if readTask.IsCompleted then
                let read = readTask.Result
                match read with
                | Some d -> 
                    do! queue.Write d
                    return! readerTask None
                | None ->
                    do! finish()
                    queueWriter.Dispose()
                    return ()
            else
                // Fill queue with spaces to fill the buffer of xmlreader
                //Yaaf.Xmpp.Helper.Tracing.getTracer().logWarn(fun () -> "Timeout -> writing data into XmlReader stream")
                //queueWriter.WriteLine(String.replicate 4000 " ")
                return! readerTask (Some readTask)
      } |> Yaaf.Xmpp.Helper.Tracing.traceMe
    Async.Start(readerTask None)
    
    let readQueueT = readOnly queue
    let readQueue = fromReadWriteDispose (fun () ->  disposed := true; readQueueT.Dispose()) readQueueT.Read readQueueT.Write
    let readerStream = fromInterface readQueue
    XmlReader.Create(readerStream, settings) |> fromXmlReader  *)
(*
let createFixedReader (stream:Stream) (settings:XmlReaderSettings) = 
    // idea, create multiple xmlreader instances so that they can't buffer

    let mutable currentReader = null // Waiting for data
    let mutable currentData = ""
    { new XmlReader with 
        member x.Settings with get() = reader.Settings
        member x.BaseURI  with get() = reader.BaseURI
        member x.EOF with get() = reader.EOF
        member x.ReadAsync () = reader.ReadAsync() |> Task.await
        member x.NodeType with get() = reader.NodeType
        member x.MoveToContentAsync () = reader.MoveToContentAsync() |> Task.await
        member x.LocalName with get() = reader.LocalName
        member x.NamespaceURI with get() = reader.NamespaceURI
        member x.MoveToFirstAttribute () = reader.MoveToFirstAttribute()
        member x.MoveToNextAttribute () = reader.MoveToNextAttribute()
        member x.MoveToAttribute i = reader.MoveToAttribute i
        member x.MoveToElement () = reader.MoveToElement()
        member x.IsEmptyElement with get() = reader.IsEmptyElement
        member x.Value with get() = reader.Value
        member x.Name with get() = reader.Name
        member x.GetAttribute att = reader.GetAttribute att
        member x.Prefix with get() = reader.Prefix
        member x.AttributeCount with get() = reader.AttributeCount
    }
*)
    









    
