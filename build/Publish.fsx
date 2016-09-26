(*
  This is a reusable chunk of FAKE script for packing and publishing library to Nuget
  from a CI server.
  
  publishPackage buildConfig templateFile   
        runs "paket pack" and "paket publish" on the given template,
        using the value of "NugetApiKey" build argument for API key
        and using given buildConfig (e.g. Debug or Release).
*)
open Fake

let nugetApiKey = getBuildParam "NugetApiKey"

let publishPackage buildConfig templateFile () =
  Paket.Pack (fun p -> { p with 
                          TemplateFile = templateFile
                          BuildConfig = buildConfig
                          OutputPath = "./nupkg" } )

  ProcessHelper.enableProcessTracing <- false
  Paket.Push (fun p -> { p with
                          WorkingDir = "./nupkg"
                          ApiKey = nugetApiKey } )
  ProcessHelper.enableProcessTracing <- true