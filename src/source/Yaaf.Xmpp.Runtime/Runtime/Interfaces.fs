// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Runtime

open Yaaf.Xmpp

open System.Xml.Linq
open System.Threading.Tasks

/// This will only be used by the runtime. However to be extendible custom implementations are allowed.
/// Implementations SHOULD NOT throw their own exceptions on reading. 
/// Allowed are only:
///  - StreamFinishedException and subclasses when the other side finished the stream.
///  - StreamErrorException when there is an error in reading the stream (to close the stream properly). NOTE that you do NOT throw this exception when you read a stream error (use StreamFailException in this case).
/// ON write custom exceptions are allowed, but note that because of the queuing system we always shut down the runtime when the write fails!
type IXmlStream = 
    abstract TryRead : unit -> Async<StreamElement option>
    abstract Write : StreamElement -> Async<unit>
    abstract ReadStart : unit -> Async<OpenElement>
    abstract WriteStart : OpenElement -> Async<unit>
    abstract ReadEnd : unit -> Async<unit>
    abstract WriteEnd : unit -> Async<unit>

/// Used by the CoreApi to open the stream
type IInternalStreamOpener = 
    inherit IPluginServiceProvider
    abstract OpenStream : IXmlStream -> Async<unit>
    abstract CloseStream : IXmlStream -> Async<unit>
 
type IStreamManager =
    abstract XmlStream : IXmlStream with get
    abstract IsOpened : bool with get
    abstract IsClosed : bool with get
    abstract CloseStream : unit -> Async<unit>
    abstract OpenStream : unit -> Async<unit>

type IStreamManager<'prim> =
    inherit IStreamManager
    abstract PrimitiveStream : 'prim with get

/// Allows plugins to change the underlaying stream. So this interface abstracts away changing IXmlStream objects.
/// Plugins will require an ICoreStreamApi in there contructors if they need this API.
/// NOTE: BE CAREFUL WITH THIS API, THE METHODS OF THIS API CAN ONLY BE USED FROM WITHIN PLUGIN CALLS.
type ICoreStreamApi =
    inherit IPluginServiceProvider

    /// Provides an additional abstraction layered stream.
    /// When calling methods on this object they will be 
    /// redirected to the latest set IXmlStream object 
    /// (set by SetXmlStream method). So you can cache this.
    /// YOU SHOULD NOT USE THIS INSTANCE TO CLOSE THE STREAM, use the CloseStream and FailwithStream members instead 
    /// (those make sure that closing is done properly and only once)
    abstract AbstractStream : IXmlStream with get

    /// Allows plugins to get the data aquired while the stream was opened (by casting the instance to its type).
    /// Note that other StreamOpener instances may be used in the future. 
    /// (there are some weird XEPs with weird headers, thats why we support this)
    /// This also allows us to skip headers in unit tests.
    //abstract StreamOpener : IXmppStreamOpener with get

    /// Opens the current xmpp stream by sending headers (normally the 'stream' start elements)
    abstract OpenStream : unit -> Async<unit>
        
    /// Closes the xml stream, and ensures that following writes throw exceptions.
    /// This does not close the underlaying streams, 
    /// as it is possible that we still have to process receiving elements.
    /// The Runtime will close them as soon as everything is done.
    abstract CloseStream : unit -> Async<unit>
    abstract IsClosed : bool with get

    /// Set a new corestream, will save the history to close all streams properly in the end.
    /// Closing will be done in reversed order as opening (when IsOpened is true and IsClosed is false), 
    /// the AbstractStream property will be set accordingly
    abstract SetCoreStream : IStreamManager -> unit
    /// The CoreStream history (head is the last set item)
    abstract CoreStreamHistory : IStreamManager list with get
    
type IExclusiveSend =
    abstract DoWork : (unit -> unit Async) -> unit Async
    
/// Provides an api for plugins to do work on runtime exit
type IRuntimeShutdown =
    /// use ContinueWith to do somthing after the runtime task.
    abstract RuntimeTask : System.Threading.Tasks.Task<exn option>
    
/// Some Plugins will require a ILocalDelivery to send raw messages back to the other end.
/// The server/client code will also use this API to send messages over the wire.
type ILocalDelivery =
    /// Delivers the given items, ensures that they are sent in the given order (head first) and 
    /// no other messages between them. NOTE that you don't have this guarantee between two calls (for example with multiple threads involved)!
    abstract QueueMessages : StreamElement list -> unit
    
// Messaging pipeline:
//
// -> Preprocess 
// Then the messages are validated and modified. This is done in the order
// the plugins were registered.
// -> Process
// Processing means handling the message semantics. 
// (ie sending messages to other people, Saving message in an archive)
// This should not depend on other plugins. 
// Note that sending to other members triggers the same pipeline 
// (and uses the same API as sending to the current user).

// Examples:
// Message stanza arrives at user romeo (from romeo to juliet).
// In this case the CoreIM plugin and an message archiving plugin.
// In Preprocess the CoreIm plugin can set the correct headers, and MessageArchiving can add some attributes
// In Process the CoreIm Plugin will do the routing while MessageArchiving can archive the message


type PreprocessResult<'a> =
    { 
        Element : 'a
        IgnoreElement : bool
    }


type ProcessInfo<'a> =
    {
        Result : PreprocessResult<'a>
        OriginalElement : 'a
    }

type PipelineResult<'a> =
    {
        ResultElem : 'a
        IsHandled : bool
        IsIgnored : bool
        ProcessTask : Task<unit>
    }


/// Represents the current state of the handler for a given item
type HandlerState =
    /// Do not execute the Process step for the current element
    | Unhandled
    /// Handler which just want to hook into the pipeline (eg process the element but don't count this as 'Handle')
    | ExecuteUnhandled
    /// Handle means routing and fullfilling the semantics of the element, there can exist only one handling handler 
    /// (if there are multiple, an error will be logged and only the first executed)
    | ExecuteAndHandle
    /// Some Fallback handler (for example for returning errors), will be executed when there is no 'ExecuteAndHandle' handler.
    /// The Handler with the lowest number is executed, if there are multiple with the same number the first one is used and a warning will be logged
    | ExecuteIfUnhandled of int


type IPipeline<'a> =
    /// Name of the handler for error messages and debugging
    abstract HandlerName : string

    /// Modify will always be called to modify the current element
    abstract Modify : ProcessInfo<'a> -> PreprocessResult<'a>
    /// Checks the handlerstate for the given item
    abstract HandlerState : ProcessInfo<'a> -> HandlerState

    /// ProcessSync is called depending on the HandlerState returned in the previous step, it is executed synchronosly
    /// Use the Process method whenever possible!
    abstract ProcessSync : ProcessInfo<'a> -> Async<unit>
    /// Process is called depending on the HandlerState returned in the previous step
    abstract Process : ProcessInfo<'a> -> Task<unit>

/// The Configuration of an Plugin. Every implementation of this interface MUST be immutable!
type IXmppPluginConfig = interface end

type IReceivePipelineProvider<'a> =
    /// Notifies the Plugin on arrival of an xmpp element
    abstract ReceivePipeline : IPipeline<'a> with get
    
type ISendPipelineProvider<'a> =
    /// Notifies the Plugin on sending of an xmpp element
    abstract SendPipeline : IPipeline<'a> with get

type IPipelineProvider<'a> =
    inherit IReceivePipelineProvider<'a>
    inherit ISendPipelineProvider<'a>

type IPlugin =
    inherit IPluginServiceProvider
    abstract Name : string with get

type IXmppPlugin = 
    inherit IPlugin

type IXmlPipelinePlugin =
    inherit IPipelineProvider<StreamElement>
    abstract StreamOpened : unit -> Async<unit>

type IPluginManagerRegistrar =
    abstract RegisterFor<'T> : 'T -> unit
    // add member to provide my own IPluginManager implementation (for example a IServicePluginManager)
    //abstract RegisterManager<'T> : IPluginManager<'T> -> unit
    abstract CreateManagerFor<'T> : unit -> IPluginManager<'T>
    abstract GetManager<'T> : unit -> IPluginManager<'T>
    
// NOTE: Done in C# because of F# limitations (See Yaaf.XmppRuntime.Core)
// The following example is not working:
//type ITest<'u> = 
//    abstract TestG<'t when 't :> 'u> : 't -> 'u
///// Most of the Plugins will require a plugin manager (ie to use API provided by other plugins
//type IRuntimePluginManager =
//    /// Registers a plugin, 't must be the concrete plugin type 
//    abstract RegisterPlugin<'t when 't :> IXmppPlugin> : unit -> unit
//    abstract RegisterPlugin : IXmppPlugin -> unit
//    abstract GetPluginService<'t> : unit -> 't
//    abstract GetPlugins : unit -> IXmppPlugin seq

// To allow some extensions used internally
type IXmlPluginManager = inherit IPluginManager<IXmlPipelinePlugin>

type IXmppPluginManager = IServicePluginManager<IXmppPlugin>
    
type IRuntimeConfig =
    /// Serverside: The serving domain,
    /// Clientside: The client jabber id
    abstract JabberId : JabberId with get
    abstract RemoteJabberId : JabberId option with get
    abstract IsInitializing : bool  with get
    abstract StreamType : StreamType with get


