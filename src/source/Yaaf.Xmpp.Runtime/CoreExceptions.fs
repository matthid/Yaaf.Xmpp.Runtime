// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Runtime


/// This error indicates that the sending pipeline was already closed, so sending is not longer possible (IE the closing element </stream> was already sent!)
[<System.Serializable>]
type SendStreamClosedException =     
    inherit System.Exception
    new (msg : string) = { inherit System.Exception(msg) }
    new (msg:string, inner:System.Exception) = { inherit System.Exception(msg, inner) }
    new (info:System.Runtime.Serialization.SerializationInfo, context:System.Runtime.Serialization.StreamingContext) = {
        inherit System.Exception(info, context)
    }

/// This class represents any kind of finish from the other side
[<System.Serializable>]
type StreamFinishedException =     
    inherit System.Exception
    new (msg : string) = { inherit System.Exception(msg) }
    new (msg:string, inner:System.Exception) = { inherit System.Exception(msg, inner) }
    new (info:System.Runtime.Serialization.SerializationInfo, context:System.Runtime.Serialization.StreamingContext) = {
        inherit System.Exception(info, context)
    }
    
/// This class represents a normal stream close from the other side (can be used by plugins to trigger a immediate regular stream shutdown)
[<System.Serializable>]
type StreamNormalFinishedException =
    inherit StreamFinishedException
    new (msg : string) = { inherit StreamFinishedException(msg) }
    
/// This class represents a normal stream close from the other side, but on an unexpected situation
[<System.Serializable>]
type StreamUnexpectedFinishException =
    inherit StreamFinishedException
    new (msg : string) = { inherit StreamFinishedException(msg) }

/// This class represents errors where the other side already closed the stream with an error
[<System.Serializable>]
type StreamFailException =
    inherit StreamFinishedException
    
    val private error : StreamErrorException
    new (msg:string, inner:StreamErrorException) = {
        inherit StreamFinishedException(msg, inner) 
        error = inner
    }
    new (info:System.Runtime.Serialization.SerializationInfo, context:System.Runtime.Serialization.StreamingContext) = {
        inherit StreamFinishedException(info, context)
        error = Unchecked.defaultof<_>
    }
    member x.Error with get () = x.error

[<System.Serializable>]
type ConfigurationException = 
    inherit System.Exception
    new() = { inherit System.Exception() }
    new(msg : string) = { inherit System.Exception(msg) }
    new(msg : string, inner : System.Exception) = { inherit System.Exception(msg, inner) }
    new(info : System.Runtime.Serialization.SerializationInfo, context : System.Runtime.Serialization.StreamingContext) = 
        { inherit System.Exception(info, context) }

module Configuration = 
    let createFail msg = ConfigurationException msg
    let createFailInner inner (msg : string) = ConfigurationException(msg, inner)
    let createFailf fmt = Printf.ksprintf createFail fmt
    let createFailInnerf inner fmt = Printf.ksprintf (createFailInner inner) fmt
    let configFail msg = raise <| createFail msg
    let configFailInner inner (msg : string) = raise <| createFailInner inner msg
    let configFailf fmt = Printf.ksprintf configFail fmt
    let configFailInnerf inner fmt = Printf.ksprintf (configFailInner inner) fmt
