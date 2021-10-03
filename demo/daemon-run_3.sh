#!/bin/bash
cd $(dirname $0)

export BuildTargetName=daemon_3
dotnet run --project ../src/Omnius.Xeus.Service.Daemon/ -- "./$BuildTargetName/state" "./$BuildTargetName/logs" -v
