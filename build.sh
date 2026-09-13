#!/usr/bin/env bash
# Build the FEB Simulator players. Usage: ./build.sh [scene|linux|mac|windows|all]
# Needs the Unity editor pinned in ProjectSettings/ProjectVersion.txt and an activated licence.
set -euo pipefail
cd "$(dirname "$0")"
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)/Editor/Unity}"
mkdir -p Logs
run() { echo ">> $1"; "$UNITY" -batchmode -nographics -quit -projectPath . -executeMethod "$1" -logFile "Logs/$2.log" || { tail -40 "Logs/$2.log"; exit 1; }; }
case "${1:-all}" in
  scene)   run FebScene.Create scene ;;
  linux)   run FebBuild.Linux linux ;;
  mac)     run FebBuild.Mac mac ;;
  windows) run FebBuild.Windows windows ;;
  all)     run FebScene.Create scene; run FebBuild.Linux linux; run FebBuild.Mac mac; run FebBuild.Windows windows ;;
  *) echo "usage: $0 [scene|linux|mac|windows|all]"; exit 2 ;;
esac
