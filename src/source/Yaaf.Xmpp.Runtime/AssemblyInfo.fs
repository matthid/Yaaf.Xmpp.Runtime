// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Xmpp
open System.Reflection
open System.Runtime.CompilerServices
open System.Reflection

[<assembly: AssemblyTitle("Yaaf.Xmpp.Runtime")>]
[<assembly: AssemblyDescription("The heart of all xmpp logic.")>]
[<assembly: AssemblyConfiguration("")>]

[<assembly: InternalsVisibleTo("Test.Yaaf.Xmpp")>]
[<assembly: InternalsVisibleTo("Test.Yaaf.Xmpp.Runtime")>]
[<assembly: InternalsVisibleTo("Test.Yaaf.Xmpp.Integration")>]

()