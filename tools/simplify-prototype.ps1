<#
  simplify-prototype.ps1 — generate prototype-simple/ from prototype/.

  The original prototype (prototype/) is a faithful planning artefact: every
  page explains its own logic ("decision A is still open, so..."), carries
  advisory banners, production notes and secondary actions. That is useful in
  a review meeting and noisy for a demo to a writer.

  prototype-simple/ is the same journey with the instructions, advisories and
  "why we built it this way" copy removed, and with the lowest-value buttons
  gone. Nothing functional changes: every page, link target and fixture number
  is identical.

  Every rule below is deliberate and documented in
  prototype-simple/README.md. To add a new page to prototype/, just re-run:

      powershell -File tools\simplify-prototype.ps1

  Rules, in execution order (regex, Singleline, applied per file):

    note-div        <div class="note">…</div>        production notes, policy asides
    footnote-p      <p class="footnote">…</p>        explanatory "why both are shown"
    hint-p          <p class="hint">…</p>            advisory hints
    alert-warn      <div class="alert alert-warn">… advisory warning banners
    decision-p      <p>…</p> containing "decision X"   open-decision explanations
    advisory-li     the "Advisory" row in the failure-triage legend
    advisory-row    "See decision H." trailing sentence in the triage table
    checks-explain  <div class="d"> under a checks <div class="t"> (documents-new only)
    split-actions   Rename/Drop/Move-boundary buttons on the split review page
    nav-swan        <div class="nav-swan">Swan</div> top-bar wordmark
#>
param([switch]$Quiet)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src  = Join-Path $root 'prototype'
$dst  = Join-Path $root 'prototype-simple'
if (-not (Test-Path $src)) { throw "prototype not found at $src" }

# Keep the generated-folder README (hand-edited) across rebuilds.
$readme = Join-Path $dst 'README.md'
$readmeTmp = Join-Path $env:TEMP 'swansimple-README.md'
if (Test-Path $readme) { Copy-Item $readme $readmeTmp -Force }

if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
Copy-Item $src $dst -Recurse
if (Test-Path $readmeTmp) { Copy-Item $readmeTmp $readme -Force; Remove-Item $readmeTmp }

# --- global rules, applied to every html file -------------------------------
$globalRules = @(
  @{ Name = 'note-div';       Pattern = '(?s)<div class="note"[^>]*>.*?</div>';                    Replace = '' },
  @{ Name = 'footnote-p';     Pattern = '(?s)<p class="footnote">.*?</p>';                          Replace = '' },
  @{ Name = 'hint-p';         Pattern = '(?s)<p class="hint"[^>]*>.*?</p>';                         Replace = '' },
  @{ Name = 'decision-p';     Pattern = '(?si)<p(?:\s[^>]*)?>(?:(?!</p>)[\s\S])*?decision [A-Z](?:(?!</p>)[\s\S])*?</p>'; Replace = '' },
  @{ Name = 'advisory-li';    Pattern = '(?s)<li><span class="ico info">(?:(?!</li>)[\s\S])*?</li>'; Replace = '' },
  @{ Name = 'advisory-row';   Pattern = ' See decision H\.';                                         Replace = '' },
  @{ Name = 'advisory-d';     Pattern = '(?s)<div class="d">Advisory only.*?</div>';               Replace = '' },
  @{ Name = 'nav-swan';       Pattern = '\s*<div class="nav-swan">Swan</div>';                        Replace = '' }
)

# Advisory warning banners are stripped on every page EXCEPT first-run.html,
# where the warn block carries a real "site not connected" status, not advice.
$alertRule = @{ Name = 'alert-warn'; Pattern = '(?s)<div class="alert alert-warn"[^>]*>.*?</p>\s*</div>\s*</div>'; Replace = '' }

Get-ChildItem $dst -Filter *.html | ForEach-Object {
  $text = Get-Content $_.FullName -Raw
  foreach ($r in $globalRules) {
    $text = [regex]::Replace($text, $r.Pattern, $r.Replace)
  }
  if ($_.Name -ne 'first-run.html') {
    $text = [regex]::Replace($text, $alertRule.Pattern, $alertRule.Replace)
  }
  Set-Content $_.FullName -Value $text -NoNewline -Encoding UTF8
  if (-not $Quiet) { Write-Host "processed $($_.Name)" }
}

# --- page-specific rules ----------------------------------------------------

# documents-new.html: keep the check titles, drop the paragraphs that justify them
$dn = Join-Path $dst 'documents-new.html'
$t = Get-Content $dn -Raw
$t = [regex]::Replace($t, '(?s)<div class="t">(.*?)</div>\s*<div class="d">.*?</div>', '<div class="t">$1</div>')
Set-Content $dn -Value $t -NoNewline -Encoding UTF8
if (-not $Quiet) { Write-Host 'processed documents-new.html (checks-explain)' }

# document-split.html: remove the low-value boundary-editing actions
$ds = Join-Path $dst 'document-split.html'
$t = Get-Content $ds -Raw
foreach ($frag in @(
  '<button class="btn btn-sm btn-ghost" title="Rename">Rename</button>',
  '<button class="btn btn-sm btn-ghost" title="Drop this article">Drop</button>',
  '<button class="btn btn-sm">Move boundary up</button>',
  '<button class="btn btn-sm">Move boundary down</button>'
)) { $t = $t.Replace($frag, '') }
# empty row-tight shells left behind by the button removal
$t = [regex]::Replace($t, '(?s)<div class="row-tight"[^>]*>\s*</div>', '')
Set-Content $ds -Value $t -NoNewline -Encoding UTF8
if (-not $Quiet) { Write-Host 'processed document-split.html (split-actions)' }

if (-not $Quiet) { Write-Host "`nprototype-simple written to $dst" }
