# Starter script for the llama.cpp example manifest.
# Adapt paths/arguments for your machine; do not commit local paths to a public repo.
param(
    [string]$Root = $env:LLAMA_ROOT,
    [string]$Server = $env:LLAMA_SERVER,
    [string]$Models = $env:LLAMA_MODELS
)
$ErrorActionPreference = 'Stop'
if (-not $Root -or -not (Test-Path $Root)) { throw "Set LLAMA_ROOT to your llama.cpp checkout." }
if (-not $Server) { $Server = Join-Path $Root 'llama-server.exe' }
if (-not $Models) { $Models = Join-Path $Root 'models' }
if (-not $env:LLAMA_MODEL) { throw "Set LLAMA_MODEL to a .gguf path (or adapt this script)." }
$port = if ($env:LLAMA_PORT) { $env:LLAMA_PORT } else { '8082' }
$ctx = if ($env:LLAMA_CTX) { $env:LLAMA_CTX } else { '8192' }
$serverArgs = @('--host', '127.0.0.1', '--port', $port, '--model', $env:LLAMA_MODEL, '--ctx-size', $ctx)
if ($env:LLAMA_MMPROJ) { $serverArgs += @('--mmproj', $env:LLAMA_MMPROJ) }
if ($env:LLAMA_EXTRA_ARGS) { $serverArgs += $env:LLAMA_EXTRA_ARGS.Split(' ') }
& $Server @serverArgs
