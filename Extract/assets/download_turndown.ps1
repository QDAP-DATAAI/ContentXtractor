$url = "https://unpkg.com/turndown/dist/turndown.js"
$downloadPath = Join-Path -Path $PSScriptRoot -ChildPath "extensions/turndown.js"

Invoke-WebRequest -Uri $url -OutFile $downloadPath
Write-Output "File downloaded and saved to $downloadPath"
