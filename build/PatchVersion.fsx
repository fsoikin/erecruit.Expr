open Fake
open System.IO

let version = getBuildParamOrDefault "Version" "0.0.0.0"
let assembyInfoVersionRegex = System.Text.RegularExpressions.Regex("""(?<=(AssemblyVersion|AssemblyFileVersion)\(\")[\d\.]+(?=\"\))""")
let paketTemplateVersionRegex = System.Text.RegularExpressions.Regex("""(?<=version )([\d\.]+)""")

let private patchFile file (regex: System.Text.RegularExpressions.Regex) =
  let text = File.ReadAllText file
  let text = regex.Replace( text, version )
  File.WriteAllText( file, text )

let patchVersion assemblyInfoFile paketTemplateFile =
	patchFile assemblyInfoFile assembyInfoVersionRegex
	patchFile paketTemplateFile paketTemplateVersionRegex
