# Generate original placeholder tiles with Qwen-Image-2.1-Turbo (sd.cpp).
# Configure the tool/model paths via environment variables or parameters:
#   SD_CLI, SD_DIFFUSION, SD_LLM, SD_VAE, SD_LLM_VISION, DSH_REFERENCE
# DSH is generated with image edit only when DSH_REFERENCE is provided.
param(
    [string]$SdCli = $env:SD_CLI,
    [string]$Diffusion = $env:SD_DIFFUSION,
    [string]$Llm = $env:SD_LLM,
    [string]$Vae = $env:SD_VAE,
    [string]$LlmVision = $env:SD_LLM_VISION,
    [string]$DshReference = $env:DSH_REFERENCE
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $SdCli)) { throw "Set SD_CLI to sd-cli.exe (or pass -SdCli)." }
if (-not (Test-Path $Diffusion) -or -not (Test-Path $Llm) -or -not (Test-Path $Vae)) {
    throw "Set SD_DIFFUSION, SD_LLM, SD_VAE to the local Qwen-Image model files."
}
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sigmas = '1.0,0.978453,0.95418,0.926626,0.89508,0.845148,0.704534,0.414568,0.0'
$out = Join-Path $root 'assets\placeholders\generated'
New-Item -ItemType Directory -Force $out | Out-Null
$neg = Join-Path $root 'assets\placeholders\prompts\negative.txt'

$textJobs = @(
    @{ n = 'llama-tile'; p = 'llama-tile.txt' },
    @{ n = 'comfy-tile'; p = 'comfy-tile.txt' }
)
foreach ($j in $textJobs) {
    $prompt = Join-Path $root ('assets\placeholders\prompts\' + $j.p)
    $dest = Join-Path $out ($j.n + '-source.webp')
    & $SdCli `
        --diffusion-model $Diffusion --llm $Llm --vae $Vae `
        --prompt-file $prompt --negative-prompt-file $neg `
        --cfg-scale 1.0 --steps 8 --sampling-method euler --sigmas $sigmas `
        -W 512 -H 512 --diffusion-fa `
        --vae-tiling --vae-tile-size 512 --vae-tile-overlap 0.25 `
        --seed 20261026 -o $dest
    if (-not (Test-Path $dest) -or (Get-Item $dest).Length -lt 1000) { throw "generation failed: $($j.n)" }
}

if (Test-Path $DshReference) {
    if (-not (Test-Path $LlmVision)) { throw "Set SD_LLM_VISION to the mmproj file for image edit." }
    $prompt = Join-Path $root 'assets\placeholders\prompts\dsh-edit-avatar.txt'
    $dest = Join-Path $out 'dsh-source.webp'
    & $SdCli `
        --diffusion-model $Diffusion --llm $Llm --llm_vision $LlmVision --vae $Vae `
        -r $DshReference `
        --prompt-file $prompt --negative-prompt-file $neg `
        --cfg-scale 1.0 --steps 8 --sampling-method euler --sigmas $sigmas `
        -W 512 -H 512 --diffusion-fa `
        --vae-tiling --vae-tile-size 512 --vae-tile-overlap 0.25 `
        --seed 20261030 -o $dest
    if (-not (Test-Path $dest) -or (Get-Item $dest).Length -lt 1000) { throw "generation failed: dsh" }
}
Write-Host 'placeholder tiles generated'
