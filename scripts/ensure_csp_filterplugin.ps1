param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir,

    [Parameter(Mandatory = $true)]
    [string]$ZipUrl
)

$ErrorActionPreference = 'Stop'

$targetDir = Join-Path $ProjectDir 'CSP_FilterPlugIn'
if (Test-Path -LiteralPath $targetDir) {
    Write-Host "CSP_FilterPlugIn is already present: $targetDir"
    exit 0
}

$zipPath = Join-Path $ProjectDir 'CSP_FilterPlugIn.zip'

try {
    Write-Host "Downloading SDK ZIP from: $ZipUrl"
    Invoke-WebRequest -Uri $ZipUrl -OutFile $zipPath

    Write-Host "Extracting ZIP into: $ProjectDir"
    Expand-Archive -Path $zipPath -DestinationPath $ProjectDir -Force

    if (-not (Test-Path -LiteralPath $targetDir)) {
        $found = Get-ChildItem -Path $ProjectDir -Directory -Recurse |
            Where-Object { $_.Name -eq 'CSP_FilterPlugIn' -and $_.FullName -ne $targetDir } |
            Select-Object -First 1

        if ($null -ne $found) {
            Write-Host "Moving extracted folder to: $targetDir"
            Move-Item -Path $found.FullName -Destination $targetDir -Force
        }
    }
}
finally {
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
}

if (-not (Test-Path -LiteralPath $targetDir)) {
    throw "CSP_FilterPlugIn was not found after extraction. Please verify ZIP contents: $ZipUrl"
}

Write-Host "CSP_FilterPlugIn has been prepared at: $targetDir"
