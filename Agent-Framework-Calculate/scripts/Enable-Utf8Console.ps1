[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues["Out-File:Encoding"] = "utf8"
$PSDefaultParameterValues["Set-Content:Encoding"] = "utf8"
$PSDefaultParameterValues["Add-Content:Encoding"] = "utf8"

cmd /c chcp 65001 > $null

$samplePath = Join-Path $PSScriptRoot "..\docs\agent.md"
$resolvedSamplePath = [System.IO.Path]::GetFullPath($samplePath)

Write-Host "UTF-8 console enabled." -ForegroundColor Green
Write-Host "InputEncoding  : $([Console]::InputEncoding.WebName)"
Write-Host "OutputEncoding : $([Console]::OutputEncoding.WebName)"
Write-Host "Code page      : 65001"
Write-Host ""
Write-Host "Recommended usage:" -ForegroundColor Cyan
Write-Host "  . .\scripts\Enable-Utf8Console.ps1"
Write-Host ""
Write-Host "Validation example:" -ForegroundColor Cyan
Write-Host ("  Get-Content '{0}' -Encoding utf8 | Select-Object -First 5" -f $resolvedSamplePath)
