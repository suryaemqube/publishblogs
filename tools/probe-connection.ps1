<#
  probe-connection.ps1
  Swan Content Publisher - authenticated WordPress connection probe.

  This is the pre-build version of the `/api/sites/{id}/test-connection` endpoint
  (PLAN-v2.md sec 8.4). It exists to answer the two decisions that public,
  unauthenticated probing could not settle:

      I  electricway   - is `category` or `insights_category` the authoritative taxonomy?
      J  swansol       - the `blog` CPT has no registered taxonomy. Where are categories?
         allaboutvat   - the `announcements` CPT has no registered taxonomy. Same question.

  It also re-verifies emoversuae after the client deploys `show_in_rest => true`.

  Credentials come from environment variables or a secure prompt. Nothing is written
  to disk, and the password never appears in output.

  USAGE
    .\probe-connection.ps1 -Site electricway
    .\probe-connection.ps1 -Site all -Verbose
    $env:SWAN_WP_USER='swan-admin'; $env:SWAN_WP_APP_PWD='xxxx xxxx ...'; .\probe-connection.ps1 -Site electricway

  Exit code 0 = all probes passed. 1 = at least one blocking failure.
#>

[CmdletBinding()]
param(
  [string[]]$Site = @('emoversuae', 'electricway', 'swansol', 'allaboutvat', 'emqube'),
  [switch]$SkipWriteProbe,   # skip the throwaway-draft create/delete
  [switch]$Json              # emit JSON only
)

$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'

# Windows PowerShell 5.1 uses .NET Framework's ServicePointManager, which defaults to
# Expect100Continue = true and a 2-connection limit. Against WordPress origins behind a
# CDN this makes Invoke-WebRequest fail with "The request was aborted: The connection was
# closed unexpectedly" on the SECOND request onward, which looks exactly like a 404 or a
# site being down. Measured against these five hosts: without these three lines, probes
# 2+ on emqube, allaboutvat and emoversuae returned false ERR/404 results; with them the
# same requests return 200. Do not remove.
[System.Net.ServicePointManager]::Expect100Continue  = $false
[System.Net.ServicePointManager]::DefaultConnectionLimit = 64
[System.Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# ---------------------------------------------------------------- site registry
# Mirrors the verified inventory in PLAN-v2.md sec 0.1.A. The REST origin is the
# WordPress host, NOT the client's public domain - that is the single most common
# mistake this probe exists to prevent.
$registry = @{
  'emqube'      = @{ Origin='https://wp.emqube.com';      Public='https://emqube.com';      PostType='blogs';         RestBase='blogs';         Taxonomies=@('blog_cat') }
  'swansol'     = @{ Origin='https://app.swansol.com';     Public='https://swansol.com';     PostType='blog';          RestBase='blog';          Taxonomies=@() }
  'allaboutvat' = @{ Origin='https://www.allaboutvat.com'; Public='https://www.allaboutvat.com'; PostType='announcements'; RestBase='announcements'; Taxonomies=@() }
  'electricway' = @{ Origin='https://electricway.com';     Public='https://electricway.com';  PostType='insights';      RestBase='insights';      Taxonomies=@('category','insights_category') }
  'emoversuae'  = @{ Origin='https://emoversuae.com';      Public='https://emoversuae.com';   PostType='move-on';       RestBase='move-on';       Taxonomies=@('blog') }
}

# ---------------------------------------------------------------- credentials
function Get-WpCredential {
  param([string]$SiteName)
  $user = $env:SWAN_WP_USER
  $pass = $env:SWAN_WP_APP_PWD
  if ([string]::IsNullOrWhiteSpace($user)) {
    $user = Read-Host "WordPress username for $SiteName"
  }
  if ([string]::IsNullOrWhiteSpace($pass)) {
    # Read-Host -AsSecureString keeps it off the screen; convert for the Basic header
    # in memory only. Never persisted, never echoed.
    $sec = Read-Host "Application Password for $SiteName" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
    try { $pass = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
  }
  return @{ User = $user; Pass = $pass }
}

function Get-WpAuthHeader {
  param([hashtable]$Cred)
  $pair = "$($Cred.User):$($Cred.Pass)"
  $b64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($pair))
  return @{ Authorization = "Basic $b64" }
}

# ---------------------------------------------------------------- http
# WordPress signals "rest_no_route" as a 404 with a JSON body, not a connection
# error, so we always parse the body before judging the result.
function Invoke-Wp {
  param(
    [string]$Uri, [string]$Method = 'GET', [hashtable]$Headers = @{},
    $Body = $null, [switch]$IncludeStatus, [int]$Retries = 3
  )
  $p = @{
    Uri             = $Uri
    Method          = $Method
    Headers         = $Headers
    UseBasicParsing = $true
    TimeoutSec      = 30
    ErrorAction     = 'Stop'
  }
  if ($Body) { $p['Body'] = $Body; $p['ContentType'] = 'application/json; charset=utf-8' }

  $lastErr = $null
  for ($attempt = 1; $attempt -le $Retries; $attempt++) {
    try {
      $r = Invoke-WebRequest @p
      $json = $null
      try { $json = $r.Content | ConvertFrom-Json } catch { $json = $null }
      return [pscustomobject]@{
        Ok       = $true
        Status   = [int]$r.StatusCode
        Headers  = $r.Headers
        Raw      = $r.Content
        Json     = $json
        Attempts = $attempt
      }
    } catch {
      $resp = $_.Exception.Response
      # A real HTTP status is a definitive answer - only retry transport-level failures.
      if ($null -ne $resp) {
        $status = [int]$resp.StatusCode
        $raw = $null
        try {
          $sr = New-Object IO.StreamReader($resp.GetResponseStream())
          $raw = $sr.ReadToEnd(); $sr.Close()
        } catch { }
        $json = $null
        if ($raw) { try { $json = $raw | ConvertFrom-Json } catch { } }
        return [pscustomobject]@{
          Ok = ($status -ge 200 -and $status -lt 300); Status = $status
          Headers = @{}; Raw = $raw; Json = $json; Attempts = $attempt
          Error = $(if ($json -and $json.code) { "$($json.code): $($json.message)" } else { "HTTP $status" })
        }
      }
      $lastErr = $_.Exception.Message
      Start-Sleep -Milliseconds (800 * $attempt)
    }
  }
  return [pscustomobject]@{ Ok=$false; Status=0; Headers=@{}; Raw=$null; Json=$null; Attempts=$Retries; Error=$lastErr }
}

# ---------------------------------------------------------------- probes
function Test-RestRoot {
  param([hashtable]$Cfg, [hashtable]$Headers)
  $r = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/" -Headers $Headers
  $names = @()
  if ($r.Json -and $r.Json.namespaces) { $names = @($r.Json.namespaces) }
  return [pscustomobject]@{
    Name = 'REST root';            Pass = $r.Ok
    Detail = $(if ($r.Ok) { "namespaces: $($names -join ', ')" } else { $r.Error })
  }
}

function Test-Auth {
  param([hashtable]$Cfg, [hashtable]$Headers)
  $r = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/users/me?context=edit" -Headers $Headers
  if (-not $r.Ok) { return [pscustomobject]@{ Name='Authentication'; Pass=$false; Detail=$r.Error } }
  $caps = @()
  if ($r.Json.capabilities) { $caps = @($r.Json.capabilities.PSObject.Properties | Where-Object { $_.Value -eq $true } | ForEach-Object { $_.Name }) }
  $canEdit = $r.Json.capabilities.edit_posts
  return [pscustomobject]@{
    Name='Authentication'; Pass=$true
    Detail = "user '$($r.Json.name)' (id $($r.Json.id)) - edit_posts=$canEdit, $(($caps | Where-Object { $_ -match 'edit_published|manage_categories|edit_others' }) -join ', ')"
  }
}

# The check that would have caught emoversuae before an adapter was written.
function Test-PostTypeExposure {
  param([hashtable]$Cfg, [hashtable]$Headers)
  $r = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/types/$($Cfg.PostType)" -Headers $Headers
  if ($r.Ok -and $r.Json) {
    return [pscustomobject]@{
      Name='Post type in REST'; Pass=$true
      Detail = "'$($r.Json.slug)' / rest_base '$($r.Json.rest_base)' - labels: $($r.Json.name)"
    }
  }
  return [pscustomobject]@{
    Name='Post type in REST'; Pass=$false
    Detail = "'$($Cfg.PostType)' not exposed ($($r.Status) $($r.Error)). Client must set show_in_rest => true."
  }
}

function Test-TaxonomyExposure {
  param([hashtable]$Cfg, [hashtable]$Headers)
  $results = @()
  foreach ($tax in $Cfg.Taxonomies) {
    $r = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/taxonomies/$tax" -Headers $Headers
    $results += [pscustomobject]@{
      Name = "Taxonomy '$tax'"; Pass = $r.Ok
      Detail = $(if ($r.Ok) { "rest_base '$($r.Json.rest_base)' - $($r.Json.name)" } else { "not exposed ($($r.Status)). Client must set show_in_rest => true." })
    }
  }
  if ($Cfg.Taxonomies.Count -eq 0) {
    $results += [pscustomobject]@{
      Name='Taxonomies'; Pass=$true
      Detail='none registered on this CPT (expected) - category location comes from the field probe'
    }
  }
  return $results
}

# Decision I. Both `category` and `insights_category` are attached to the `insights`
# post type, so "which is registered" cannot answer it. The only question that matters
# is which one the editors actually use, so this samples real published posts and counts
# how many carry terms in each. A 20/20 vs 0/20 split is not a judgement call.
function Get-ElectricwayTaxonomyEvidence {
  param([hashtable]$Cfg, [hashtable]$Headers)

  $sampleSize = 20
  $attached = @()
  $t = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/types/$($Cfg.PostType)" -Headers $Headers
  if ($t.Ok -and $t.Json -and $t.Json.taxonomies) { $attached = @($t.Json.taxonomies) }

  $e = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/$($Cfg.RestBase)?per_page=$sampleSize" -Headers $Headers
  $posts = @()
  if ($e.Ok -and $e.Json) { $posts = @($e.Json) }

  # rest_base is usually the plural, and is the only correct term route.
  $restBase = @{}
  $tr = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/taxonomies" -Headers $Headers
  if ($tr.Ok -and $tr.Json) {
    foreach ($prop in $tr.Json.PSObject.Properties) { $restBase[$prop.Name] = $prop.Value.rest_base }
  }

  $evidence = @()
  foreach ($tax in $Cfg.Taxonomies) {
    $row = [ordered]@{
      Taxonomy       = $tax
      RestBase       = $(if ($restBase.ContainsKey($tax)) { $restBase[$tax] } else { $tax })
      Attached       = ($attached -contains $tax)
      PayloadKey     = '-'
      PostsWithTerms = 0
      PostsSampled   = $posts.Count
      TermCount      = 'n/a'
    }
    if ($attached.Count -gt 0 -and -not $row.Attached) {
      $row['PostsWithTerms'] = 0
    } else {
      foreach ($p in $posts) {
        $names = @($p.PSObject.Properties.Name)
        # `category` arrives as `categories` (y -> ies, not y -> ys);
        # `insights_category` arrives as itself. Try every plausible shape.
        $candidates = @($tax, "${tax}s", ($tax -replace 'y$', 'ies'), ($tax -replace '_category$', 's'))
        $key = $null
        foreach ($c in $candidates) { if ($c -and $names -contains $c) { $key = $c; break } }
        if ($key) {
          $row['PayloadKey'] = $key
          $v = $p.$key
          if ($null -ne $v -and @($v).Count -gt 0) { $row['PostsWithTerms'] = $row['PostsWithTerms'] + 1 }
        }
      }
    }
    $c = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/$($row['RestBase'])?per_page=1" -Headers $Headers
    if ($c.Ok) { $row['TermCount'] = $c.Headers['X-WP-Total'] }
    $evidence += [pscustomobject]$row
  }

  $used      = @($evidence | Where-Object { $_.PostsWithTerms -gt 0 })
  $unused    = @($evidence | Where-Object { $_.PostsWithTerms -eq 0 })
  $contested = ($posts.Count -gt 0) -and ($used.Count -gt 0) -and ($unused.Count -gt 0) -and `
               ($used[0].PostsWithTerms -lt $posts.Count)

  $verdict = 'UNDECIDED - no published posts to sample'
  if ($used.Count -eq 1 -and $unused.Count -ge 1) {
    $u = $used[0]
    $verdict = "DECISION I ANSWERED: '$($u.Taxonomy)' is authoritative - $($u.PostsWithTerms)/$($u.PostsSampled) sampled posts carry terms; '$((@($unused | ForEach-Object { $_.Taxonomy })) -join ', ')' is attached but empty. Sync to '$($u.Taxonomy)' only."
  } elseif ($used.Count -eq 0) {
    $verdict = 'UNDECIDED - no sampled post carries terms in either taxonomy.'
  } elseif ($contested) {
    $verdict = 'AMBIGUOUS - both carry terms but not on every post. A human who knows the theme must confirm.'
  } else {
    $verdict = 'AMBIGUOUS - both taxonomies are in active use. A human who knows the theme must confirm.'
  }

  return [pscustomobject]@{ Attached=$attached; SampleSize=$posts.Count; Evidence=$evidence; Verdict=$verdict }
}

# Decision J. The CPT exposes no taxonomy, so categories must be a field. This
# dumps every key WordPress returns and flags the ones that look like one, and
# also reads the public HTML nav, whose category base usually reveals the truth
# even when the field is not REST-exposed.
function Get-CategoryFieldEvidence {
  param([hashtable]$Cfg, [hashtable]$Headers)

  $attached = @()
  $t = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/types/$($Cfg.PostType)" -Headers $Headers
  if ($t.Ok -and $t.Json -and $t.Json.taxonomies) { $attached = @($t.Json.taxonomies) }

  $keys = @(); $acfKeys = @(); $metaKeys = @(); $hasAcfKey = $false
  $usedContext = 'edit'
  # context=edit is what reveals ACF values, but it 401s without credentials. Fall back
  # so the unauthenticated run still reports what it can, rather than nothing.
  $r = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/$($Cfg.RestBase)?per_page=5&context=edit" -Headers $Headers
  if (-not $r.Ok) {
    $usedContext = 'view'
    $r = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/$($Cfg.RestBase)?per_page=5" -Headers $Headers
  }
  if ($r.Ok -and $r.Json -and @($r.Json).Count -gt 0) {
    $keys = @($r.Json[0].PSObject.Properties.Name | Sort-Object)
    $hasAcfKey = ($keys -contains 'acf')
    $acfKeys = @($keys | Where-Object { $_ -notmatch '^(id|date|date_gmt|guid|modified|modified_gmt|slug|status|type|link|title|content|excerpt|author|featured_media|comment_status|ping_status|sticky|template|format|meta|categories|tags|class_list|acf|yoast_head|yoast_head_json|_links|_embedded)$' })
    if ($r.Json[0].acf)    { $acfKeys = @($acfKeys + @($r.Json[0].acf.PSObject.Properties.Name)) }
    if ($r.Json[0].meta)  { $metaKeys = @($r.Json[0].meta.PSObject.Properties.Name | Sort-Object) }
  }

  # `_acf_changed` is written by ACF itself. Its presence proves a field group is
  # attached to this post type even when none of the field values are exposed.
  $acfInstalled = ($metaKeys -contains '_acf_changed')

  $a = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/$($Cfg.RestBase)?per_page=1&context=edit&acf_format=standard" -Headers $Headers
  $acfRestExposed = $false
  if ($a.Ok -and $a.Json -and @($a.Json).Count -gt 0) {
    $acfRestExposed = @($a.Json[0].PSObject.Properties.Name | Where-Object { $_ -notmatch '^(acf|_links|_embedded)$' }).Count -gt 0
  }
  $htmlBases = @()
  $h = Invoke-Wp -Uri "$($Cfg.Public)/" -IncludeStatus
  if ($h.Ok -and $h.Raw) {
    $htmlBases = @([regex]::Matches($h.Raw, '/(?:category|cat|topics?|blog_cat|insights_category|announcements|news)/([a-z0-9\-]+)') |
      ForEach-Object { $_.Groups[0].Value } | Sort-Object -Unique)
  }

  $categoryish = @($acfKeys | Where-Object { $_ -match 'categor|topic|section|industry|vertical|sector' })

  $verdict = 'UNDECIDED'
  if ($categoryish.Count -gt 0) {
    $verdict = "DECISION J LIKELY ANSWERED: candidate field(s) - $($categoryish -join ', '). Confirm each field's type before building the adapter."
  } elseif ($acfRestExposed -and $acfKeys.Count -gt 0) {
    $verdict = "PARTIAL: REST exposes non-core keys but none look like a category: $($acfKeys -join ', '). Inspect the ACF field group in wp-admin."
  } elseif ($acfInstalled -or $hasAcfKey) {
    $why = if ($acfInstalled) { "_acf_changed meta is present, so a field group is attached" } else { "an `acf` key is present in the payload" }
    $verdict = "BLOCKED: no taxonomy, and no field values exposed - $why but the values are withheld. Needs either an authenticated request with the field's show_in_rest enabled, or wp-admin access. Swan cannot populate a category picker on this site as it stands."
  } elseif ($htmlBases.Count -gt 0) {
    $verdict = "PARTIAL: no REST exposure, but the public nav uses $($htmlBases -join ', '). That names the term base - confirm server-side before relying on it."
  } else {
    $verdict = 'BLOCKED: no taxonomy, no exposed field, no nav signal. Requires wp-admin access or a temporary show_in_rest diagnostic.'
  }

  return [pscustomobject]@{
    AttachedTaxonomies = $attached
    NonCoreKeys       = $acfKeys
    MetaKeys          = $metaKeys
    AcfInstalled      = $acfInstalled
    AcfRestExposed    = $acfRestExposed
    UsedContext       = $usedContext
    Categoryish       = $categoryish
    PublicNavBases    = $htmlBases
    Verdict           = $verdict
  }
}

# Optional: prove the credentials can actually write, not just read. This is the
# check that fails when ACF has required fields the adapter has not been told about.
function Test-WriteAccess {
  param([hashtable]$Cfg, [hashtable]$Headers)
  $payload = @{
    title   = 'Swan connection probe - safe to delete'
    status  = 'draft'
    content = 'Created by Swan Content Publisher connection probe. Delete me.'
    excerpt = 'probe'
  } | ConvertTo-Json
  $c = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/$($Cfg.RestBase)" -Method POST -Headers $Headers -Body $payload
  if (-not $c.Ok -or -not $c.Json.id) {
    return [pscustomobject]@{ Name='Write access'; Pass=$false; Detail="draft create failed - $($c.Error). Check required ACF fields and the user's capability." }
  }
  $id = $c.Json.id
  $d = Invoke-Wp -Uri "$($Cfg.Origin)/wp-json/wp/v2/$($Cfg.RestBase)/$id?force=true" -Method DELETE -Headers $Headers
  return [pscustomobject]@{
    Name='Write access'; Pass=$true
    Detail=$(if ($d.Ok) { "created and deleted draft #$id cleanly" } else { "created draft #$id but DELETE returned $($d.Status) - remove it manually" })
  }
}

# ---------------------------------------------------------------- main
$report = @{}

foreach ($name in $Site) {
  if (-not $registry.ContainsKey($name)) {
    Write-Warning "Unknown site '$name'. Known: $($registry.Keys -join ', ')"
    continue
  }
  $cfg = $registry[$name]
  if ($Json) { } else { Write-Host '' ; Write-Host "=== $name  ($($cfg.Origin)) ===" -ForegroundColor Cyan }

  $cred = Get-WpCredential -SiteName $name
  $hdr  = Get-WpAuthHeader -Cred $cred
  $checks = @()
  $checks += Test-RestRoot        -Cfg $cfg -Headers $hdr
  $checks += Test-Auth            -Cfg $cfg -Headers $hdr
  $checks += Test-PostTypeExposure -Cfg $cfg -Headers $hdr
  $checks += Test-TaxonomyExposure -Cfg $cfg -Headers $hdr
  if (-not $SkipWriteProbe) { $checks += Test-WriteAccess -Cfg $cfg -Headers $hdr }

  $findings = $null
  if ($name -eq 'electricway') { $findings = Get-ElectricwayTaxonomyEvidence -Cfg $cfg -Headers $hdr }
  elseif ($cfg.Taxonomies.Count -eq 0) { $findings = Get-CategoryFieldEvidence -Cfg $cfg -Headers $hdr }

  if (-not $Json) {
    foreach ($c in $checks) {
      $tag = if ($c.Pass) { 'PASS' } else { 'FAIL' }
      $col = if ($c.Pass) { 'Green' } else { 'Red' }
      Write-Host ("  [{0}] {1,-26} {2}" -f $tag, $c.Name, $c.Detail) -ForegroundColor $col
    }
    if ($findings) {
      Write-Host ''
      if ($findings.PSObject.Properties.Name -contains 'Evidence') {
        "attached to the CPT : $($findings.Attached -join ', ')"
        "sampled posts        : $($findings.SampleSize)"
        foreach ($e in $findings.Evidence) {
          $bits = @($e.PSObject.Properties | ForEach-Object { "$($_.Key)=$($_.Value)" })
          Write-Host ("    {0,-20} {1}" -f $e.Taxonomy, ($bits -join '  '))
        }
      } else {
        Write-Host "    attached taxonomies: $(if (@($findings.AttachedTaxonomies).Count) { $findings.AttachedTaxonomies -join ', ' } else { 'NONE' })"
        Write-Host "    non-core keys     : $(if ($findings.NonCoreKeys.Count) { $findings.NonCoreKeys -join ', ' } else { 'none' })"
        Write-Host "    meta keys         : $(if ($findings.MetaKeys.Count) { $findings.MetaKeys -join ', ' } else { 'none' })"
        Write-Host "    acf installed     : $($findings.AcfInstalled)   exposed in REST: $($findings.AcfRestExposed)"
        Write-Host "    public nav bases  : $(if ($findings.PublicNavBases.Count) { $findings.PublicNavBases -join ', ' } else { 'none found' })"
      }
      Write-Host ''
      Write-Host "    $($findings.Verdict)" -ForegroundColor Yellow
    }
  }

  $report[$name] = [pscustomobject]@{
    Origin = $cfg.Origin; PostType = $cfg.PostType; RestBase = $cfg.RestBase
    Checks = $checks
    AllPassed = -not ($checks | Where-Object { -not $_.Pass })
    Findings = $findings
  }
  # Zero the credential as soon as this site is done.
  $cred.Pass = $null
}

if ($Json) {
  $report | ConvertTo-Json -Depth 8
} else {
  Write-Host ''
  Write-Host '=== summary ===' -ForegroundColor Cyan
  foreach ($k in $report.Keys) {
    $state = if ($report[$k].AllPassed) { 'ok' } else { 'FAILED' }
    Write-Host ("  {0,-12} {1}" -f $k, $state)
  }
  $blocked = @($report.Keys | Where-Object { $report[$_].Findings -and $report[$_].Findings.Verdict -like 'BLOCKED*' })
  if ($blocked.Count) {
    Write-Host ''
    Write-Host "  Still blocked, needs wp-admin: $($blocked -join ', ')" -ForegroundColor Yellow
  }
}

if ($report.Values | Where-Object { -not $_.AllPassed }) { exit 1 } else { exit 0 }
