# Yaaf.AdvancedBuilding implementation documentation 

## Building
Open the ``.sln`` file or run ``./build``.

> NOTE: You can only build the `.sln` file AFTER doing an initial `./build` (because nuget dependencies have to be resolved).

If you want to edit the build for a specific build you should look into the `./src/platform` folders for a solution file.

> NOTE: The platform solutions are generated with the first build!

DON'T edit those platform specific solutions/project files as they will be overwritten by the next build!
Just edit the templates in `./src/source/Yaaf.AdvancedBuilding/templates` NOT the ones in `./src/templates` (will be overwritten)
or the "normal" project and solution files or the project generation files (*._proj.fsx files).

## General overview:

This project tries to help you with your build setup.

### Issues / Features / TODOs

New features are accepted via github pull requests (so just fork away right now!):  https://github.com/matthid/Yaaf.AdvancedBuilding

Issues and TODOs are tracked on github, see: https://github.com/matthid/Yaaf.AdvancedBuilding/issues

Discussions/Forums are on IRC. 

### Versioning: 

http://semver.org/

### High level documentation ordered by project.

- `Yaaf.AdvancedBuilding`: For one a project to test the building infrastructure, in the future a helper to generate fsproj and csproj files.

### The Project structure:

- /.nuget/

	Nuget dependencies will be downloaded into this folder. 
	This folder can safely be deleted without affecting the build.

- /build/

	The project assemblies will be build into this folder. This folder can safely be deleted without affecting the build.

- /lib/

	library dependencies (currently not used). Most dependencies are automatically managed by nuget and not in this folder. 
	Only some internal dependencies and packages not in nuget. The git repository should always be "complete".

- /doc/

	Project documentation files. This folder contains both development and user documentation.

- /src/

	The Solution directory for all projects

	- /src/source/

		The root for all projects (not including unit test projects).

	- /src/templates/

		The root for project templates (DONT TOUCH: will be overwritten automatically with `/src/source/Yaaf.AdvancedBuilding/templates`)
    
	- /src/test/

		The root for all unit test projects.

- /test/

	The unit test assemblies will be build into this folder. This folder can safely be deleted without affecting the build.

- /tmp/

	This folder is ignored by git.

- /build.cmd, /build.sh, /build.fsx

	Files to directly start a build including unit tests via console (windows & linux).

Each project should have has a corresponding project with the name `Test.${ProjectName}` in the test folder.
This test project provides unit tests for the project `${ProjectName}`.

## Advanced Building

The build is done in different steps and you can execute the build until a given step or a single step:

First `build.sh` and `build.cmd` restore build dependencies and `nuget.exe`, then build.fsx is invoked:

 - `Clean`: cleans the directories (previous builds)
 - `RestorePackages`: restores nuget packages
 - `SetVersions`: sets the current version
 - `Build_net40`: build/test for net40
 - `Build_net45`: build/test for net45
 - `CopyToRelease`: copy the generated .dlls to release/lib
 - `LocalDoc`: create the local documentation you can view that locally
 - `All`: this does nothing itself but is used as a marker (executed by default when no parameter is given to ./build)
 - `VersionBump`: commits all current changes (when you change the version before you start the build you will have some files changed)
 - `NuGet`: generates the nuget packages
 - `GithubDoc`: generates the documentation for github
 - `ReleaseGithubDoc`: pushes the documentation to github
 - `Release`: a marker like "All"

You can execute all steps until a given point with `./build #Step#` (replace #Step# with `Build_net40` to execute `Clean`, `RestorePackages`, `SetVersions`, `Build_40`)

You can execute a single step with `build #Step#_single`: For example to build the nuget packages you can just invoke `./build NuGet_single` 

> Of course you need to have the appropriate dlls in place (otherwise the Nuget package creation will fail); ie have build the project before.


There is another (hidden) step `CleanAll` which will clean everything up (even build dependencies and the downloaded Nuget.exe), 
this step is only needed when build dependencies change. `git clean -d -x -f` is also a good way to do that

## Visual Studio / Monodevelop

As mentioned above you need to `build` at least once before you can open the 
solution files (`src/${ProjectName}.*.sln`) with Visual Studio / Monodevelop.

Just open the solution file for the build you want to edit. 

 - Note that files/references are managed through special `includes.targets` (eg: `src/source/${ProjectName}/includes.targets`) files.
 - The build configuration is managed through `src/buildConfig.targets`