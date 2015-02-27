// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Runtime

open Yaaf.Xmpp
open System.Threading.Tasks

type INegotiationService =
    abstract NegotiationCompleted : bool with get

    // Task to allow ContinueWith without any threading issues (event would have been called only once)
    abstract NegotiationTask : Task<unit>
    
    // Task to allow ContinueWith without any threading issues (event would have been called only once)
    abstract ConnectionTask : Task<JabberId> with get

    abstract RemoteJid : JabberId with get
    abstract LocalJid : JabberId with get

    // Helps the XmlStanzaplugin to correctly identify negotiation stanzas (like bind and feature) and ignore them
    abstract IsNegotiationElement : StreamElement -> bool

