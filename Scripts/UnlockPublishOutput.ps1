# publish\win-x64\DirectoryManagement.exe calisiyorsa kapatir.
# Dosya hala kilitliyse yeniden adlandirir; GenerateBundle eski exe'yi silmek zorunda kalmaz.
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

$ErrorActionPreference = 'Stop'

function Normalize-Path([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return $null }
    $path = $path.Trim().Trim('"')
    if ($path.StartsWith('\\?\')) { $path = $path.Substring(4) }
    try { return [System.IO.Path]::GetFullPath($path) } catch { return $null }
}

if (-not (Test-Path -LiteralPath $PublishDir)) {
    exit 0
}

$publishFull = (Normalize-Path $PublishDir).TrimEnd('\')
$prefix = $publishFull + '\'
$exe = Join-Path $publishFull 'DirectoryManagement.exe'

function Test-PublishImage([string]$imagePath) {
    $norm = Normalize-Path $imagePath
    if (-not $norm) { return $false }
    return $norm.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-PublishProcessIds {
    $found = @{}

    Get-CimInstance Win32_Process -Filter "Name = 'DirectoryManagement.exe'" -ErrorAction SilentlyContinue |
        ForEach-Object {
            if (Test-PublishImage $_.ExecutablePath) {
                $found[[int]$_.ProcessId] = $true
            }
        }

    Get-Process -Name DirectoryManagement -ErrorAction SilentlyContinue |
        ForEach-Object {
            $image = $null
            try { $image = $_.Path } catch { }
            if (Test-PublishImage $image) {
                $found[[int]$_.Id] = $true
            }
        }

    return @($found.Keys)
}

function Test-Replaceable([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return $true }
    try {
        $stream = [System.IO.File]::Open(
            $path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
        $stream.Dispose()
        return $true
    } catch {
        return $false
    }
}

function Clear-ReadOnly([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return }
    $item = Get-Item -LiteralPath $path
    if ($item.Attributes -band [System.IO.FileAttributes]::ReadOnly) {
        $item.Attributes = $item.Attributes -band (-bnot [System.IO.FileAttributes]::ReadOnly)
    }
}

for ($attempt = 0; $attempt -lt 15; $attempt++) {
    $ids = @(Get-PublishProcessIds)
    foreach ($id in $ids) {
        Write-Host "Calisan portable uygulama kapatiliyor (PID $id)..."
        Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
    }

    if ($ids.Count -eq 0 -and (Test-Replaceable $exe)) {
        break
    }

    Start-Sleep -Milliseconds 200
}

Clear-ReadOnly $exe

if (-not (Test-Replaceable $exe)) {
    $stamp = Get-Date -Format 'yyyyMMddHHmmssfff'
    $parked = Join-Path $publishFull "DirectoryManagement.$stamp.exe.old"
    Write-Host "Exe kilitli, kenara aliniyor: $parked"
    Move-Item -LiteralPath $exe -Destination $parked -Force
}

Get-ChildItem -LiteralPath $publishFull -Filter 'DirectoryManagement.*.exe.old' -ErrorAction SilentlyContinue |
    ForEach-Object {
        Clear-ReadOnly $_.FullName
        try {
            Remove-Item -LiteralPath $_.FullName -Force -ErrorAction Stop
        } catch {
            Write-Host "Eski kopya silinemedi (hala acik olabilir): $($_.Name)"
        }
    }

if (-not (Test-Replaceable $exe)) {
    throw "Portable exe hala kilitli, uzerine yazilamiyor: $exe"
}

exit 0
