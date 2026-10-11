<#
  build-prototype.ps1 - generates the Swan Content Publisher HTML prototype.

  One writer journey, mock data, fully linked. The shared shell (nav, top bar,
  context bar) is emitted by a function so every page stays identical; page
  bodies live in this one file so restyling means editing one place.

  Brand values live only in assets/tokens.css - this file contains no colours.

  Usage:  powershell -File tools\build-prototype.ps1
#>

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = Split-Path -Parent $PSScriptRoot
$Out  = Join-Path $Root 'prototype'
New-Item -ItemType Directory -Path $Out -Force | Out-Null

# ---------------------------------------------------------------- icons
$Paths = @{
  grid    = '<rect x="2.5" y="2.5" width="5" height="5" rx="1"/><rect x="8.5" y="2.5" width="5" height="5" rx="1"/><rect x="2.5" y="8.5" width="5" height="5" rx="1"/><rect x="8.5" y="8.5" width="5" height="5" rx="1"/>'
  file    = '<path d="M9 1.8H4.2A1.2 1.2 0 0 0 3 3v10a1.2 1.2 0 0 0 1.2 1.2h7.6A1.2 1.2 0 0 0 13 13V5.8z"/><path d="M9 1.8V5.8h4"/>'
  layers  = '<path d="M8 1.6 14.4 5 8 8.4 1.6 5z"/><path d="M1.6 8 8 11.4 14.4 8"/><path d="M1.6 11 8 14.4 14.4 11"/>'
  shield  = '<path d="M8 1.6 13.4 3.6V8c0 3-2.3 5.5-5.4 6.4C4.9 13.5 2.6 11 2.6 8V3.6z"/><path d="M5.8 7.9 7.4 9.5l3-3.2"/>'
  send    = '<path d="M14.4 1.6 1.6 6.8l5 1.9 1.9 5z"/><path d="M14.4 1.6 6.6 8.7"/>'
  alert   = '<path d="M8 1.9 15 13.4H1z"/><path d="M8 6.2v3.4"/><circle cx="8" cy="11.5" r=".7" fill="currentColor" stroke="none"/>'
  x       = '<path d="M3.6 3.6 12.4 12.4M12.4 3.6 3.6 12.4"/>'
  search  = '<circle cx="7" cy="7" r="4.6"/><path d="M10.5 10.5 14 14"/>'
  plus    = '<path d="M8 3.2v9.6M3.2 8h9.6"/>'
  chevd   = '<path d="M3.6 6.2 8 10.6l4.4-4.4"/>'
  chevr   = '<path d="M6.2 3.6 10.6 8l-4.4 4.4"/>'
  grip    = '<circle cx="6" cy="4" r=".9" fill="currentColor" stroke="none"/><circle cx="10" cy="4" r=".9" fill="currentColor" stroke="none"/><circle cx="6" cy="8" r=".9" fill="currentColor" stroke="none"/><circle cx="10" cy="8" r=".9" fill="currentColor" stroke="none"/><circle cx="6" cy="12" r=".9" fill="currentColor" stroke="none"/><circle cx="10" cy="12" r=".9" fill="currentColor" stroke="none"/>'
  external= '<path d="M9.2 2.2H13.8v4.6"/><path d="M13.8 2.2 7.6 8.4"/><path d="M12 9.6V13a1 1 0 0 1-1 1H3.2a1 1 0 0 1-1-1V5.2a1 1 0 0 1 1-1H6.6"/>'
  eye     = '<path d="M1.6 8S4.1 3.5 8 3.5 14.4 8 14.4 8 11.9 12.5 8 12.5 1.6 8 1.6 8z"/><circle cx="8" cy="8" r="1.8"/>'
  gear    = '<circle cx="8" cy="8" r="2.2"/><path d="M12.9 9.8a1.1 1.1 0 0 0 .22 1.21l.04.04a1.33 1.33 0 1 1-1.88 1.88l-.04-.04a1.1 1.1 0 0 0-1.21-.22 1.1 1.1 0 0 0-.67 1v.11a1.33 1.33 0 0 1-2.66 0v-.06a1.1 1.1 0 0 0-.72-1 1.1 1.1 0 0 0-1.21.22l-.04.04A1.33 1.33 0 1 1 2.85 11.1l.04-.04a1.1 1.1 0 0 0 .22-1.21 1.1 1.1 0 0 0-1-.67h-.11a1.33 1.33 0 0 1 0-2.66h.06a1.1 1.1 0 0 0 1-.72 1.1 1.1 0 0 0-.22-1.21l-.04-.04A1.33 1.33 0 1 1 4.68 2.67l.04.04a1.1 1.1 0 0 0 1.21.22h.05a1.1 1.1 0 0 0 .67-1v-.11a1.33 1.33 0 0 1 2.66 0v.06a1.1 1.1 0 0 0 .67 1 1.1 1.1 0 0 0 1.21-.22l.04-.04a1.33 1.33 0 1 1 1.88 1.88l-.04.04a1.1 1.1 0 0 0-.22 1.21v.05a1.1 1.1 0 0 0 1 .67h.11a1.33 1.33 0 0 1 0 2.66h-.06a1.1 1.1 0 0 0-1 .67z"/>'
  users   = '<path d="M10.8 14v-1.4a2.8 2.8 0 0 0-2.8-2.8H4.4a2.8 2.8 0 0 0-2.8 2.8V14"/><circle cx="6.2" cy="5.2" r="2.4"/><path d="M13.4 14v-1.4a2.8 2.8 0 0 0-2.1-2.7M10.2 3a2.4 2.4 0 0 1 0 4.6"/>'
  globe   = '<circle cx="8" cy="8" r="6.1"/><path d="M1.9 8h12.2"/><path d="M8 1.9c1.9 2.2 1.9 9 0 12.2M8 1.9c-1.9 2.2-1.9 9 0 12.2"/>'
  refresh = '<path d="M13.6 8a5.6 5.6 0 1 1-1.7-4"/><path d="M13.9 2.2v3.5h-3.5"/>'
  clock   = '<circle cx="8" cy="8" r="6.1"/><path d="M8 4.4V8l2.5 1.5"/>'
  link    = '<path d="M6.6 9.4 9.4 6.6"/><path d="M7.1 4.5 8.5 3.1a2.8 2.8 0 0 1 4 4l-1.4 1.4"/><path d="M8.9 11.5 7.5 12.9a2.8 2.8 0 0 1-4-4l1.4-1.4"/>'
  image   = '<rect x="2.2" y="2.8" width="11.6" height="10.4" rx="1.4"/><circle cx="5.9" cy="6.4" r="1.1"/><path d="M2.6 11.2 6 8.6l2.7 2.4 2-1.6 2.7 2"/>'
  check   = '<path d="M3 8.4 6.4 11.8 13 4.2"/>'
  okcirc  = '<circle cx="8" cy="8" r="6.1"/><path d="M5.2 8.2 7.3 10.3 10.9 5.9"/>'
  xcirc   = '<circle cx="8" cy="8" r="6.1"/><path d="M5.7 5.7 10.3 10.3M10.3 5.7 5.7 10.3"/>'
  info    = '<circle cx="8" cy="8" r="6.1"/><path d="M8 7.4v3.6"/><circle cx="8" cy="5" r=".7" fill="currentColor" stroke="none"/>'
  arrowl  = '<path d="M13 8H3.2"/><path d="M6.6 4.6 3.2 8l3.4 3.4"/>'
  arrowr  = '<path d="M3 8h9.8"/><path d="M9.4 4.6 12.8 8l-3.4 3.4"/>'
  logout  = '<path d="M9.2 14H4a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1h5.2"/><path d="M11.4 8H6"/><path d="M9 5.2 11.8 8 9 10.8"/>'
  lock    = '<rect x="3.4" y="7" width="9.2" height="6.6" rx="1.4"/><path d="M5.6 7V5.1a2.4 2.4 0 0 1 4.8 0V7"/>'
  upload  = '<path d="M8 11V3"/><path d="M4.4 6.6 8 3l3.6 3.6"/><path d="M2.6 12.6h10.8"/>'
  dl      = '<path d="M8 3v8"/><path d="M4.4 7.4 8 11l3.6-3.6"/><path d="M2.6 13.4h10.8"/>'
  tag     = '<path d="M2.4 2.4h5.2l6 6-5.2 5.2-6-6z"/><circle cx="5.2" cy="5.2" r=".9"/>'
  user    = '<circle cx="8" cy="5.4" r="2.9"/><path d="M2.8 14a5.2 5.2 0 0 1 10.4 0"/>'
  menu    = '<path d="M2.6 4.4h10.8M2.6 8h10.8M2.6 11.6h10.8"/>'
  list    = '<path d="M5.6 4.2h8M5.6 8h8M5.6 11.8h8"/><circle cx="2.9" cy="4.2" r=".9" fill="currentColor" stroke="none"/><circle cx="2.9" cy="8" r=".9" fill="currentColor" stroke="none"/><circle cx="2.9" cy="11.8" r=".9" fill="currentColor" stroke="none"/>'
  edit    = '<path d="M11.2 2.4 13.6 4.8 5.6 12.8H3.2v-2.4z"/>'
  zap     = '<path d="M8.9 1.4 2.9 9.1h4.4l-.6 5.5 6-7.7H8.3z"/>'
  bell    = '<path d="M11.8 6.4a3.8 3.8 0 1 0-7.6 0c0 4.2-1.6 5.4-1.6 5.4h10.8s-1.6-1.2-1.6-5.4"/><path d="M9 13.6a1.3 1.3 0 0 1-2 0"/>'
}
function Ico([string]$n, [int]$w = 16) {
  $p = $Paths[$n]
  if (-not $p) { throw "unknown icon '$n'" }
  '<svg viewBox="0 0 16 16" width="' + $w + '" height="' + $w + '" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + $p + '</svg>'
}

# ---------------------------------------------------------------- mock data
$Articles = @(
  @{ Id=1; Label='Thought Leadership'; Words=331; Paras=12; Status='Draft'
     Title='The IIM Ahmedabad Case Study: How Our Customer Service Became a Global Teaching Case'
     H2s=@(); Lists=0
     Note='No H2 structure - runs straight from the intro to a closing paragraph. Reads as an email, not a post.' }
  @{ Id=2; Label='Health & Wellness'; Words=618; Paras=19; Status='Needs SEO'
     Title='What is Relocation Stress Syndrome: Why Moving Feels So Overwhelming'
     H2s=@('The Clinical Reality of Relocation Stress','Why the Moving Process Exhausts the Brain','Defeat Relocation Stress with E-Movers')
     Lists=1
     Note='Title is 76 characters. Too long for a SERP title and it wraps to two lines on mobile.' }
  @{ Id=3; Label='Real Estate'; Words=629; Paras=23; Status='Ready'
     Title='Modern Renting in Dubai: How Flexi Rent and Payment Rewards Are Easing the Relocation Journey'
     H2s=@("1. The Dubai Land Department's Flexi Rent Initiative",'2. Unlocking Value Through Digital Rent Rewards','Reclaiming Comfort and Reducing Move-Day Friction','Partner with the Relocation Specialists at E-Movers')
     Lists=2
     Note='Two numbered H2s, then two unnumbered. Inconsistent - pick one convention and the outline tool will follow it.' }
  @{ Id=4; Label='Trust & Reputation'; Words=348; Paras=18; Status='Needs SEO'
     Title='Why E-Movers Service Continues to Earn 5-Star Reviews'
     H2s=@('1. Clear Financial Value','2. Safe and Protected Transit','3. Process-Driven Execution','4. Trained, Permanent Teams','5. Practical Service Innovations','Experience a True "No Mess, No Stress" Move')
     Lists=0
     Note='348 words is below the 500-word minimum. Thin content will not rank against the aggregators.' }
)

$Reviewers = @(
  @{ Name='Aisha Rahman'; Initials='AR'; Role='Content Lead'; State='approved'; When='28 Sep, 16:40' }
  @{ Name='Vikram S. ';    Initials='VS'; Role='SEO Manager'; State='pending';  When='' }
  @{ Name='Meera Iyer';   Initials='MI'; Role='Legal';       State='pending';  When='' }
)

$Failures = @(
  @{ Site='emoversuae.com';       Stage='post';    Code='rest_forbidden';       Msg='REST API returned 401 Unauthorized. Application Password is missing or was revoked.'; SiteId=3 }
  @{ Site='emoversrss.com';       Stage='media';   Code='attachment_missing';   Msg='Featured image media ID 4812 no longer exists on the target site. The post was created without a featured image.'; SiteId=7 }
  @{ Site='swanwomenswear.ae';    Stage='post';    Code='term_missing';         Msg='Term "Real Estate" does not exist in the target taxonomy. Assigned the site default "General" instead.'; SiteId=4 }
  @{ Site='ladypackers.ae';       Stage='preflight'; Code='taxonomy_unregistered'; Msg='Taxonomy "insights_category" is not registered with show_in_rest, so Swan cannot verify its terms. See decision H.'; SiteId=9 }
)

# ---------------------------------------------------------------- shell
$NavSec = @(
  @{ Label='Workspace'; Items=@(
      @{ H='dashboard.html';    T='Dashboard';    I='grid' }
      @{ H='documents.html';    T='Documents';    I='file'; Badge='2' }
      @{ H='articles.html';     T='Articles';     I='layers' }
  ) }
  @{ Label='Publishing'; Items=@(
      @{ H='review.html';            T='Review queue';  I='shield'; Badge='3' }
      @{ H='publications.html';      T='Publications';   I='send' }
      @{ H='publishing-failures.html';T='Failures';      I='alert'; Badge='4' }
  ) }
  @{ Label='Clients'; Items=@(
      @{ H='sites.html'; T='Sites'; I='globe' }
  ) }
  @{ Label='Settings'; Items=@(
      @{ H='settings-team.html';      T='Team';      I='users' }
      @{ H='settings-workspace.html'; T='Workspace'; I='gear' }
  ) }
)

function NavHtml([string]$active) {
  $sb = [System.Text.StringBuilder]::new()
  foreach ($sec in $NavSec) {
    [void]$sb.Append("`n      <div class=`"nav-sec`">$($sec.Label)</div>")
    foreach ($it in $sec.Items) {
      $on = if ($it.H -eq $active) { ' on' } else { '' }
      $badge = if ($it.ContainsKey('Badge')) { "<span class=`"nav-badge`">$($it.Badge)</span>" } else { '' }
      [void]$sb.Append("`n      <a href=`"$($it.H)`"$on>$(Ico $it.I)<span>$($it.T)</span>$badge</a>")
    }
  }
  $sb.ToString()
}

function TopHtml {
  @"
    <header class="topbar">
      <form class="search" action="search.html" method="get">
        $(Ico 'search')
        <input type="search" name="q" placeholder="Search articles, sites, deployments&hellip;" aria-label="Search">
        <kbd>/</kbd>
      </form>
      <div class="row-tight" style="margin-left:auto">
        <a class="btn btn-ghost btn-sm" href="first-run.html">$(Ico 'zap') Get started</a>
        <span class="avatar" title="Aisha Rahman">AR</span>
        <a class="btn btn-ghost btn-sm" href="login.html" title="Sign out">$(Ico 'logout')</a>
      </div>
    </header>
    <div class="ctxbar">
      <label for="ctx-client">Client</label>
      <span class="ctx" id="ctx-client">$(Ico 'user') E-Movers International</span>
      <label for="ctx-site">Site</label>
      <span class="ctx" id="ctx-site">$(Ico 'globe') All sites (9)</span>
      <span class="muted xs" style="margin-left:auto">Client and site stay selected until you change them</span>
    </div>
"@
}

function Page {
  param([string]$File, [string]$Title, [string]$Active, [string]$Crumbs, [string]$Body,
        [switch]$Wide, [string]$Main = '', [switch]$NoTop)
  $html = @"
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>$Title &middot; Swan Content Publisher</title>
<link rel="icon" href="assets/emqube-logo-black.svg">
<link rel="stylesheet" href="assets/tokens.css">
<link rel="stylesheet" href="assets/app.css">
</head>
<body>
<div class="app">
  <nav class="nav" aria-label="Main">
    <div class="nav-brand">
      <img src="assets/emqube-logo-white.svg" alt="emQube">
      <div>
        <div class="nav-swan">Swan</div>
      </div>
    </div>
$(NavHtml $Active)
    <div class="nav-foot">Prototype &middot; mock data<br>Brand: emqube.com</div>
  </nav>
  <div>
$(TopHtml)
    <main$(if($Wide){' class="wide"'})>
$(if ($Crumbs) { '      <div class="crumbs">' + $Crumbs + '</div>' })
$(if ($Main)  { $Main } else { '      ' + $Body })
    </main>
  </div>
</div>
</body>
</html>
"@
  Set-Content -LiteralPath (Join-Path $Out $File) -Value $html -Encoding UTF8
  "  $File"
}

function Bare {
  param([string]$File, [string]$Title, [string]$Body, [string]$BodyClass = '')
  $html = @"
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>$Title &middot; Swan Content Publisher</title>
<link rel="icon" href="assets/emqube-logo-black.svg">
<link rel="stylesheet" href="assets/tokens.css">
<link rel="stylesheet" href="assets/app.css">
</head>
<body$(if($BodyClass){' class="' + $BodyClass + '"'})>
$Body
</body>
</html>
"@
  Set-Content -LiteralPath (Join-Path $Out $File) -Value $html -Encoding UTF8
  "  $File"
}

Write-Host 'Generating prototype ->' $Out
New-Item -ItemType Directory -Path (Join-Path $Out 'assets') -Force | Out-Null
. (Join-Path $PSScriptRoot 'pages.ps1')
. (Join-Path $PSScriptRoot 'pages-part2.ps1')
. (Join-Path $PSScriptRoot 'pages-part3.ps1')
. (Join-Path $PSScriptRoot 'pages-part4.ps1')
Write-Host 'Done.'
