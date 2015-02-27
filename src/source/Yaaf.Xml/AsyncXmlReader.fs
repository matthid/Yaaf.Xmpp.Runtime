// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
module Yaaf.Xml.AsyncXmlReader

open Yaaf.IO
open System.Text
open Yaaf.Helper

let StrictUTF8 = new UTF8Encoding (false, true) :> Encoding
let Strict1234UTF32 = new UTF32Encoding (true, false, true):> Encoding
let StrictBigEndianUTF16 = new UnicodeEncoding (true, false, true):> Encoding
let StrictUTF16 = new UnicodeEncoding (false, false, true):> Encoding

let encodingException = new System.Xml.XmlException ("invalid encoding specification.")
let skipWhitespace (stream:IStream<byte>) = 
    async {
        let returnValue = ref None
        let finished = ref false
        let readCount = ref 0
        while not !finished do
            let! c = stream.Read()
            match c |> Option.map char with
            | Some '\r' | Some '\n' 
            | Some '\t' | Some ' ' ->
                ()
            | _ ->
                finished := true
                returnValue := c
        return !returnValue
    }

let readEncodingFromStream (stream:IPeekStream<byte>) = 
    async {
        let bytesToSkip = ref 0
        //let buffer = (Array.zeroCreate 6) : byte array
        let enc = ref <| StrictUTF8 // Default to UTF8 if we can't guess it
        //let! bufLength = stream.ReadAsync (buffer, 0, buffer.Length)
        //if (bufLength == -1 || bufLength == 0) then
        //    return enc
        
        let! c1 = stream.Read()
        let! c2 = stream.Read()
        if c1.IsNone || c2.IsNone then 
            return !enc
        else
        let c1,c2 = c1.Value,c2.Value
        bytesToSkip := !bytesToSkip + 1
        match (int c1, int c2) with
        | 0xFF, 0xFE ->
            // BOM-ed little endian utf-16
            enc := Encoding.Unicode
        | 0xFF, _ ->
            // It doesn't start from "<?xml" then its encoding is utf-8
            do! stream.ResetAll()
        | 0xFE, 0xFF ->
            // BOM-ed big endian utf-16
            enc := Encoding.BigEndianUnicode
        | 0xFE, _ ->
            // It doesn't start from "<?xml" then its encoding is utf-8
            do! stream.ResetAll()
        | 0xEF, 0xBB ->
            let! c = stream.Read()
            match c with 
            | Some d when int d = 0xBF -> // UTF8 BOM
                ()
            | Some d ->
                do! stream.ResetAll()
            | None ->
                do! stream.ResetAll()
                //do! stream.Write c1
                //do! stream.Write c2
        | 0xEF, _ ->
            do! stream.Write (byte 0xEF)
        // It could still be 1234/2143/3412 variants of UTF32, but only 1234 version is available on .NET.
        | 0, 0 ->
            enc := Strict1234UTF32
        | 0, _ ->
            enc := StrictBigEndianUTF16
        | f, 0 when char f = '<' ->
            let! c = stream.Read()
            match c with 
            | Some d when int d = 0 ->
                enc := Encoding.UTF32 // little endian UTF32
            | _ ->
                enc := Encoding.Unicode // little endian UTF16
        | f, g when char f = '<' && char g = '?' ->
            // write all back to stream
            let! xmlDeclPeek = stream.TryReadMore 3
            if (xmlDeclPeek.IsComplete && Encoding.ASCII.GetString xmlDeclPeek.Data = "xml") then
                // try to get encoding name from XMLDecl.
                let! newC = skipWhitespace stream
                let c = ref newC

                // version. It is optional here.
                match (!c) |> Option.map char with
                | Some 'v' ->
                    while ((!c).IsSome && (!c).Value >= 0uy) do
                        let! newC = stream.Read()
                        c := newC
                        if ((!c).IsSome && char (!c).Value = '0') then // 0 of 1.0
                            let! quote = stream.Read()
                            c :=  None // break;
                        
                    let! next = skipWhitespace stream
                    c := next
                | Some _ 
                | None ->
                    ()

                // encoding
                match !c |> Option.map char with
                | Some 'e' ->
                    let! ncodingPeekBytes = stream.TryReadMore 7
                    if (ncodingPeekBytes.IsComplete && Encoding.ASCII.GetString ncodingPeekBytes.Data = "ncoding") then
                        let! newC = skipWhitespace stream
                        c :=  newC
                        if ((!c).IsNone || char (!c).Value <> '=') then
                            raise <| encodingException
                        let! newC = skipWhitespace stream
                        c :=  newC

                        let quoteChar = c
                        let sb = new System.Text.StringBuilder ()
                        while ((!c).IsSome) do
                            let! newC = stream.Read()
                            c :=  newC
                            if (c = quoteChar) then
                                c :=  None
                            else if ((!c).IsNone) then
                                raise <| encodingException
                  
                            sb.Append (char (!c).Value) |> ignore
                        
                        let encodingName = sb.ToString ();
                        if (not <| Mono.System.Xml.XmlChar.IsValidIANAEncoding (encodingName)) then
                            raise <| encodingException
                        enc := Encoding.GetEncoding (encodingName)
                | Some _
                | None -> ()
                
            do! stream.ResetAll()
        | _ ->
            do! stream.ResetAll()
            if (int c1 = 0) then
                enc := StrictUTF16
        return !enc
    }

(*    public XmlTextReader (Stream xmlFragment, XmlNodeType fragType, XmlParserContext context)
			: this (context != null ? context.BaseURI : String.Empty,
				new XmlStreamReader (xmlFragment),
			fragType,
			context)*)
open Mono.System.Xml
open System.IO
open System.Threading.Tasks
type LazyMonoXmlTextReader(s : Stream, settings : Mono.System.Xml.XmlReaderSettings) = 
    inherit Mono.System.Xml.XmlReader()
    let monoReader = 
        // NOTE: use lazy instead of task because we don't want to have a running 'read' until we actually need it
        // (the mono xmlreader implementation tries to figure out the encoding in the constructor -> blocking, reading)
        //Task.runWork (fun () -> Mono.System.Xml.XmlReader.Create(s, settings))
        lazy Mono.System.Xml.XmlReader.Create(s, settings)
    let getReader () = monoReader.Force()
    let myAsyncHelper f =
        async { 
            //assert WorkerThread.IsWorkerThread
            //let context = System.Threading.SynchronizationContext.Current
            let reader = monoReader |> Lazy.force
            //do! Async.SwitchToContext context
            // Because there is currently no real async api, we should be in the worker thread now.
            do! WorkerThread.SwitchToLogicalWorker()
            let read = f reader
            return read }
    member x.MyRead () = myAsyncHelper (fun r -> r.Read())
    member x.MyMoveToContent () = myAsyncHelper (fun r -> r.MoveToContent())
    // Needed by us
    override x.Settings with get() = getReader().Settings
    override x.BaseURI  with get() = getReader().BaseURI
    override x.EOF with get() = getReader().EOF
    override x.ReadAsync () = x.MyRead() |> Async.StartAsTaskImmediate
    override x.NodeType with get() = getReader().NodeType
    override x.MoveToContentAsync () = x.MyMoveToContent() |> Async.StartAsTaskImmediate
    override x.LocalName with get() = getReader().LocalName
    override x.NamespaceURI with get() = getReader().NamespaceURI
    override x.MoveToFirstAttribute () = getReader().MoveToFirstAttribute()
    override x.MoveToNextAttribute () = getReader().MoveToNextAttribute()
    override x.MoveToAttribute(i:int) = getReader().MoveToAttribute i
    override x.MoveToElement () = getReader().MoveToElement()
    override x.IsEmptyElement with get() = getReader().IsEmptyElement
    override x.Value with get() = getReader().Value
    override x.Name with get() = getReader().Name
    override x.GetAttribute( att :int ) = getReader().GetAttribute att
    override x.Prefix with get() = getReader().Prefix
    override x.AttributeCount with get() = getReader().AttributeCount
    override x.Dispose(disposing) = if (disposing) then getReader().Dispose()


    // XmlReader
    override x.Depth with get() = getReader().Depth
    override x.MoveToAttribute(i:string) = getReader().MoveToAttribute(i)
    override x.MoveToAttribute(i:string, i2:string) = getReader().MoveToAttribute(i, i2)
    override x.GetAttribute( name : string, ns : string ) = getReader().GetAttribute(name, ns)
    override x.GetAttribute( att : string ) = getReader().GetAttribute att
    override x.ReadAttributeValue( ) = getReader().ReadAttributeValue()
    override x.Read( ) = 
        failwith "use async api"
        getReader().Read()
    override x.ReadState with get() = getReader().ReadState
    override x.NameTable with get() = getReader().NameTable
    override x.LookupNamespace( att :string ) = getReader().LookupNamespace att
    override x.ResolveEntity( ) = getReader().ResolveEntity()

/// Best case scenario would be to implement our own parser.. Because both standard implementations have drawbacks.
/// (.net because it sometimes buffers to much, mono because it blocks in the Create call (fixed above!) => unit tests).
type AsyncXmlTextReader (rawStream: IStream<byte array>, fragType:XmlNodeType, context: XmlParserContext) = 
    inherit System.Xml.XmlReader()
    let initializeTask = Stream.handlePeek rawStream readEncodingFromStream |> Async.StartAsTaskImmediate
    let encoding = 
        async {
            let! enc,_ = initializeTask |> Task.await
            return enc
        }
    let mutable stream = 
        async {
            let! encoding, s = initializeTask |> Task.await
            return new System.IO.StreamReader( s |> Stream.fromInterfaceSimple, encoding )
        } |> Async.StartAsTaskImmediate

    // Needed by us
    override x.Settings with get() = Unchecked.defaultof<_>
    override x.BaseURI  with get() = Unchecked.defaultof<_>
    override x.EOF with get() = Unchecked.defaultof<_>
    override x.ReadAsync () = Unchecked.defaultof<_>
    override x.NodeType with get() = Unchecked.defaultof<_>
    override x.MoveToContentAsync () = Unchecked.defaultof<_>
    override x.LocalName with get() = Unchecked.defaultof<_>
    override x.NamespaceURI with get() = Unchecked.defaultof<_>
    override x.MoveToFirstAttribute () = Unchecked.defaultof<_>
    override x.MoveToNextAttribute () = Unchecked.defaultof<_>
    override x.MoveToAttribute(i:int) = Unchecked.defaultof<unit>
    override x.MoveToElement () = Unchecked.defaultof<_>
    override x.IsEmptyElement with get() = Unchecked.defaultof<_>
    override x.Value with get() = Unchecked.defaultof<_>
    override x.Name with get() = Unchecked.defaultof<_>
    override x.GetAttribute( att :int ) = Unchecked.defaultof<string>
    override x.Prefix with get() = Unchecked.defaultof<_>
    override x.AttributeCount with get() = Unchecked.defaultof<_>
    override x.Dispose(disposing) = Unchecked.defaultof<_>


    // XmlReader
    override x.Depth with get() = Unchecked.defaultof<_>
    override x.MoveToAttribute(i:string) = Unchecked.defaultof<bool>
    override x.MoveToAttribute(i:string, i2:string) = Unchecked.defaultof<_>
    override x.GetAttribute( name : string, ns : string ) = Unchecked.defaultof<string>
    override x.GetAttribute( att : string ) = Unchecked.defaultof<string>
    override x.ReadAttributeValue( ) = Unchecked.defaultof<bool>
    override x.Read( ) = Unchecked.defaultof<bool>
    override x.ReadState with get() = Unchecked.defaultof<_>
    override x.NameTable with get() = Unchecked.defaultof<_>
    override x.LookupNamespace( att :string ) = Unchecked.defaultof<string>
    override x.ResolveEntity( ) = Unchecked.defaultof<_>