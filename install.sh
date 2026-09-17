#!/bin/sh

rm -rf ./out
dotnet publish ./GameServer.Cli -c Release -o ./out || exit $?

mkdir -p "$HOME/.local/bin"
mv ./out/* "$HOME/.local/bin/"

case ":$PATH:" in
    *":$HOME/.local/bin:"*) ;;
    *)
        grep -Fq "$HOME/.local/bin" "$HOME/.profile" 2>/dev/null ||
            printf 'export PATH="%s:$PATH"\n' "$HOME/.local/bin" >> "$HOME/.profile"
        export PATH="$HOME/.local/bin:$PATH"
        printf 'Added %s to PATH\n' "$HOME/.local/bin"
        ;;
esac