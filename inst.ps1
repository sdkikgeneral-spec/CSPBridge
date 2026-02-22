# jq インストールスクリプト（Windows / PowerShell）
# 実行例:
#   powershell -ExecutionPolicy Bypass -File .\inst.ps1

$ErrorActionPreference = 'Stop'

Write-Host 'jq のインストールを開始します...'

if (Get-Command jq -ErrorAction SilentlyContinue) {
    Write-Host 'jq は既にインストールされています。'
    jq --version
    exit 0
}

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    Write-Error 'winget が見つかりません。Microsoft Store 版 App Installer を導入してから再実行してください。'
}

winget install --id jqlang.jq --exact --accept-source-agreements --accept-package-agreements

if (Get-Command jq -ErrorAction SilentlyContinue) {
    Write-Host 'jq のインストールが完了しました。'
    jq --version
}
else {
    Write-Host 'jq コマンドが見つかりません。新しいターミナルを開いて jq --version を確認してください。'
}
