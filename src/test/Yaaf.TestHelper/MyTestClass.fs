// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.TestHelper

open System
open System.Collections.Generic
open NUnit.Framework
open System.Diagnostics
open Yaaf.Helper
open Yaaf.Logging

[<TestFixture>]
type MyTestClass() = 
    static do System.IO.Directory.CreateDirectory("logs") |> ignore
    static let level_verb = SourceLevels.Verbose
    static let level_all = SourceLevels.All
    static let level_warn = SourceLevels.Warning
    static let level_info = SourceLevels.Information
    
    static let prepare level (logger : System.Diagnostics.TraceListener) = 
        logger.Filter <- new System.Diagnostics.EventTypeFilter(level)
        logger.TraceOutputOptions <- System.Diagnostics.TraceOptions.None
        logger // :> System.Diagnostics.TraceListener 
    
    static let xmlWriter = new SimpleXmlWriterTraceListener("logs/tests.svclog") |> prepare level_all
    
    //new Yaaf.Logging.XmlWriterTraceListener("logs/tests.svclog") |> prepare level_all
    static let listeners : System.Diagnostics.TraceListener [] = 
        [| if not isMono then yield Log.ConsoleLogger level_all |> prepare level_all
           yield xmlWriter |]
    
    let mutable sources : System.Diagnostics.TraceSource [] = null
    let mutable testActivity = Unchecked.defaultof<ITracer>
    
    static let shortenTestName (testName:string) (fullName:string) =
        // Because tests have a name like Test.Namespace.Test-Namespace-UnitUnderTests, we want to shorten that
        // Another reason is because we trigger the 260 character limit on windows...
        assert (fullName.EndsWith ("." + testName))
        let firstPart = fullName.Substring(0, fullName.Length - (testName.Length + 1))
        let namespaces = firstPart.Split([|'.'; '+'|])
        assert (namespaces.Length > 1)
        let className = namespaces.[namespaces.Length - 1]
        let prefixName = System.String.Join("-", namespaces |> Seq.take (namespaces.Length - 1))
        let newClassName =
            if className.StartsWith(prefixName) then
                // we can sorten the namespace away and effectively only take the className
                let indexOfColon = className.IndexOf(':')
                if indexOfColon > prefixName.Length - 1 then
                    // Shorten even more by leaving out everything after ':'
                    className.Substring(0, indexOfColon)
                else 
                    className
            else
                // Doesn't have the new Test format (this will be marked obsolete and we will throw here to enforce the new scheme!)
                System.String.Join("-", namespaces)
        
        let newTestName =
            let colonIndex = testName.IndexOf(":")
            let periodIndex = testName.IndexOf(".")
            if colonIndex > -1 then
                // Check if we have 'Test-Yaaf-Class: desc. test dec' format
                let first = testName.Substring(0, colonIndex)
                if not <| first.Contains(" ") && colonIndex < periodIndex then
                    // we assume this format, so we take everything after the period
                    testName.Substring(periodIndex + 1)
                else
                    testName
            else
                testName

        let shortName = sprintf "%s: %s" newClassName newTestName
        assert (shortName.StartsWith("Test-Yaaf-") || shortName.StartsWith("Yaaf-"))
        if shortName.StartsWith("Test-Yaaf-") then
            shortName.Substring("Test-Yaaf".Length + 1)
        else
            shortName.Substring("Yaaf".Length + 1)
            


    static member ShortenTestName testName fullName = shortenTestName testName fullName
    [<TearDownAttribute>] abstract TearDown : unit -> unit
    
    [<TearDownAttribute>]
    override x.TearDown() = 
        warnTearDown()
        testActivity.Dispose()
        testActivity <- Unchecked.defaultof<_>
        sources |> Seq.iter (fun s -> 
                       s.Listeners
                       |> Seq.cast
                       |> Seq.iter (fun (l : System.Diagnostics.TraceListener) -> l.Dispose() |> ignore))
    
    //MyTestClass.Listeners |> 
    [<SetUpAttribute>] abstract Setup : unit -> unit
    
    [<SetUpAttribute>]
    override x.Setup() = 
        // Setup logging
        let test = TestContext.CurrentContext.Test
        let cache = System.Diagnostics.TraceEventCache()
        let shortName = shortenTestName test.Name test.FullName
        let name = CopyListenerHelper.cleanName shortName
        
        let testListeners = 
            listeners 
            |> Array.map (Yaaf.Logging.CopyListenerHelper.duplicateListener "Yaaf.TestHelper" cache name)
        
        let addLogging (sourceName : String) = 
            sourceName
            |> Log.SetupNamespace (fun source ->
                source.Switch.Level <- System.Diagnostics.SourceLevels.All
                if listeners.Length > 0 then 
                    source.Listeners.Clear()
                    //if not <| source.Listeners.Contains(MyTestClass.Listeners.[0]) then
                source.Listeners.AddRange(testListeners)
                source)
        
        let defSource = addLogging "Yaaf.Logging"
        Log.SetUnhandledSource defSource
        
        sources <- [| addLogging "Yaaf.Xmpp"
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
                      defSource
                      addLogging "Yaaf.RefactorOut"
                      addLogging "Yaaf.TestHelper"
                      addLogging "Yaaf.RunHelper"
                      addLogging "Yaaf.XmppTest" 
                      addLogging "Yaaf.Xmpp.IMTest"|]
        testActivity <- Log.StartActivity(shortName)
        ()
    
    [<Test>]
    member x.``Check TestClass Setup and TearDown``() = ()


    