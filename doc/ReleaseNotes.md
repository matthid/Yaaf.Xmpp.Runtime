### 0.2.1

 * Fixed some bundled templates
 * remove an Include to Yaaf.FSharp.Scriping (removes a warning)

### 0.2.0

 * BREAKING: Redesigned how `buildConfig.fsx` has to be written!
   Now you need to implement a BuildConfiguration record type, this helps (in the future)
   that builds don't break when new features are introduced.
   This also allows us to set a lot of defaults for you.
   - you need to set `buildConfig` to a BuildConfiguration instance (all other variables are now ignored).
   - You can see the docs or look into `buildConfigDef.fsx` for a definition.
   - You need to update your `build.fsx` and `generateDocs.fsx` and add
     `#load "packages/Yaaf.AdvancedBuilding/content/buildConfigDef.fsx"`
     on the top (or use the latest from the package (see Quick-start tutorial)).
 * Implemented initial project and solution generation.
   - Can be enabled with `EnableProjectFileCreation`

### 0.1.4

 * `releaseDir` Configuration is no longer required.
 * BREAKING: You can/must specify `outNugetDir` after updating!
 * fix some broken links in scaffold files.
 * add missing generateDocs.fsx scaffold file.
 * fix reference templates ending up in the wrong folder.
 * add chmod +x to default build.sh (because we can't add that to the nuget package)

 ### 0.1.3

 * add build.fsx as well.
 * fixed some problems with use_nuget=false.
 * added some files to the nuget package starting with a dot (nuget removed them previously).

### 0.1.2

 * a CONTRIBUTION.md file is now assumed in project dir (instead of doc/Contributing.md)
 * we assume a LICENSE.md now
 * added some scaffolding files to the nuget package
 * NuGet is now build on ./build All (as test on CI and to allow users to use them internally, its pretty fast anyway)

### 0.1.1

 * Fix invalid backward slash on linux.
 * Don't ask for task and branch push (use "git flow" and push manually!)


### 0.1.0

 * Initial release
