#!/bin/sh

rm -rfv contrib || exit 1
mkdir -v contrib || exit 1

echo "[ Running Rust-based setup script ]"
cd Melodia || exit 1
    cargo run --features setup_tool || exit 1
cd .. || exit 1

echo "[ Restoring projects... ]"
cd MelodiaBuildTool || exit 1
    dotnet restore || exit 1
    dotnet msbuild -p:Configuration=Release || exit 1
    mono bin/Release/net4.5.2/MelodiaPatcher.exe || exit 1
cd .. || exit 1
