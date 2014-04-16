Param( [string]$ApiKey )
if ( -not $ApiKey ) { write-host "Need -ApiKey."; return;3 }

Push-Location (split-path -parent $MyInvocation.MyCommand.Definition)

@('', '.net45') | % { 
	msbuild "..\Src\erecruit.Expr$_.csproj" /p:Configuration=Release /t:Rebuild 
	if ( $LastExitCode -ne 0 ) { Pop-Location; return; }
}

del *.nupkg
nuget pack
if ( $LastExitCode -ne 0 ) { Pop-Location; return; }

nuget push $(dir *.nupkg) -ApiKey $ApiKey

Pop-Location