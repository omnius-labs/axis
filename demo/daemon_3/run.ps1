Set-Location $PSScriptRoot

$Env:BuildTargetName = "daemon_3"
dotnet run --project ../../src/Omnius.Axis.Daemon/ -- --config "./config.yml"