[CmdletBinding()]
param([switch] $Check)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$resources = [ordered]@{}
Get-ChildItem (Join-Path $root 'src/NexusDash/I18n') -Filter '*.json' | Sort-Object Name | ForEach-Object {
    $json = Get-Content $_.FullName -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
    $resources[$json.cultureName] = $json.NexusDash.NexusDashL
}
$content = 'window.translations = ' + ($resources | ConvertTo-Json -Depth 20) + ";`n"
$target = Join-Path $root 'scripts/prototype/assets/resources.js'
if ($Check) {
    if ((Get-Content $target -Raw -Encoding utf8) -cne $content) { throw 'Prototype resources are stale.' }
} else {
    [System.IO.File]::WriteAllText($target, $content, [System.Text.UTF8Encoding]::new($false))
}

& (Join-Path $PSScriptRoot 'build_standalone_prototype.ps1') -Check:$Check
