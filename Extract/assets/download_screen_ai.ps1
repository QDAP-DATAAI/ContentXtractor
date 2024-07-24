$appid = "mfhmdacoffpmifoibamicehhklffanao" # Screen AI extension id
$operatingSystem = "Linux" # "Linux", "mac", "windows"...; Case sensitive!
$arch = "x64" # we only care about x64

$downloadPath = Join-Path -Path $PSScriptRoot -ChildPath "extensions/screen_ai"

if (-not (Test-Path -Path $downloadPath)) {
    New-Item -ItemType Directory -Path $downloadPath | Out-Null
}

$body = @"
{
    `"request`": {
        `"acceptformat`": `"crx3`",
        `"app`": [
            {
                `"appid`": `"$appid`",
                `"cohortname`": `"Stable`",
                `"enabled`": true,
                `"updatecheck`": {}
            }
        ],
        `"arch`": `"$arch`",
        `"os`": {
            `"platform`": `"$operatingSystem`"
        },
        `"protocol`": `"3.1`"
    }
}
"@

try {
    $response = Invoke-RestMethod 'https://update.googleapis.com/service/update2/json' -Method 'POST' -Body $body
}
catch {
    Write-Error "Failed to get response from server: $($_.Exception.Message)"
    exit 1
}

$xssiResponsePreamble = ")]}'"
$responseString = $response | Out-String

if ($responseString.StartsWith($xssiResponsePreamble)) {
    $responseString = $responseString.Substring($xssiResponsePreamble.Length)
}

try {
    $responseJson = $responseString | ConvertFrom-Json
}
catch {
    Write-Error "Failed to parse JSON response: $responseString"
    exit 1
}

$app = $responseJson.response.app[0]

if ($app.updatecheck.status -eq "noupdate" -or -not $app.updatecheck.urls) {
    Write-Output "No results"
}
else {
    $version = $app.updatecheck.manifest.version
    Write-Output "Found version $version for $operatingSystem"
        
    $urls = @()
    $packageName = $app.updatecheck.manifest.packages.package.name
    foreach ($urlObj in $app.updatecheck.urls.url) {
        $urls += "$($urlObj.codebase)$packageName"
    }

    # try with https first
    $urls = $urls | Sort-Object { $_ -notlike "https*" }

    $downloaded = $false
    foreach ($url in $urls) {
        Write-Output "Trying to download from $url"
        $outputFilePath = Join-Path -Path $downloadPath -ChildPath $packageName

        try {
            Invoke-WebRequest -Uri $url -OutFile $outputFilePath -ErrorAction Stop
            Write-Output "Downloaded successfully from $url"
            $downloaded = $true

            break
        }
        catch {
            Write-Output "Failed to download from ${url}: $($_.Exception.Message)"
        }
    }
        
    if (-not $downloaded) {
        Write-Error "All download attempts failed!"
        exit 1
    }
}
 