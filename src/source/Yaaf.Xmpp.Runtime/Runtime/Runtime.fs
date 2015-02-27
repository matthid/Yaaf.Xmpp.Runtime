// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Runtime

open Yaaf.DependencyInjection
//open Yaaf.DependencyInjection.Ninject
open Yaaf.Helper
open Yaaf.Logging
open Yaaf.Xmpp
open Yaaf.FSharp.Control

type internal SendQueueAction =
    | EnqueueMessage of StreamElement list
    | BlockSend of (unit -> unit Async) * AsyncReplyChannel<unit>

type SendQueueBox (sendAction) =
    let errorTask = System.Threading.Tasks.TaskCompletionSource<exn>()
    let mailbox =
        MailboxProcessor.Start(
            fun inbox -> 
                async {
                    while true do
                        let! msg = inbox.Receive()
                        try
                            match msg with
                            | EnqueueMessage msgs ->
                                if msgs.IsEmpty then
                                    do! sendAction None
                                else
                                    for m in msgs do
                                        do! sendAction (Some m)
                            | BlockSend (work,reply) ->
                                try
                                    do! work ()
                                finally
                                    reply.Reply ()
                        with exn ->
                            errorTask.TrySetResult exn |> ignore
                            reraisePreserveStackTrace exn
                        return ()
                })
    do
        mailbox.Error |> Event.add (fun exn -> errorTask.TrySetResult exn |> ignore)
    let reThrowHelper (exn:exn) =
        match exn with
        | :? SendStreamClosedException as e -> raise e
        | _ as e -> 
            raise <| System.InvalidOperationException("the send mailbox crashed", errorTask.Task.Result)
    member x.Error = errorTask.Task
    member x.SendMessages msgs =
        if not errorTask.Task.IsCompleted then
            mailbox.Post(EnqueueMessage msgs)
        else
            reThrowHelper errorTask.Task.Result

    member x.BlockSend work =
        if not errorTask.Task.IsCompleted then
            mailbox.PostAndAsyncReply(fun reply -> BlockSend(work, reply))
        else
            reThrowHelper errorTask.Task.Result

type PluginManagerRegistrar(kernel : IKernel) = 
    interface IPluginManagerRegistrar with
        member x.RegisterFor<'T> instance =
            kernel.Get<IPluginManager<'T>>().RegisterPlugin(instance)
        member x.CreateManagerFor<'T> () =
            let pluginManager = new PluginManager<'T>()
            kernel.Bind<IPluginManager<'T>>().ToConstant(pluginManager) |> ignore
            pluginManager :> IPluginManager<'T>
        member x.GetManager<'T> () =
            kernel.Get<IPluginManager<'T>>()
        
type SingleChecker() =
    let o = obj()
    let mutable isStarted = false
    member x.IsStarted = isStarted
    member x.StartSingle(exn) =
        if isStarted then
            raise exn
        else
            lock o (fun () ->
                if isStarted then
                    raise exn
                else
                    isStarted <- true)




/// Represents the part of the runtime which doesn't depend on the 'prim type
type XmppRuntime(coreApi : ICoreStreamApi, config : IRuntimeConfig, kernel : IKernel) as x =
    static do
        WorkerThread.CallContext <- 
            { new ICallContext with
                member x.LogicalGetData key =
                    System.Runtime.Remoting.Messaging.CallContext.LogicalGetData key
                member x.LogicalSetData (key, value) =
                    System.Runtime.Remoting.Messaging.CallContext.LogicalSetData(key, value) 
            }
    
    let childKernel = kernel.CreateChild()
    let xmlPipelineManager = 
        //childKernel.Get<IRuntimePluginManager>() 
        new XmlPipelineManager() :> IXmlPluginManager
    let registrar = new PluginManagerRegistrar(childKernel)
    let startChecker = SingleChecker()
    static let createError =
        new System.Func<string,exn,exn>(
            fun msg (exn:exn) ->
                //match exn with
                //| null -> failwith msg
                //| _ -> raise exn)
                match exn with
                | null -> Configuration.createFail msg :> exn
                | _ -> Configuration.createFailInner exn msg :> exn)
    let pluginManager =
        new ServicePluginManager<IXmppPlugin>(childKernel, createError) :> IServicePluginManager<IXmppPlugin>
    let runtimeShutdown = new System.Threading.Tasks.TaskCompletionSource<_>()
    let runtimeShutdownProvider =
        { new IRuntimeShutdown with
            member x.RuntimeTask = runtimeShutdown.Task }
    do
        if not coreApi.CoreStreamHistory.IsEmpty then
            invalidArg "coreApi" "the coreApi has to be unused, to start a connection"
        //childKernel.Bind<IKernel>().ToConstant(childKernel) |> ignore
        ServicePluginManager.RegisterServices (childKernel, coreApi.PluginService, createError)
        childKernel.Bind<IServicePluginManager<IXmppPlugin>>().ToConstant(pluginManager) |> ignore
        childKernel.Bind<IXmlPluginManager>().ToConstant(xmlPipelineManager) |> ignore
        childKernel.Bind<IPluginManager<IXmlPipelinePlugin>>().ToConstant(xmlPipelineManager) |> ignore
        childKernel.Bind<ICoreStreamApi>().ToConstant(coreApi) |> ignore
        childKernel.Bind<ILocalDelivery>().ToConstant(x) |> ignore
        childKernel.Bind<IRuntimeShutdown>().ToConstant(runtimeShutdownProvider) |> ignore
        childKernel.Bind<IExclusiveSend>().ToConstant(x) |> ignore
        childKernel.Bind<IPluginManagerRegistrar>().ToConstant(registrar) |> ignore
        childKernel.Bind<IRuntimeConfig>().ToConstant(config) |> ignore
    

    let sendBoxFinished = AsyncManualResetEvent()
    let sendBox = 
        SendQueueBox(fun elem ->
            async {
                match elem with
                | Some item ->
                    let sendPipeline = xmlPipelineManager.GetPlugins() |> Seq.map (fun p -> p.SendPipeline)
                    let! result = Pipeline.processPipeline "Send XML Pipeline" sendPipeline item
                    // really wait?
                    do! result.ProcessTask
                    do! coreApi.AbstractStream.Write(result.ResultElem)
                | None -> 
                    do! coreApi.CloseStream()
                    sendBoxFinished.Set()
            } |> Log.TraceMe)
    let worker = new WorkerThread()
    let isLoopFinished, finishLoop = 
        let source = new System.Threading.Tasks.TaskCompletionSource<_>()
        (fun () -> source.Task.IsCompleted),
        (fun () -> source.TrySetResult () |> ignore)
    let handlerError = System.Threading.Tasks.TaskCompletionSource<_>()
    let handlerLoop () =
        async {
            // Because currently we have no real async backend we try to stay on the same worker thread
            while not <| isLoopFinished() do
                
                let nextRead = 
                    Async.StartAsTaskImmediate(
                        async { 
                            worker.SetWorker()
                            let! res = coreApi.AbstractStream.TryRead()
                            return res
                        } |> Log.TraceMe)
                //let nextError = errorList |> Async.StartAsTask
                do! Async.SwitchToThreadPool() // or we will deadlock because we are possibly within worker
                let! nextItem = Task.whenAny ([nextRead; handlerError.Task ]) |> Task.await
                worker.SetWorker()
                //let! nextItem = coreApi.AbstractStream.TryRead()
                match nextItem with
                | Some item ->
                    try
                        let! result = Pipeline.processManager "Receive XML Pipeline" xmlPipelineManager item
                        // really wait?
                        //do! result.ProcessTask
                        result.ProcessTask.ContinueWith(fun (t:System.Threading.Tasks.Task<_>) -> 
                            if t.IsFaulted then handlerError.TrySetException (Task.flatAggregate t.Exception) |> ignore)
                            |> ignore
                    with :? SendStreamClosedException -> 
                        Log.Warn (fun () -> "Received element could not be processed because a plugin tried to send on a closed stream")
                | None ->
                    finishLoop()
                    // close when all messages are sent
                    try
                        if not coreApi.IsClosed then
                            sendBox.SendMessages []
                            do! sendBoxFinished.AsAsync
                    with :? SendStreamClosedException -> 
                        Log.Info (fun () -> "sendBox.SendMessages [] failed because stream was already closed.")
                        
            return ()
        } |> Log.TraceMe
    let connectionHandler (prim:IStreamManager) =
         async {
            do! worker.SwitchToWorker()
            if not coreApi.CoreStreamHistory.IsEmpty then
                invalidOp "the coreApi has to be unused, to start a connection"
            try
                coreApi.SetCoreStream(prim)
                do! coreApi.OpenStream()
                do! xmlPipelineManager.StreamOpened()
                do! handlerLoop ()
            with
            | :? StreamErrorException as error -> 
                // we have to close the stream
                do! coreApi.FailwithStream error
                reraisePreserveStackTrace (error :> exn)
            | :? StreamNormalFinishedException as f -> 
                do! coreApi.CloseStream()
            | :? StreamFinishedException as f -> 
                // The other side closed the stream already, so close it
                do! coreApi.CloseStream()
                reraisePreserveStackTrace (f:> exn)
            | exn -> 
                // tell the other side we failed :(
                // we log this exception in case the Failwith call fails again...
                Log.Err(fun _ -> L "Unknown error in XmppCore (on handler loop): %O" exn)
                try
                    do! coreApi.FailwithStream 
                            (StreamErrorException(XmlStreamError.InternalServerError, Some "unknown error", []))
                with :? SendStreamClosedException -> ()
                reraisePreserveStackTrace exn
         } |> Log.TraceMeAs "XmppRuntime-Loop"
    let handleConnection (prim:IStreamManager) =
        if prim.IsClosed then
            invalidArg "prim" "can't start a connection with a closed stream"
        startChecker.StartSingle (System.InvalidOperationException ("Runtime can only connect once!"))
        let conTask = Async.StartAsTaskImmediate(connectionHandler prim)
        conTask.ContinueWith (fun (t:System.Threading.Tasks.Task<unit>) -> 
            (worker :> System.IDisposable).Dispose()
            let err = 
                if t.IsFaulted then 
                    let flatten = Task.flatAggregate t.Exception
                    Log.Err(fun _ -> L "connectionHandler Task faulted: %O" flatten)
                    Some flatten
                else None
            runtimeShutdown.SetResult (err)
            err)

    let closeConnection force err =
        async {
            Log.Warn(fun _ -> L "Starting to close a connection (force: %A) with error: %A" force err)
            if force then
                finishLoop()
            
            //try
            Log.Info(fun _ -> L "Loop finished trying to write exit")
            match err with
            | Some e ->
                try
                    do! coreApi.FailwithStream e
                with :? SendStreamClosedException -> ()
            | None ->
                do! coreApi.CloseStream()
            sendBoxFinished.Set()

                //if coreApi.IsClosed && not  then
                //    sendBox.SendMessages []
                //    do! sendBoxFinished.AsAsync
            //with :? SendStreamClosedException -> 
            //    Log.Info (fun () -> "sendBox.SendMessages [] failed because stream was already closed.")
            
            if force then
                Log.Info(fun _ -> L "Shutting down streams (because of force)")
                // Shut everything down? (every nice way was done above)
                // close underlaying stream
                for stream in coreApi.CoreStreamHistory do
                    do! stream.CloseStream()
            
            Log.Info(fun _ -> L "XmppRuntime closing task finished with success!")
            return ()
        } |> Log.TraceMe

    do
        sendBox.Error.ContinueWith(fun (t:System.Threading.Tasks.Task<exn>) -> 
            Log.Warn(fun _ -> L "Sendmailbox closed with an error: %O" t.Result)
            try
                sendBoxFinished.Set()
                match t.Result with
                | :? SendStreamClosedException -> ()
                | exn -> 
                    // We basically have to trigger a complete shutdown, 
                    // because it is possible that we now wait for new data while we could not send the request!
                    Log.Err(fun _ -> L "Unknown error in XmppCore (on sending): %O" exn)
                    async {
                        // Prevent writing some follow up exceptions by closing the stream immediatly
                        do! closeConnection true (Some (StreamError.createinternal "server send error"))
                    } |> Async.RunSynchronously
            with exn ->
                Log.Err(fun () -> L "Error in sendbox.Error continuation: %A" exn)) |> ignore

    static member internal CreateErrorHelper = createError
    member x.Connect (prim:IStreamManager) = handleConnection prim
    /// The letReceive flag means that we allow that we still let the receive loop run until we receive a stream closing element (regular shutdown)
    member x.CloseConnection (force:bool) = closeConnection force None
    member x.CloseConnection (force:bool,error:StreamErrorException) = 
        closeConnection force (Some error)

    member x.IsClosed = coreApi.IsClosed

    member x.PluginManager = pluginManager 

    member x.QueueMessages (msgs : StreamElement list) = sendBox.SendMessages msgs
    member x.QueueMessage (msg : StreamElement) = x.QueueMessages [msg]
    
    interface ILocalDelivery with
        member x.QueueMessages msgs = x.QueueMessages msgs


    interface IExclusiveSend with
        member x.DoWork work = sendBox.BlockSend work