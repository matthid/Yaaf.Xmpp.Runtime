// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.TestHelper

open Foq

[<AutoOpen>]
module FoqExtension =
    let inOrder l = 
        let s = obj()
        let vars = ref l
        fun () ->
            lock s (fun () ->
                match !vars with
                | [] -> failwith "empty order list"
                | s :: [] -> s
                | h :: tail ->
                    vars := tail
                    h)

    type ResultBuilder<'a, 'b when 'a : not struct>  with
        member x.ReturnsInOrder(l) =
            x.ReturnsFunc(inOrder l)
