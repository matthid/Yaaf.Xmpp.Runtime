Yaaf.AdvancedBuilding
===================
## [Documentation](https://matthid.github.io/Yaaf.AdvancedBuilding/)

## Build status

**Development Branch**

[![Build Status](https://travis-ci.org/matthid/Yaaf.AdvancedBuilding.svg?branch=develop)](https://travis-ci.org/matthid/Yaaf.AdvancedBuilding)
[![Build status](https://ci.appveyor.com/api/projects/status/2xitdogybhrpd74o/branch/develop?svg=true)](https://ci.appveyor.com/project/matthid/yaaf-511/branch/develop)

**Master Branch**

[![Build Status](https://travis-ci.org/matthid/Yaaf.AdvancedBuilding.svg?branch=master)](https://travis-ci.org/matthid/Yaaf.AdvancedBuilding)
[![Build status](https://ci.appveyor.com/api/projects/status/2xitdogybhrpd74o/branch/master?svg=true)](https://ci.appveyor.com/project/matthid/yaaf-511/branch/master)

## NuGet

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The Yaaf.AdvancedBuilding library can be <a href="https://nuget.org/packages/Yaaf.AdvancedBuilding">installed from NuGet</a>:
      <pre>PM> Install-Package Yaaf.AdvancedBuilding</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

## Why AdvancedBuilding?

After setting up some projects with FAKE and paket I often find the need to edit the `build.fsx` and add new features.
And most of the time after adding a feature I wanted that feature on other projects as well.

Because of this I introduced Yaaf.AdvancedBuilding a nuget package containing a general FAKE build script, which can be configured.
Whenever I introduce a new feature into the build script I only need to update Yaaf.AdvancedBuilding to be able to use it.

Another pain point is building for multiple targets... fsproj files have only limited (read "no") support for
msbuild features like `<Compile Include="@(CompileList)" />` (to extract common files/references), therefore you can only really duplicate the
fsproj files and keep maintaining a bunch of them. This project tries to fix this by generating target specific msbuild files for you.

Features:

- Your build can be updated.
- Extendible with your own FAKE targets / dependencies.
- Additional support for building projects for multiple targets (net40, net45 and portable profiles)
   * Feature to specify fsproj and csproj templates and generate profile specific fsproj and csproj templates out of these
   * You will have FAKE targets for the generated files

## Quick intro

This project can be used to simplify the build process by replacing your build scripts with this nuget package.
It should especially help/simplify the process of building projects for multiple targets (net40, net45 and portable profiles).

The setup for this is not trivial and having an idea of FAKE (http://fsharp.github.com/FAKE) and paket (http://fsprojects.github.io/Paket/) helps.
We assume in this quick intro guide that you are able to setup a simple project with FAKE (without ProjectScaffold).

We also assume that you have a project named `Yaaf.Project` already setup with `FAKE` and paket with the following folder structure:

```
- .paket (paket folder)
- src
  - test (Directory for test projects)
    - Test.Yaaf.Project (test projects start with "Test." and produce equally named ".dll" files)
  - source (Directort for source projects)
  - Yaaf.Project.sln
- build.cmd
- build.sh
- build.fsx
- paket.dependencies
```

> Note other layouts are possible but this is the recommended layout which requires a minimal configuration later on.
> Paket is also not strictly required! You can get away with a `packages.config` nuget file and have your dependencies there.

### Getting Yaaf.AdvancedBuilding

Now we first add Yaaf.AdvancedBuilding to our dependencies

```
source https://nuget.org/api/v2

nuget MyDependency
nuget Yaaf.AdvancedBuilding
```

> You can basically remove all other build dependencies now and only keep Yaaf.AdvancedBuilding,
> because Yaaf.AdvancedBuilding will itself depend on all things it needs, and paket will install all of them.
> Paket's `internalize` feature isn't supported! If you have your own additional build dependencies you need to add them.

Now we update our dependencies:

```bash
.paket/paket.exe update
```

> Note when using NuGet you now need to manually download and extract Yaaf.AdvancedBuilding to `./packages/Yaaf.AdvancedBuilding`.
> You also need to specify all dependencies in `packages.config` instead of only Yaaf.AdvancedBuilding.

### Setting up Yaaf.AdvancedBuilding

> Note we assume you are using any kind of VCS system, because we will REPLACE some files and then tell you to look into their old versions later on!
> please commit/backup any unsaved changes now (excluding the changes from above)!

Now you can start setting up the project with:

```bash
# delegating build scripts
cp -r packages/Yaaf.AdvancedBuilding/scaffold/build/* ./
# Some initial files for your repro 
#(not required if you already have CONTRIBUTING.md, README.md and so forth in place)
# includes a .travis.yml and appveyor.yml for Travis and AppVeyor support
cp -r packages/Yaaf.AdvancedBuilding/scaffold/initial/* ./
# required if you want support for nuget in your project files 
# (not required if you use paket only)
cp -r packages/Yaaf.AdvancedBuilding/scaffold/nuget/* ./
# Some content files for the documentation
cp -r packages/Yaaf.AdvancedBuilding/scaffold/content/* ./
# copies some templates for the documentation
cp -r packages/Yaaf.AdvancedBuilding/scaffold/templates/* ./

# copies an build configuration to your project root
cp packages/Yaaf.AdvancedBuilding/examples/buildConfig/buildConfig.Yaaf.AdvancedBuilding.fsx ./buildConfig.fsx
```

### Setting up the `buildConfig.fsx` file

Now you need to setup the `buildConfig.fsx` file! 
Most settings can be taken from your old `build.fsx` file (which was overwritten above).
This step is documented in an extra documentation page: https://matthid.github.io/Yaaf.AdvancedBuilding/buildConfig.html
