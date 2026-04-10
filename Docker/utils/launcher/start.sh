#!/bin/bash
export NETWORK_NAME=$(ip -4 addr show scope global | grep -oP '(?<=inet )\d+\.\d+\.\d+\.\d+' | grep -v '^127\.' | head -n 1)
#sysctl -w net.ipv6.conf.all.disable_ipv6=1
dotnet ./KlusterKite.NodeManager.Launcher.dll
