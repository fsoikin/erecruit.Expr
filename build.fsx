#r @"packages/build/FAKE/tools/FakeLib.dll"
#load "./build/Publish.fsx"
#load "./build/PatchVersion.fsx"
open Fake

let config = getBuildParamOrDefault "Config" "Debug"

let build target () =
  MSBuildHelper.build 
    (fun p -> { p with
                  Targets = [target]
                  Properties = ["Configuration", config]
                  Verbosity = Some MSBuildVerbosity.Detailed })
    "./Src/erecruit.Expr.sln"

Target "Build" <| fun _ -> PatchVersion.patchVersion "./Src/Properties/AssemblyInfo.cs" "./Src/paket.template"; build "Build" ()
Target "Clean" <| build "Clean"
Target "Rebuild" DoNothing
Target "RunTests" <| fun _ -> [sprintf "./Tests/bin/%s/erecruit.Expr.Tests.dll" config] |> Fake.Testing.XUnit2.xUnit2 id 
Target "Publish" <| Publish.publishPackage config "./Src/paket.template"

"Build" ==> "Rebuild"
"Clean" ==> "Rebuild"
"Clean" ?=> "Build"

"Build" ==> "RunTests" ==> "Publish"

RunTargetOrDefault "Build"