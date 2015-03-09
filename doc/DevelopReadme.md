# Yaaf.Xmpp.Runtime implementation documentation 

## Building

This project uses Yaaf.AdvancedBuilding, see https://matthid.github.io/Yaaf.AdvancedBuilding/DevelopReadme.html for details.

## General overview:

This project is a full XMPP.core implementation strictly following the RFC specification.
It provides a lot of extension points to implement any kind of XEP.

### Issues / Features / TODOs

New features are accepted via github pull requests (so just fork away right now!):  https://github.com/matthid/Yaaf.Xmpp.Runtime

Issues and TODOs are tracked on github, see: https://github.com/matthid/Yaaf.Xmpp.Runtime/issues

### Versioning: 

http://semver.org/

### High level documentation ordered by project.

- `Mono.System.Xml`: A copy of the Mono System.Xml implementation (MIT license) which has been changed to work around some limitations in the async implementation.

- `Yaaf.Xml`: A simple interface for processing xml from within F#

- `Yaaf.Xmpp.Runtime.Core`: Some core interfaces, which need to be C# because of F# compiler limitations.

- `Yaaf.Xmpp.Runtime`: The xmpp library implementation.
