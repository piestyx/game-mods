#!/bin/bash

MOD_NAME="QudStateExtractor"
SRC_DIR="./src"
OUT_DIR="./ModAssemblies"

mkdir -p "$OUT_DIR"

mono-csc -target:library -out:"$OUT_DIR/$MOD_NAME.dll" \
  -reference:"/home/games/caves_of_qud/CoQ_Data/Managed/Assembly-CSharp.dll" \
  -reference:"/home/games/caves_of_qud/CoQ_Data/Managed/0Harmony.dll" \
  -reference:"/home/games/caves_of_qud/CoQ_Data/Managed/UnityEngine.CoreModule.dll" \
  -reference:"/usr/lib/mono/4.8-api/Facades/netstandard.dll" \
  "$SRC_DIR"/*.cs
