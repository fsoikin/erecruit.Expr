& "$PSScriptRoot\.paket\paket.bootstrapper.exe"
if ($lastexitcode -ne 0) { exit }

& "$PSScriptRoot\.paket\paket.exe" restore
if ($lastexitcode -ne 0) { exit }

& "$PSScriptRoot\packages\build\FAKE\tools\FAKE.exe" "$PSScriptRoot\build.fsx" $args