// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
// NOTE: If warnings appear, you may need to retarget this project to .NET 4.0. Show the Solution
// Pad, right-click on the project node, choose 'Options --> Build --> General' and change the target
// framework to .NET 4.0 or .NET 4.5.
module Yaaf.Xmpp.Testproject.Main

open System
open System.IO
open System.Xml
open System.Xml.Linq
open Yaaf.FSharp.Control
#if Disabled
open Yaaf.Sasl
open Yaaf.Xml
open Yaaf.Helper
open Yaaf.Logging
open System
open System.Runtime.Remoting.Messaging

open Yaaf.Xmpp
open Yaaf.Xmpp.IM

type ssl = System.Security.Authentication.SslProtocols

let messagePump (xmppClient : IXmppClient) = 
    let imService = xmppClient.GetService<IImService>()
    let mutable exit = false
    while not exit do
        let command = Console.ReadLine()
        match command with
        | "exit" -> 
            xmppClient.CloseConnection(true).Wait()
            exit <- true
        | "requestRoster" -> 
            let r = imService.RequestRoster(None).Result
            match r with
            | ViaPush -> Log.Info(fun () -> L "Received ViaPush")
            | Roster(items, version) -> 
                Log.Info(fun () -> L "Received Roster (Version: %A):" version)
                items |> Seq.iter (fun item -> Log.Info (fun () -> L "\t Item: %A" item))
        | "setOnline" -> 
            imService.SendPresence(None, PresenceProcessingType.StatusInfo(PresenceStatus.SetStatus PresenceData.Empty))
        | "setOffline" -> 
            imService.SendPresence(None, PresenceProcessingType.StatusInfo(PresenceStatus.SetStatusUnavailable PresenceData.Empty.Status))
        | StartsWith "setRoster" -> 
            let splits = command.Split(' ')
            let msgCommand = command.Substring(splits.[0].Length + 1)
            let toJid = JabberId.Parse splits.[1]
            //let msg = command.Substring(splits.[0].Length + 1 + splits.[1].Length + 1)
            let item = RosterItem.CreateEmpty(toJid)
            imService.UpdateRoster(item).Wait()
        | StartsWith "subscribe" -> 
            let splits = command.Split(' ')
            let msgCommand = command.Substring(splits.[0].Length + 1)
            let toJid = JabberId.Parse splits.[1]
            //let msg = command.Substring(splits.[0].Length + 1 + splits.[1].Length + 1)
            imService.SendPresence(Some toJid, PresenceProcessingType.SubscriptionRequest)
        | StartsWith "approve" -> 
            let splits = command.Split(' ')
            let msgCommand = command.Substring(splits.[0].Length + 1)
            let toJid = JabberId.Parse splits.[1]
            //let msg = command.Substring(splits.[0].Length + 1 + splits.[1].Length + 1)
            imService.SendPresence(Some toJid, PresenceProcessingType.SubscriptionApproval)
        | StartsWith "message" -> 
            let splits = command.Split(' ')
            let msgCommand = command.Substring(splits.[0].Length + 1)
            let toJid = JabberId.Parse splits.[1]
            let msg = command.Substring(splits.[0].Length + 1 + splits.[1].Length + 1)
            imService.SendMessage(toJid, MessageData.CreateSimple msg)
        | _ -> printfn "unknown command %s" command
let runName = sprintf "RUN: %O" (System.Guid.NewGuid())
//System.IO.Directory.CreateDirectory("logs") |> ignore
let level_verb = System.Diagnostics.SourceLevels.Verbose
let level_all =  System.Diagnostics.SourceLevels.All
let level_warn = System.Diagnostics.SourceLevels.Warning
let level_info = System.Diagnostics.SourceLevels.Information
let prepare level (logger:System.Diagnostics.TraceListener) =
    logger.Filter <- new System.Diagnostics.EventTypeFilter(level)
    logger.TraceOutputOptions <- System.Diagnostics.TraceOptions.None
    logger // :> System.Diagnostics.TraceListener 

let listeners = 
    [| 
        yield Log.ConsoleLogger level_warn |> prepare level_all
        //yield new SimpleXmlWriterTraceListener("Yaaf.Xmpp.Run.svclog") |> prepare level_all
    |] : System.Diagnostics.TraceListener[] 

let cache = System.Diagnostics.TraceEventCache()
let name = CopyListenerHelper.cleanName runName
let runListeners =
    listeners
    |> Array.map (Yaaf.Logging.CopyListenerHelper.duplicateListener "Yaaf.Xmpp.Run" cache name)

let addLogging (sourceName:string) = 
    sourceName
    |> Log.SetupNamespace (fun source ->
        source.Switch.Level <- System.Diagnostics.SourceLevels.All
        if listeners.Length > 0 then
            source.Listeners.Clear()
            //if not <| source.Listeners.Contains(MyTestClass.Listeners.[0]) then
        source.Listeners.AddRange(runListeners)
        source)
let sources = [|
    addLogging "Yaaf.Xmpp"
    addLogging "Yaaf.Xmpp.Runtime"
    addLogging "Yaaf.Xmpp.Runtime.Features"
    addLogging "Yaaf.Xmpp"
    addLogging "Yaaf.Xmpp.MessageArchiveManager"
    addLogging "Yaaf.Xmpp.MessageArchiveManager.IMAP"
    addLogging "Yaaf.Xmpp.MessageArchiveManager.IMAP.GoogleChat"
    addLogging "Yaaf.Xmpp.Run"
    addLogging "Yaaf.Xmpp.IM"
    addLogging "Yaaf.Xmpp.IM.Sql"
    addLogging "Yaaf.Xmpp.IM.Sql.Model"
    addLogging "Yaaf.Xmpp.IM.Sql.MySql"
    addLogging "Yaaf.Xmpp.IM.MessageArchiving"
    addLogging "Yaaf.Xmpp.Features" 
    addLogging "Yaaf.Xmpp.Stream"
    addLogging "Yaaf.Xmpp.Server"
    addLogging "Yaaf.Xmpp.Configuration"
    addLogging "Yaaf.Sasl" 
    addLogging "Yaaf.Xml"
    addLogging "Yaaf.IO"
    addLogging "Yaaf"
    addLogging "Yaaf.Logging"
    addLogging "Yaaf.RefactorOut"
    addLogging "Yaaf.TestHelper"
    addLogging "Yaaf.RunHelper"
    addLogging "Yaaf.XmppTest" |]
        

[<EntryPoint>]
let main args = 
    printfn "starting program"
    use runActivity = Log.StartActivity (runName)
    let config = 
        Yaaf.Xmpp.XmppSetup.CreateSetup() 
        |> XmppSetup.AddMessagingClient
    let xmppClient =
        XmppClient.Connect({ ConnectInfo.LocalJid = JabberId.Parse "ldapuser@yaaf.de"
                             ConnectInfo.Login = [ new Plain.PlainClient("", "ldapuser", "T6y3369OruBc6NvvgMod") ] }, config)
        |> Async.RunSynchronously
    
    messagePump xmppClient
    try 
        xmppClient.Exited.Wait()
        runActivity.Dispose()
        sources |> Seq.iter (fun s -> s.Listeners |> Seq.cast |> Seq.iter (fun (l:System.Diagnostics.TraceListener) -> l.Dispose() |> ignore))
        0
    with exn -> 
        printfn "Client faulted: %O" exn
        -10
#endif