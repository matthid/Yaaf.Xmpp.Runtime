// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.TestHelper


[<AutoOpen>]
module HelperExtension =
    [<RequiresExplicitTypeArguments>] 
    let isType<'a> v =
        match box v with
        | :? 'a -> true
        | _ -> false