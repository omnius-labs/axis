#!/bin/bash
cd $(dirname $0)

export BuildTargetName=daemon-3
dotnet run --project ../../src/Omnius.Axis.Daemon/ -- -s ./storage/daemon -l "tcp(ip4(127.0.0.1),43203)" -v true