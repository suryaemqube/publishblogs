<#
  serve.ps1 - tiny static server for the prototype.

  The prototype also opens straight from the filesystem (every link is
  relative), so this is a convenience, not a requirement.

  Usage:  powershell -File tools\serve.ps1 [-Port 8731] [-Open]
#>
param([int]$Port = 8731, [switch]$Open)

$ErrorActionPreference = 'Stop'
$Root = Join-Path (Split-Path -Parent $PSScriptRoot) 'prototype'
if (-not (Test-Path $Root)) { throw "prototype folder not found at $Root" }

$Types = @{
  '.html' = 'text/html; charset=utf-8'
  '.css'  = 'text/css; charset=utf-8'
  '.svg'  = 'image/svg+xml'
  '.js'   = 'text/javascript; charset=utf-8'
  '.png'  = 'image/png'
  '.jpg'  = 'image/jpeg'
  '.woff2'= 'font/woff2'
}

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()
Write-Host "Swan prototype: http://localhost:$Port/login.html"
Write-Host "Serving $Root - press Ctrl+C to stop."

if ($Open) { Start-Process "http://localhost:$Port/login.html" }

while ($listener.IsListening) {
  try {
    $ctx = $listener.GetContext()
    $rel = $ctx.Request.Url.AbsolutePath.TrimStart('/')
    if ($rel -eq '' -or $rel -eq 'index.html') { $rel = 'login.html' }

    $file = Join-Path $Root $rel
    # keep requests inside the prototype folder
    $full = [IO.Path]::GetFullPath($file)
    if (-not $full.StartsWith([IO.Path]::GetFullPath($Root), 'OrdinalIgnoreCase')) { $rel = 'login.html'; $full = Join-Path $Root $rel }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { $rel = 'login.html'; $full = Join-Path $Root $rel }

    $ext = [IO.Path]::GetExtension($full).ToLower()
    $ctx.Response.ContentType = if ($Types.ContainsKey($ext)) { $Types[$ext] } else { 'application/octet-stream' }
    $bytes = [IO.File]::ReadAllBytes($full)
    $ctx.Response.ContentLength64 = $bytes.Length
    $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
    $ctx.Response.Close()
  } catch {
    try { $ctx.Response.Abort() } catch { }
  }
}
