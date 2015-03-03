Yaaf.Xmpp.Runtime
===================
## [Documentation](https://matthid.github.io/Yaaf.Xmpp.Runtime/)

## Build status

**Development Branch**

[![Build Status](https://travis-ci.org/matthid/Yaaf.Xmpp.Runtime.svg?branch=develop)](https://travis-ci.org/matthid/Yaaf.Xmpp.Runtime)
[![Build status](https://ci.appveyor.com/api/projects/status/3apdx33exbabe19p/branch/develop?svg=true)](https://ci.appveyor.com/project/matthid/yaaf-xmpp-runtime/branch/develop)

**Master Branch**

[![Build Status](https://travis-ci.org/matthid/Yaaf.Xmpp.Runtime.svg?branch=master)](https://travis-ci.org/matthid/Yaaf.Xmpp.Runtime)
[![Build status](https://ci.appveyor.com/api/projects/status/3apdx33exbabe19p/branch/master?svg=true)](https://ci.appveyor.com/project/matthid/yaaf-xmpp-runtime/branch/master)

## NuGet

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The Yaaf.Xmpp.Runtime library can be <a href="https://nuget.org/packages/Yaaf.Xmpp.Runtime">installed from NuGet</a>:
      <pre>PM> Install-Package Yaaf.Xmpp.Runtime</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

## Why another XMPP library/implementation?

 * For fun.
 * More flexible, than most other implementations.
 * Can be used to implement a server as well as clients. 
   Historically XMPP has the problem that features are often implemented on the server or the client only and therefore unusable.
   This library encourages to write both implementations at the same time.
 * Asynchronous from the core by design (NOTE: currently we are limited by System.XML not being completely asynchronous.
   Because we currently use a mono port, but now with https://github.com/dotnet/corefx we may be able to use that instead.)
 * While called "Xmpp.Runtime" this library is flexible enough to build any kind of XML based communication on top of any kind of transportation layer.
