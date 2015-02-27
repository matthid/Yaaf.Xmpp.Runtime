// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp

open Yaaf.FSharp.Control
open Yaaf.DependencyInjection

open Yaaf.Sasl
open Yaaf.Helper
open Yaaf.IO
open Yaaf.Logging

type Task<'a> = System.Threading.Tasks.Task<'a>

type ConnectInfo = 
    { LocalJid : JabberId
      Login : IClientMechanism list }

/// Simple interface representing an xmpp connection
type IXmppClient = 
    /// True when the runtime loop has finished with an exception (abnormal termination)
    abstract IsFaulted : bool with get
    /// True when the runtime loop has completed without exception
    abstract IsCompleted : bool with get
    /// True when the runtime has closed the sending loop (ie we are possibly still receiving)
    abstract IsClosed : bool with get
    /// True when the Negotation has completed successfully
    abstract NegotiationCompleted : bool with get
    /// Start to close the Connection (force flag indicates whether we should just force closing by closing the underlaying streams)
    abstract CloseConnection : force:bool -> System.Threading.Tasks.Task<unit>
    /// Start to close the Connection with the given stream exception (force flag indicates whether we should just force closing by closing the underlaying streams)
    abstract CloseConnection : force:bool * Runtime.StreamErrorException -> System.Threading.Tasks.Task<unit>
    /// Writes the given Element to the stream, should not be used! (used by unit tests)
    abstract WriteElem : StreamElement -> Async<unit>
    /// The type of the connection
    abstract StreamType : StreamType with get
    /// The JID of the other end
    abstract RemoteJid : JabberId with get
    /// The JID of the current side
    abstract LocalJid : JabberId with get
    /// A task object to "subscribe" to the NegotiationFinished event in a thread safe manner (ContinueWith)
    abstract NegotiationTask : Task<unit>
    /// A task object to "subscribe" to the NegotiationFinished (sic! This is basically the same) event in a thread safe manner (ContinueWith)
    abstract ConnectTask : Task<JabberId>
    /// A task object to "subscribe" to the Exited event in a thread safe manner (ContinueWith)
    abstract Exited : Task<exn option>

    /// The main method to interact with the instance, request the services you want to use (registered by plugins) and use then accordingly.
    abstract GetService<'a> : unit -> 'a