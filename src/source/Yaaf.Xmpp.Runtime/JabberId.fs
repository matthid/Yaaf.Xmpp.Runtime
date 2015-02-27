// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp

// Resolving: http://xmpp.org/rfcs/rfc6120.html 3.2!
// 
type JabberId = 
    { Localpart : string option
      Domainpart : string
      Resource : string option }
    
    member jid.BareId 
        with get () = 
            match jid.Localpart with
            | Some s -> sprintf "%s@%s" s jid.Domainpart
            | None -> jid.Domainpart
    
    member jid.FullId
        with get () = 
            let bareId = jid.BareId
            match jid.Resource with
            | Some loc -> sprintf "%s/%s" bareId loc
            | None -> bareId
    
    /// The Domain part as JabberId
    member x.Domain with get () = JabberId.Parse(x.Domainpart)
    
    /// The bare id as JabberId (without resourcepart)
    member x.BareJid with get () = JabberId.Parse(x.BareId)
    
    static member Parse(jidRaw : string) = 
        let parseSimple (s : string) = 
            if s.Contains("@") then 
                let splits = s.Split('@')
                { Localpart = Some <| splits.[0]
                  Domainpart = splits.[1]
                  Resource = None }
            else 
                { Localpart = None
                  Domainpart = s
                  Resource = None }
        if jidRaw.Contains("/") then 
            let splits = jidRaw.Split('/')
            { parseSimple splits.[0] with Resource = Some(splits.[1]) }
        else parseSimple jidRaw
    
    static member Empty = 
        { Localpart = None
          Domainpart = ""
          Resource = None }
    
    member child.IsSpecialOf(parent : JabberId) = 
        if parent.Resource.IsSome then child.FullId = parent.FullId
        else if parent.Localpart.IsSome then child.BareId = parent.BareId
        else child.Domainpart = parent.Domainpart
