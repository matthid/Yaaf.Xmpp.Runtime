// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.TestHelper

open Yaaf.IO
open Yaaf.IO.Stream
open Yaaf.IO.StreamExtensions.Stream
open System.IO
open System
type StreamTestProvider () =

    let mutable finishStream1 = Unchecked.defaultof<_>
    let mutable finishStream2 = Unchecked.defaultof<_>
    let mutable s1 = Unchecked.defaultof<_>
    let mutable s2 = Unchecked.defaultof<_>
    let mutable stream1 = Unchecked.defaultof<_>
    let mutable stream2 = Unchecked.defaultof<_>
    let mutable c1 = Unchecked.defaultof<_>
    let mutable c2 = Unchecked.defaultof<_>
    let mutable cStream1 = Unchecked.defaultof<_>
    let mutable cStream2 = Unchecked.defaultof<_>
    let mutable writer1 = Unchecked.defaultof<_>
    let mutable writer2 = Unchecked.defaultof<_>
    let mutable reader1 = Unchecked.defaultof<_>
    let mutable reader2 = Unchecked.defaultof<_>
    member this.Setup() = 
        let _finishStream1, _stream1 = Stream.limitedStream()
        let _finishStream2, _stream2 = Stream.limitedStream()
        finishStream1 <- (fun () -> _finishStream1() |> Async.RunSynchronously)
        finishStream2 <- (fun () -> _finishStream2() |> Async.RunSynchronously)
        s1 <- _stream1
        s2 <- _stream2
        let crossS1, crossS2 = Stream.crossStream s1 s2
        c1 <- crossS1
        c2 <- crossS2
        stream1 <- Stream.fromInterfaceAdvanced finishStream1 s1
        stream2 <- Stream.fromInterfaceAdvanced finishStream2 s2
        cStream1 <- Stream.fromInterfaceAdvanced finishStream2 c1
        cStream2 <- Stream.fromInterfaceAdvanced finishStream1 c2
        //cStream1 <- Stream.fromInterfaceAdvanced finishStream1 c1
        //cStream2 <- Stream.fromInterfaceAdvanced finishStream2 c2
        writer1 <- new StreamWriter(stream1)
        writer1.AutoFlush <- true
        writer2 <- new StreamWriter(stream2)
        writer1.AutoFlush <- true
        reader1 <- new StreamReader(stream1)
        reader2 <- new StreamReader(stream2)
    
    member this.TearDown() = 
        finishStream1()
        finishStream2()
        //(* Don't care about disposing
        writer1.Dispose()
        writer2.Dispose()
        reader1.Dispose()
        reader2.Dispose()
        stream1.Dispose()
        stream2.Dispose()
        cStream1.Dispose()
        cStream2.Dispose()
        c1.Dispose()
        c2.Dispose()
        s1.Dispose()
        s2.Dispose()
        //*)
        s1 <- Unchecked.defaultof<_>
        s2 <- Unchecked.defaultof<_>
        stream1 <- Unchecked.defaultof<_>
        stream2 <- Unchecked.defaultof<_>
        c1 <- Unchecked.defaultof<_>
        c2 <- Unchecked.defaultof<_>
        cStream1 <- Unchecked.defaultof<_>
        cStream2 <- Unchecked.defaultof<_>
        writer1 <- Unchecked.defaultof<_>
        writer2 <- Unchecked.defaultof<_>
        reader1 <- Unchecked.defaultof<_>
        reader2 <- Unchecked.defaultof<_>
    
    member x.StreamI1 with get () = s1
    member x.StreamI2 with get () = s2
    member x.FinishStream1() = finishStream1()
    member x.FinishStream2() = finishStream2()
    member x.Stream1 with get () = stream1
    member x.Stream2 with get () = stream2
    member x.CrossStream1 with get () = cStream1
    member x.CrossStream2 with get () = cStream2
    member x.Reader1 with get () = reader1
    member x.Reader2 with get () = reader2
    member x.Writer1 with get () = writer1
    member x.Writer2 with get () = writer2
    
    member x.Write1(msg : string) = 
        writer1.Write msg
        writer1.Flush()
    
    member x.Write2(msg : string) = 
        writer2.Write msg
        writer2.Flush()