// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp.Server

open Yaaf.DependencyInjection
open Yaaf.Xmpp


type IUserServiceManager =
    abstract JabberId : JabberId
    abstract GetService<'a> : unit -> 'a
type UserServiceManager (kernel : IKernel, jid) =

    member x.Kernel = kernel
    interface IUserServiceManager with
        member x.JabberId = jid
        member x.GetService<'a> () = kernel.Get<'a>()

type IPerUserService = 
    inherit IServiceManager
    abstract ForUser : JabberId -> IUserServiceManager

/// Plugin which is only available on the server side, and registers some server side only actions
type XmppServerUserServicePlugin(serverApi : IServerApi, kernel:IKernel) = 
    inherit ServiceManager() 

    let userServiceManagers = new System.Collections.Concurrent.ConcurrentDictionary<JabberId, UserServiceManager>()
    
    let registeredServices = new System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Type>()

    let registerService (mgr:UserServiceManager) (tService:System.Type, tInstance:System.Type) =
        let instance = mgr.Kernel.Get(tInstance)
        mgr.Kernel.Bind(tService).ToConstant(instance) |> ignore

    let createMgr =
        new System.Func<_, _>(
            fun jid -> 
                let newManager = UserServiceManager(kernel.CreateChild(), jid) 
                for t in registeredServices do
                    registerService newManager (t.Key, t.Value)
                newManager )
    
    override x.RegisterService (s,t) =
        let wasAdded = ref false
        let regType = registeredServices.GetOrAdd(s, (fun _ -> wasAdded := true; t))
        if regType <> t then
            invalidOp "Can not call RegisterService with different instance types for the same service type! (We don't ignore the second call because this could be a silent error!)"
        else
            if !wasAdded then
                for mgr in userServiceManagers do
                    registerService mgr.Value (s,t)

    interface IPerUserService with
        member x.ForUser jid =
            userServiceManagers.GetOrAdd (jid.BareJid, createMgr) :> IUserServiceManager

    
    interface IServerPlugin with
        member x.PluginService = Runtime.Service.FromInstance<IPerUserService, _> x
        member x.Name = "XmppServerUserServicePlugin"