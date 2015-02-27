// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp

// Strictly speaking this file is in the wrong folder, 
// but because of dependencies it makes sense to put it in the Runtime folder

open System.IO

open Yaaf.Xmpp.Runtime
open Yaaf.Xmpp.StreamHelpers

open Yaaf.Helper
open Yaaf.Logging
open Yaaf.Xml

module IOStreamHelpers =
    let writeOpenXElem e (w) = guardAsync (fun () -> writeOpenElement e w)

    let readOpenXElem (reader) = guardAsync (fun () -> readOpenXElement reader)

    
    let writeEndStream (writer : XmlWriter) = 
        guardAsync (fun () -> 
            async { 
                Log.Info(fun () -> L "writing end stream!")
                do! writeCloseElement writer
            })
    
    let writeStreamElement (elem : StreamElement) (writer : XmlWriter) = 
        async {
            Log.Info(fun () -> L "writing: \n%O" elem)
            do! writeElem elem writer
        }
    
    let readStreamElement (reader : XmlReader) : Async<StreamElement option> = 
        guardAsync (fun () -> 
            async { 
                let! read = readXElementOrClose reader
                match read with
                | Some elem -> 
                    Log.Info(fun () -> L "reading: \n%O" elem)
                    match elem.Name.NamespaceName with
                    | Equals KnownStreamNamespaces.streamNS -> 
                        match elem.Name.LocalName with
                        | "error" -> 
                            let streamError = StreamError.parseStreamError elem
                            // read end element?
                            raise <| StreamFailException("stream failed because the other side closed the stream with an error.", streamError)
                        | _ -> ()
                    | _ -> ()
                | None -> Log.Info(fun () -> "reading: STREAMEND")
                return read
            })

    let readStreamEnd (reader : XmlReader) : Async<unit> = 
        guardAsync (fun () -> 
            async { 
                let! read = readStreamElement reader
                match read with
                | Some _ -> StreamError.failf XmlStreamError.InternalServerError "expected stream end!"
                | None -> ()
            })
    
    let writeInWriter (writer : XmlWriter) f = 
        async { 
            do! f writer
            do! writer.FlushAsync() |> Task.ofPlainTask |> Task.await
        }
    let xmlEncoding = System.Text.UTF8Encoding(false)
    let writerSettings = 
        let s = new XmlWriterSettings(Async = true)
        // s.ConformanceLevel <- ConformanceLevel.Fragment
        s.Indent <- false
        // Prevent writing BOM, see http://xmpp.org/rfcs/rfc6120.html#xml-encoding (11.6)
        s.Encoding <- xmlEncoding
        s.NewLineChars <- ""
        s.NewLineHandling <- Mono.System.Xml.NewLineHandling.None
        s.NewLineOnAttributes <- false
        s.CloseOutput <- false
        s.WriteEndDocumentOnClose <- false
        s

    let defaultStreamBackend (stream : Stream) = 
        let writer = XmlWriter.Create(stream, writerSettings)
        let reader = createFixedReader stream (new XmlReaderSettings(Async = true, CloseInput = false))
        { new IXmlStream with
            member x.ReadStart () = readOpenXElem reader
            member x.WriteStart openData = writeInWriter writer (writeOpenXElem openData)
            member x.TryRead () = readStreamElement reader
            member x.Write elem = writeInWriter writer (writeStreamElement elem)
            member x.ReadEnd () = readStreamEnd reader
            member x.WriteEnd () = writeInWriter writer writeEndStream
            }

type IOStreamManager (s : Stream) =
    inherit StreamManager<Stream>(s, IOStreamHelpers.defaultStreamBackend)

    override x.CloseStreamOverride () =
        async {
            s.Close()
            s.Dispose()
            return ()
        }
    