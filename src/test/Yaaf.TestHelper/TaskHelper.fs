// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.TestHelper

open FSharp.Control
open System.Threading.Tasks
open System
open FsUnit
open NUnit.Framework
open Yaaf.Helper
open Yaaf.Logging

[<AutoOpen>]
module TaskTestHelper =
    let waitTaskIT (taskId : string) (time : int) (t : System.Threading.Tasks.Task<_>) = 
        try 
            let safeTimeout = 
                if (System.Diagnostics.Debugger.IsAttached) then System.Threading.Timeout.Infinite
                else time
        
            let success = t.Wait(safeTimeout)
            if not success then 
                let span = System.TimeSpan.FromMilliseconds(float time)
                let msg = sprintf "Task (%s) did not finish within the given timeout (%A)!" taskId span
                Log.Err(fun () -> msg)
                Assert.Fail(msg)
        with :? AggregateException as agg -> 
            let agg = agg.Flatten()
            for i in 0..agg.InnerExceptions.Count - 1 do
                Log.Err(fun () -> L "Exception in Task: %O" (agg.InnerExceptions.Item i))
            reraisePreserveStackTrace <| if agg.InnerExceptions.Count = 1 then agg.InnerExceptions.Item 0
                                         else agg :> exn
        t.Result

    let mutable defaultTimeout = 
        if isMono then 1000 * 50 * 1 // 50 secs
        else 1000 * 10 // 10 secs

    let waitTaskT (time : int) (t : System.Threading.Tasks.Task<_>) = waitTaskIT "unknown" time t
    let waitTask (t : System.Threading.Tasks.Task<_>) = waitTaskT defaultTimeout t
    let waitTaskI (taskId : string) (t : System.Threading.Tasks.Task<_>) = waitTaskIT taskId defaultTimeout t
    let private warnMessages = new System.Collections.Generic.Dictionary<Object, String list>()
    let mutable warnedContextNull = false

    let getKey (context : TestContext) = 
        if context = null then "key is null" // :> obj
        else context.Test.FullName

    let warn msg = 
        let key = getKey TestContext.CurrentContext
        match warnMessages.TryGetValue(key) with
        | false, _ -> warnMessages.Add(key, [ msg ])
        | true, l -> warnMessages.[key] <- msg :: l
        if (TestContext.CurrentContext = null && not warnedContextNull) then 
            warnedContextNull <- true
            warnMessages.Add(getKey null, [ "TestContext.CurrentContext is null so we can not seperate warnings by test-case!" ])

    let getWarnings() = 
        match warnMessages.TryGetValue(getKey TestContext.CurrentContext) with
        | false, _ -> None
        | true, warnings -> 
            warnings
            |> List.fold (fun s warning -> sprintf "%s\n%s" warning s) ""
            |> Some

    let warnStop msg = 
        let warnings = 
            match getWarnings() with
            | None -> msg
            | Some other -> sprintf "%s\n%s" other msg
        Assert.Inconclusive(warnings)

    let warnTearDown() = 
        match getWarnings() with
        | None -> ()
        | Some allWarnings -> 
            if (TestContext.CurrentContext <> null && TestContext.CurrentContext.Result.Status = TestStatus.Failed) then Assert.Fail(allWarnings)
            else Assert.Inconclusive(allWarnings)