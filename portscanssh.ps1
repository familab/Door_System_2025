<#
.SYNOPSIS
    Scan hosts for SSH (port 22) and write successes to a CSV file.

.DESCRIPTION
    Attempts to connect to port 22 on one or more hosts and outputs the results to a CSV file.
    If no hosts are provided, the script attempts to detect the local IPv4 address and determine
    the local subnet range automatically (with safe limits to avoid huge scans).

.PARAMETER Hosts
    One or more hostnames or IP addresses to scan.

.PARAMETER HostFile
    Path to a file containing hostnames/IPs, one per line.

.PARAMETER TimeoutMs
    Connection timeout in milliseconds (default: 3000).

.PARAMETER Output
    Path to output CSV file (default: ssh_success.csv in current directory).

.PARAMETER Auto
    When specified (or no hosts provided), auto-detect local IPv4 and scan the local subnet.

.PARAMETER Force
    Allow scanning ranges larger than the safety limit (use with caution).

.PARAMETER MaxHosts
    Maximum number of hosts to scan without using -Force (default: 1024).

.EXAMPLE
    .\portscanssh.ps1 -Hosts "192.168.1.10","example.com" -Output results.csv

.EXAMPLE
    .\portscanssh.ps1 -HostFile hosts.txt -Output results.csv

.EXAMPLE
    .\portscanssh.ps1 -Auto -Output results.csv
#>

param(
    [Parameter(Position=0, ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
    [string[]]$Hosts,

    [string]$HostFile,

    [int]$TimeoutMs = 3000,

    [string]$Output = "ssh_success.csv",

    [switch]$Auto,

    [switch]$Force,

    [int]$MaxHosts = 1024
)

function Test-PortOpen {
    param(
        [Parameter(Mandatory=$true)] [string]$Target,
        [Parameter(Mandatory=$true)] [int]$Port = 22,
        [int]$TimeoutMs = 3000
    )

    # Prefer Test-NetConnection if available
    if (Get-Command -Name Test-NetConnection -ErrorAction SilentlyContinue) {
        try {
            $res = Test-NetConnection -ComputerName $Target -Port $Port -WarningAction SilentlyContinue -InformationLevel Detailed
            return [PSCustomObject]@{
                Host = $Target
                Port = $Port
                Open = $res.TcpTestSucceeded
                Timestamp = (Get-Date).ToString("s")
                Method = 'Test-NetConnection'
            }
        } catch {
            return [PSCustomObject]@{
                Host = $Target
                Port = $Port
                Open = $false
                Timestamp = (Get-Date).ToString("s")
                Method = 'Test-NetConnection-Error'
            }
        }
    }

    # Fallback to TcpClient
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $iar = $client.BeginConnect($Target, $Port, $null, $null)
        $wait = $iar.AsyncWaitHandle.WaitOne($TimeoutMs)
        if (-not $wait) {
            $client.Close()
            return [PSCustomObject]@{
                Host = $Target
                Port = $Port
                Open = $false
                Timestamp = (Get-Date).ToString("s")
                Method = 'TcpClient-Timeout'
            }
        }
        try {
            $client.EndConnect($iar)
            $open = $client.Connected
            $client.Close()
            return [PSCustomObject]@{
                Host = $Target
                Port = $Port
                Open = $open
                Timestamp = (Get-Date).ToString("s")
                Method = 'TcpClient'
            }
        } catch {
            $client.Close()
            return [PSCustomObject]@{
                Host = $Target
                Port = $Port
                Open = $false
                Timestamp = (Get-Date).ToString("s")
                Method = 'TcpClient-Error'
            }
        }
    } catch {
        return [PSCustomObject]@{
            Host = $Host
            Port = $Port
            Open = $false
            Timestamp = (Get-Date).ToString("s")
            Method = 'TcpClient-InitError'
        }
    }
}

function IPToInt {
    param([string]$ip)
    $p = $ip.Split('.') | ForEach-Object { [uint32]$_ }
    return ($p[0] -shl 24) -bor ($p[1] -shl 16) -bor ($p[2] -shl 8) -bor $p[3]
}

function IntToIP {
    param([uint32]$i)
    return ( ($i -shr 24) -band 255 ) + "." + ( ($i -shr 16) -band 255 ) + "." + ( ($i -shr 8) -band 255 ) + "." + ( $i -band 255 )
}

function MaskToPrefix {
    param([string]$mask)
    $parts = $mask.Split('.')
    $bits = ($parts | ForEach-Object { [Convert]::ToString([int]$_,2).PadLeft(8,'0') }) -join ''
    return ($bits.ToCharArray() | Where-Object { $_ -eq '1' }).Count
}

function IsAllowedIP {
    param([string]$ip)
    if (-not $ip) { return $false }
    # Explicitly exclude 127.* and all 172.* addresses; allow 10.* and 192.*
    if ($ip -match '^127\.') { return $false }
    if ($ip -match '^172\.') { return $false }
    if ($ip -match '^10\.') { return $true }
    if ($ip -match '^192\.') { return $true }
    return $false
}

function Get-LocalIPv4AndPrefix {
    # Try PowerShell Get-NetIPAddress first
    if (Get-Command -Name Get-NetIPAddress -ErrorAction SilentlyContinue) {
        $ipa = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -and $_.IPAddress -notmatch '^169\.254|^127\.' -and $_.PrefixOrigin -ne 'WellKnown' } | Select-Object -First 1
        if ($ipa) { return @{ IP = $ipa.IPAddress; Prefix = $ipa.PrefixLength } }
    }
    # Fallback to WMI
    try {
        $nic = Get-WmiObject -Class Win32_NetworkAdapterConfiguration | Where-Object { $_.IPEnabled -and $_.IPAddress } | Select-Object -First 1
        if ($nic) {
            $ip = $nic.IPAddress[0]
            $sub = $nic.IPSubnet[0]
            $prefix = MaskToPrefix $sub
            return @{ IP = $ip; Prefix = $prefix }
        }
    } catch {
        return $null
    }
    return $null
}

function Get-DefaultGateway {
    # Try Get-NetRoute/Get-NetIPConfiguration first
    if (Get-Command -Name Get-NetRoute -ErrorAction SilentlyContinue) {
        $gw = Get-NetRoute -DestinationPrefix "0.0.0.0/0" -AddressFamily IPv4 | Select-Object -First 1
        if ($gw -and $gw.NextHop -and $gw.NextHop -ne '0.0.0.0') { return $gw.NextHop }
    }
    if (Get-Command -Name Get-NetIPConfiguration -ErrorAction SilentlyContinue) {
        $cfg = Get-NetIPConfiguration | Where-Object { $_.IPv4DefaultGateway -and $_.IPv4DefaultGateway.NextHop } | Select-Object -First 1
        if ($cfg -and $cfg.IPv4DefaultGateway) { return $cfg.IPv4DefaultGateway.NextHop }
    }

    # Fallback to WMI
    try {
        $nic = Get-WmiObject -Class Win32_NetworkAdapterConfiguration | Where-Object { $_.IPEnabled -and $_.DefaultIPGateway } | Select-Object -First 1
        if ($nic -and $nic.DefaultIPGateway) { return $nic.DefaultIPGateway[0] }
    } catch { }
    return $null
}

function Get-IPRangeFromIPAndPrefix {
    param([string]$ip, [int]$prefix)
    $ipInt = IPToInt $ip
    # If the detected prefix produces a larger-than-/24 network, default to scanning a /24 for safety
    if ($prefix -lt 24 -and -not $Force) {
        Write-Host "Detected prefix /$prefix — defaulting to /24 for safety. Use -Force to scan full subnet." -ForegroundColor Yellow
        $parts = $ip.Split('.')
        $base = "$($parts[0]).$($parts[1]).$($parts[2])"
        $hosts = New-Object System.Collections.Generic.List[string]
        for ($i = 1; $i -le 254; $i++) { $hosts.Add("$base.$i") }
        return $hosts.ToArray()
    }

    # Construct mask using integer bit operations to avoid floating-point or overflow issues
    $ones = [uint64]0
    for ($i = 0; $i -lt $prefix; $i++) {
        $ones = ((($ones -shl 1) -bor 1) -band [uint64]0xFFFFFFFF)
    }
    if ($prefix -ge 32) { $mask64 = [uint64]0xFFFFFFFF } elseif ($prefix -eq 0) { $mask64 = 0 } else { $mask64 = ($ones -shl (32 - $prefix)) -band [uint64]0xFFFFFFFF }
    $mask = [uint32]$mask64
    $network = $ipInt -band $mask
    $wild64 = ([uint64]0xFFFFFFFF -band [uint64]0xFFFFFFFF) - $mask64
    $broadcast = ($network -bor [uint32]$wild64) -band [uint32]0xFFFFFFFF
    $start = $network + 1
    $end = $broadcast - 1
    $count = [int]($end - $start + 1)

    # Safety: if too many hosts, default to /24 unless forced
    if ($count -gt $MaxHosts -and -not $Force) {
        Write-Host "Detected subnet ($prefix) has $count hosts which exceeds MaxHosts ($MaxHosts). Defaulting to /24 for safety. Use -Force to override." -ForegroundColor Yellow
        $mask24 = [uint32]0xFFFFFF00
        $network24 = $ipInt -band $mask24
        $start = $network24 + 1
        $end = $network24 + 254
        $count = 254
    } elseif ($count -gt $MaxHosts -and $Force) {
        Write-Host "Warning: scanning large range of $count hosts because -Force was specified." -ForegroundColor Yellow
    }

    $hosts = New-Object System.Collections.Generic.List[string]
    for ($i = $start; $i -le $end; $i++) {
        $val = [uint32]$i
        $ipstr = IntToIP $val
        $hosts += $ipstr
    }
    return $hosts.ToArray()
}

# Resolve hosts from parameters, file, pipeline, or auto-detection
$allHosts = New-Object System.Collections.Generic.List[string]
if ($HostFile) {
    if (-Not (Test-Path $HostFile)) { Write-Error "Host file '$HostFile' not found."; exit 1 }
    $fileHosts = Get-Content $HostFile | Where-Object { $_ -and (-not $_.StartsWith('#')) } | ForEach-Object { $_.Trim() }
    $fileHosts = $fileHosts | Where-Object { IsAllowedIP $_ }
    if ($fileHosts.Count -eq 0) { Write-Host "No allowed hosts found in '$HostFile' after filtering (allowed: 10.*, 192.*; excluded: 172.*, 127.*)." -ForegroundColor Yellow }
    $allHosts.AddRange([string[]]$fileHosts)
}
if ($Hosts) {
    $allowed = $Hosts | Where-Object { IsAllowedIP $_ }
    $blocked = $Hosts | Where-Object { -not (IsAllowedIP $_) }
    if ($blocked.Count -gt 0) { Write-Host "Skipping hosts not in allowed ranges: $($blocked -join ', ')" -ForegroundColor Yellow }
    if ($allowed.Count -gt 0) { $allHosts.AddRange([string[]]$allowed) }
}

# If there's no input hosts, auto-detect network (default behavior)
if ($allHosts.Count -eq 0) {
    $Auto = $true
}

if ($Auto) {
    $info = Get-LocalIPv4AndPrefix
    if (-not $info) { Write-Error "Unable to detect local IPv4 address."; exit 1 }
    Write-Host "Detected local IP $($info.IP)/$($info.Prefix). Determining scan range..." -ForegroundColor Cyan

    $gateway = Get-DefaultGateway
    if ($gateway) { Write-Host "Detected gateway: $gateway" -ForegroundColor Cyan }

    # If local IP is disallowed and not forcing, try to use gateway if it's allowed
    $useGatewayOnly = $false
    if (-not (IsAllowedIP $info.IP) -and -not $Force) {
        if ($gateway -and (IsAllowedIP $gateway)) {
            Write-Host "Local IP $($info.IP) is not allowed; using gateway $gateway for auto range." -ForegroundColor Yellow
            $useGatewayOnly = $true
        } else {
            Write-Error "Detected local IP $($info.IP) is not in allowed ranges (10.*, 192.*). Use -Force to override or provide -Hosts/-HostFile."; exit 1
        }
    } elseif (-not (IsAllowedIP $info.IP) -and $Force) {
        Write-Host "Warning: local IP $($info.IP) is not in allowed ranges but -Force was specified; proceeding." -ForegroundColor Yellow
    }

    $allSets = New-Object 'System.Collections.Generic.HashSet[string]'

    if (-not $useGatewayOnly) {
        $rangeHosts = Get-IPRangeFromIPAndPrefix -ip $info.IP -prefix $info.Prefix
        # Add hosts from local IP range
        $localFiltered = $rangeHosts | Where-Object { IsAllowedIP $_ }
        foreach ($x in $localFiltered) { [void]$allSets.Add($x) }
    }

    # If gateway exists and is allowed (or forced), include its range
    if ($gateway -and ($useGatewayOnly -or $gateway -ne $info.IP)) {
        if (-not (IsAllowedIP $gateway) -and -not $Force) {
            if ($useGatewayOnly) { Write-Error "Gateway $gateway is not in allowed ranges; cannot proceed."; exit 1 } else { Write-Host "Gateway $gateway is not in allowed ranges; skipping gateway range." -ForegroundColor Yellow }
        } else {
            $gwRange = Get-IPRangeFromIPAndPrefix -ip $gateway -prefix $info.Prefix
            $gwFiltered = $gwRange | Where-Object { IsAllowedIP $_ }
            foreach ($x in $gwFiltered) { [void]$allSets.Add($x) }
            Write-Host "Including gateway-derived range (may overlap with local range)." -ForegroundColor Cyan
        }
    }

    # Extract unique hosts from HashSet into an array
    $final = @()
    $enum = $allSets.GetEnumerator()
    while ($enum.MoveNext()) { $final += $enum.Current }
    if (-not $final -or $final.Count -eq 0) { Write-Error "No allowed hosts to scan after filtering auto-detected ranges."; exit 1 }

    # Enforce MaxHosts safety for the combined range
    if ($final.Count -gt $MaxHosts -and -not $Force) {
        Write-Host "Combined auto-detected range has $($final.Count) hosts which exceeds MaxHosts ($MaxHosts). Defaulting to /24 around local IP for safety. Use -Force to override." -ForegroundColor Yellow
        $parts = $info.IP.Split('.')
        $base = "$($parts[0]).$($parts[1]).$($parts[2])"
        $final = (1..254 | ForEach-Object { "$base.$_" })
    } elseif ($final.Count -gt $MaxHosts -and $Force) {
        Write-Host "Warning: scanning large combined range of $($final.Count) hosts because -Force was specified." -ForegroundColor Yellow
    }

    $allHosts.AddRange([string[]]$final)
}

if ($allHosts.Count -eq 0) {
    Write-Host "No hosts available to scan. Provide -Hosts, -HostFile, or -Auto." -ForegroundColor Yellow
    exit 1
}

Write-Host "Scanning $($allHosts.Count) hosts for port 22..." -ForegroundColor Cyan

$results = New-Object System.Collections.Generic.List[object]
foreach ($h in $allHosts) {
    if (-not $h) { continue }
    $r = Test-PortOpen -Target $h -Port 22 -TimeoutMs $TimeoutMs
    if ($r.Open) {
        Write-Host "$h : OPEN" -ForegroundColor Green
        $results.Add($r)
    } else {
        Write-Host "$h : CLOSED" -ForegroundColor Red
    }
}
if ($results.Count -eq 0) {
    Write-Host "No hosts with port 22 open were found." -ForegroundColor Yellow
    # Still create/overwrite an empty CSV with header
    $header = [PSCustomObject]@{ Host='';Port=22;Open=$false;Timestamp=(Get-Date).ToString('s');Method='none' }
    $header | Select Host,Port,Open,Timestamp,Method | Export-Csv -Path $Output -NoTypeInformation
    Write-Host "Wrote $Output" -ForegroundColor Green
    exit 0
}

# Export successes to CSV
$results | Select Host,Port,Open,Timestamp,Method | Export-Csv -Path $Output -NoTypeInformation -Force
Write-Host "Wrote $($results.Count) successful entries to $Output" -ForegroundColor Green
