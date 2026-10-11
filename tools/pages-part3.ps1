
# ================================================================= part 3
# SEO, approval, publication, preview, triage, and the supporting pages

# ---------------------------------------------------------------- 12. SEO + image
Page 'article-seo.html' 'SEO and image' 'articles.html' '<a href="articles.html">Articles</a><span class="sep">/</span>What is Relocation Stress Syndrome<span class="sep">/</span>SEO &amp; image' @"
<div class="phead">
  <div class="grow"><h1>SEO and featured image</h1>
  <p>Article 2 of 4. These are the only fields an author fills in that do not come from the document.</p></div>
  <div class="phead-acts">
    <a class="btn" href="article-content.html">Back to content</a>
    <a class="btn btn-primary" href="review.html">Send for approval</a>
  </div>
</div>

<div class="split">
  <div>
    <div class="card">
      <div class="card-h"><h2>Search appearance</h2>
        <span class="chip chip-warn" style="margin-left:auto">1 of 2 checks need attention</span></div>
      <div class="card-b">
        <div class="field">
          <label for="seo-title">SEO title <span class="counter">58 / 60</span></label>
          <input class="input" type="text" id="seo-title" value="What is Relocation Stress Syndrome? | E-Movers">
          <p class="hint">Falls back to the article title when left empty, but the title here is 76 characters
          and will be truncated in search results.</p>
        </div>
        <div class="field">
          <label for="seo-desc">Meta description <span class="counter">142 / 160</span></label>
          <textarea class="textarea" id="seo-desc">Moving house triggers a measurable physical and emotional response. Here is what relocation stress syndrome is, why it happens, and how to avoid it.</textarea>
        </div>
        <div class="field">
          <label for="seo-slug">URL slug</label>
          <input class="input over" type="text" id="seo-slug" value="what-is-relocation-stress-syndrome-why-moving-feels-so-overwhelming">
          <p class="hint" style="color:var(--danger)">148 characters. Most sites truncate around 60-80, and the
          tail will be cut mid-word. Suggested: <span class="mono">relocation-stress-syndrome</span></p>
        </div>
        <div class="grid-2">
          <div class="field">
            <label for="seo-fb">Focus keyword</label>
            <input class="input" type="text" id="seo-fb" value="relocation stress syndrome">
          </div>
          <div class="field">
            <label for="seo-cat">Category</label>
            <select class="select" id="seo-cat">
              <option>Health &amp; Wellness</option><option>Real Estate</option>
              <option>Thought Leadership</option><option>Trust &amp; Reputation</option>
            </select>
            <p class="hint">Read from the target site's own taxonomy, so it cannot save a term that does not exist there.</p>
          </div>
        </div>
      </div>
    </div>

    <div class="card" style="margin-top:16px">
      <div class="card-h"><h2>Featured image</h2></div>
      <div class="card-b">
        <div class="drop" style="padding:32px">
          $(Ico 'image' 34)
          <h3>No image set</h3>
          <p>Articles need a featured image on every site. Upload once and Swan copies it to each target.</p>
          <a class="btn btn-primary" href="#">Choose image</a>
          <div class="or">JPG or PNG &middot; 1200&times;630 recommended &middot; alt text required</div>
        </div>
        <div class="note">Article 1 also has no featured image. You can set one here and a single image can be
        reused across articles if they genuinely share one.</div>
      </div>
    </div>
  </div>

  <div>
    <div class="card">
      <div class="card-h"><h3>Swan checks</h3></div>
      <ul class="checks">
        <li><span class="ico bad">$(Ico 'xcirc' 17)</span>
          <div><div class="t">SEO title missing</div><div class="d">Not required, but the 76-character
          article title will be truncated in search.</div></div></li>
        <li><span class="ico bad">$(Ico 'xcirc' 17)</span>
          <div><div class="t">No featured image</div><div class="d">Required before publishing to any site.</div></div></li>
        <li><span class="ico good">$(Ico 'okcirc' 17)</span>
          <div><div class="t">Meta description length</div><div class="d">142 characters, inside the 160 limit.</div></div></li>
        <li><span class="ico good">$(Ico 'okcirc' 17)</span>
          <div><div class="t">Category exists on target sites</div><div class="d">"Health &amp; Wellness" is
          present in every selected site's taxonomy.</div></div></li>
        <li><span class="ico warn">$(Ico 'alert' 17)</span>
          <div><div class="t">Slug too long</div><div class="d">Advisory only - Swan will shorten it and log
          the change rather than fail the deployment.</div></div></li>
      </ul>
      <div class="card-f">
        <a class="btn btn-primary" href="review.html">Continue to approval</a>
      </div>
    </div>
  </div>
</div>
"@

# ---------------------------------------------------------------- 13. approval
$rv = foreach ($r in $Reviewers) {
  $cls = if ($r.State -eq 'approved') { 'chip-ok' } else { 'chip-warn' }
  $lbl = if ($r.State -eq 'approved') { 'Approved' } else { 'Waiting' }
  $whn = if ($r.When) { '<div class="muted xs">' + $r.When + '</div>' } else { '<div class="muted xs">&mdash;</div>' }
  '          <tr><td><div class="row-tight"><span class="avatar">' + $r.Initials + '</span>' +
  '<div><div class="strong">' + $r.Name + '</div><div class="muted xs">' + $r.Role + '</div></div></div></td>' +
  '<td><span class="chip ' + $cls + '"><span class="dot"></span>' + $lbl + '</span></td>' +
  '<td>' + $whn + '</td>' +
  '<td class="right">' + $(if ($r.State -eq 'approved') { '<button class="btn btn-sm btn-ghost">Withdraw</button>' } else { '<button class="btn btn-sm">Remind</button>' }) + '</td></tr>'
}
$rv = $rv -join "`n"

Page 'review.html' 'Review queue' 'review.html' '' @"
<div class="phead">
  <div class="grow"><h1>Review queue</h1>
  <p>Approvals are per article, not per document. A document can be half published and half still in review.</p></div>
  <div class="phead-acts"><a class="btn" href="articles.html">All articles</a></div>
</div>

<div class="split">
  <div>
    <div class="card">
      <div class="card-h">
        <h2>What is Relocation Stress Syndrome</h2>
        <span class="chip chip-info" style="margin-left:auto">2 of 3 approved</span>
      </div>
      <div class="card-b">
        <div class="alert alert-danger" style="margin-bottom:16px">
          $(Ico 'alert' 17)
          <div><h3>Cannot be approved yet</h3>
          <p>No featured image is set, and the SEO title is missing. Both are required before this can go to
          any site. The reviewer can still read it and leave comments.</p></div>
        </div>
        <div class="row wrap" style="gap:20px">
          <div><div class="k muted xs" style="text-transform:uppercase;letter-spacing:.06em">Word count</div><div class="strong">618</div></div>
          <div><div class="k muted xs" style="text-transform:uppercase;letter-spacing:.06em">H2s</div><div class="strong">3</div></div>
          <div><div class="k muted xs" style="text-transform:uppercase;letter-spacing:.06em">Internal links</div><div class="strong">9</div></div>
          <div><div class="k muted xs" style="text-transform:uppercase;letter-spacing:.06em">Outbound links</div><div class="strong">2</div></div>
          <div><div class="k muted xs" style="text-transform:uppercase;letter-spacing:.06em">Submitted</div><div class="strong">26 Sep</div></div>
        </div>
        <div class="row" style="margin-top:20px">
          <a class="btn" href="article-content.html">$(Ico 'eye') Read the article</a>
          <a class="btn" href="article-seo.html">$(Ico 'edit') Fix the blockers</a>
        </div>
      </div>
    </div>

    <div class="card" style="margin-top:16px">
      <div class="card-h"><h2>Modern Renting in Dubai</h2>
        <span class="chip chip-ok" style="margin-left:auto">$(Ico 'check' 12) Fully approved</span></div>
      <div class="card-b row wrap" style="gap:20px">
        <div><div class="k muted xs" style="text-transform:uppercase;letter-spacing:.06em">Word count</div><div class="strong">629</div></div>
        <div><div class="k muted xs" style="text-transform:uppercase;letter-spacing:.06em">H2s</div><div class="strong">4</div></div>
        <div><div class="k muted xs" style="text-transform:uppercase;letter-spacing:.06em">Sites chosen</div><div class="strong">2</div></div>
        <div class="grow"></div>
        <a class="btn btn-primary" href="publication-new.html">Publish this $(Ico 'arrowr')</a>
      </div>
    </div>
  </div>

  <div>
    <div class="card">
      <div class="card-h"><h3>Approvers</h3>
        <button class="btn btn-sm btn-ghost" style="margin-left:auto">Edit</button></div>
      <table class="tbl">
        <tbody>
$rv
        </tbody>
      </table>
      <div class="card-b" style="border-top:1px solid var(--border)">
        <p class="small muted" style="margin:0 0 10px">This client uses <strong>named approvers</strong>:
        all three must approve. The alternative - any team member can approve - is
        <span class="mono">decision G</span> and is still open, so the setting is shown per client.</p>
        <div class="row-tight">
          <span class="chip chip-neutral">Named approvers</span>
          <span class="chip chip-neutral">Any member</span>
        </div>
      </div>
    </div>

    <div class="card" style="margin-top:16px">
      <div class="card-b">
        <div class="row-tight" style="margin-bottom:8px">$(Ico 'shield' 16)<h3 style="font-size:15px">What an approval covers</h3></div>
        <p class="small muted" style="margin:0">Approving locks the article's content, SEO, and image. Any
        later edit invalidates the approval and returns the article to this queue, so nobody publishes
        something the reviewer did not read.</p>
      </div>
    </div>
  </div>
</div>
"@

# ---------------------------------------------------------------- 14. choose sites
Page 'publication-new.html' 'Choose sites' 'publications.html' '<a href="review.html">Review</a><span class="sep">/</span>Modern Renting in Dubai<span class="sep">/</span>Choose sites' @"
<div class="phead">
  <div class="grow"><h1>Choose sites for &ldquo;Modern Renting in Dubai&rdquo;</h1>
  <p>One article, many destinations. Each site gets its own publication, so a failure on one does not stop the others.</p></div>
  <div class="phead-acts"><a class="btn" href="preview.html">Preview first</a></div>
</div>

<div class="split">
  <div>
    <div class="card">
      <div class="card-h"><h2>E-Movers International</h2>
        <span class="chip chip-green" style="margin-left:auto">4 of 5 selected</span></div>
      <table class="tbl">
        <thead><tr><th style="width:36px"></th><th>Site</th><th>Type</th><th>Category exists</th><th>Status</th></tr></thead>
        <tbody>
          <tr><td><input type="checkbox" checked aria-label="emoversuae.com"></td>
              <td><span class="strong">emoversuae.com</span><div class="muted xs">move-on</div></td>
              <td><span class="chip chip-neutral">move-on</span></td>
              <td><span class="chip chip-ok"><span class="dot"></span>Yes</span></td>
              <td><span class="chip chip-ok"><span class="dot"></span>Ready</span></td></tr>
          <tr><td><input type="checkbox" checked aria-label="emoversrss.com"></td>
              <td><span class="strong">emoversrss.com</span><div class="muted xs">post</div></td>
              <td><span class="chip chip-neutral">post</span></td>
              <td><span class="chip chip-ok"><span class="dot"></span>Yes</span></td>
              <td><span class="chip chip-warn"><span class="dot"></span>1 past media failure</span></td></tr>
          <tr><td><input type="checkbox" checked aria-label="swanwomenswear.ae"></td>
              <td><span class="strong">swanwomenswear.ae</span><div class="muted xs">post</div></td>
              <td><span class="chip chip-neutral">post</span></td>
              <td><span class="chip chip-ok"><span class="dot"></span>Yes</span></td>
              <td><span class="chip chip-ok"><span class="dot"></span>Ready</span></td></tr>
          <tr><td><input type="checkbox" checked aria-label="ladypackers.ae"></td>
              <td><span class="strong">ladypackers.ae</span><div class="muted xs">post</div></td>
              <td><span class="chip chip-neutral">post</span></td>
              <td><span class="chip chip-danger"><span class="dot"></span>Unverifiable</span></td>
              <td><span class="chip chip-warn"><span class="dot"></span>Preflight warns</span></td></tr>
          <tr><td><input type="checkbox" disabled aria-label="emovers.com move-on">
              <td><span class="strong" style="opacity:.6">emovers.com</span><div class="muted xs">move-on</div></td>
              <td><span class="chip chip-neutral">move-on</span></td>
              <td><span class="chip chip-danger"><span class="dot"></span>No</span></td>
              <td><span class="chip chip-danger"><span class="dot"></span>Not connected</span></td></tr>
        </tbody>
      </table>
      <div class="card-b" style="border-top:1px solid var(--border)">
        <div class="alert alert-warn">
          $(Ico 'alert' 17)
          <div><h3>ladypackers.ae: category cannot be verified</h3>
          <p>Its <span class="mono">insights_category</span> taxonomy is not registered with
          <span class="mono">show_in_rest</span>, so Swan cannot confirm the term exists. This is
          <span class="mono">decision H</span>. You can still publish - Swan will assign the site default and
          log it - but it will show up in <a href="publishing-failures.html">failures</a>.</p></div>
        </div>
      </div>
      <div class="card-f">
        <span class="small muted">Publishing to <strong>4 sites</strong>. Each becomes a separate publication
        with its own deployment record.</span>
        <a class="btn btn-primary" href="preview.html" style="margin-left:auto">Preview $(Ico 'eye')</a>
        <a class="btn btn-dark" href="deployments.html">Publish now $(Ico 'send')</a>
      </div>
    </div>
  </div>

  <div>
    <div class="card">
      <div class="card-h"><h3>Publication settings</h3></div>
      <div class="card-b">
        <div class="field">
          <label for="pub-author">Author</label>
          <select class="select" id="pub-author"><option>Aisha Rahman</option><option>Marketing Team</option></select>
        </div>
        <div class="field">
          <label for="pub-date">Publish</label>
          <select class="select" id="pub-date">
            <option>Immediately</option>
            <option>Tomorrow 09:00</option>
            <option>Pick a date&hellip;</option>
          </select>
        </div>
        <div class="field">
          <label class="checkline"><input type="checkbox" checked> Update the existing post if one exists</label>
          <p class="hint">Swan matches on slug. Without this, a re-run creates a duplicate.</p>
        </div>
        <div class="field">
          <label class="checkline"><input type="checkbox" checked> Mark as sticky on the target site</label>
        </div>
        <div class="field" style="margin-bottom:0">
          <label class="checkline"><input type="checkbox"> Replace any existing featured image</label>
        </div>
      </div>
    </div>

    <div class="card" style="margin-top:16px">
      <div class="card-b">
        <h3 style="font-size:15px;margin-bottom:8px">The unit is the publication</h3>
        <p class="small muted" style="margin:0">Scheduling, preview, and publishing all attach to the
        publication, not the article. An article can be live on two sites and queued on a third, and each
        site keeps its own slug, date, and author.</p>
      </div>
    </div>
  </div>
</div>
"@

# ---------------------------------------------------------------- 15. preview
Page 'preview.html' 'Preview' 'publications.html' '<a href="publication-new.html">Choose sites</a><span class="sep">/</span>Preview' @"
<div class="phead">
  <div class="grow"><h1>Preview before publishing</h1>
  <p>Rendered with each site's own stylesheet, so this is closer to the live page than the editor is.</p></div>
  <div class="phead-acts">
    <a class="btn" href="publication-new.html">Back</a>
    <a class="btn btn-dark" href="deployments.html">Publish to 4 sites $(Ico 'send')</a>
  </div>
</div>

<div class="phead" style="margin-bottom:16px">
  <div class="row-tight grow">
    <span class="chip chip-neutral">Site</span>
    <span class="ctx">$(Ico 'globe') emoversuae.com</span>
    <span class="chip chip-neutral">Status</span>
    <span class="chip chip-warn"><span class="dot"></span>Draft</span>
    <span class="chip chip-neutral">Slug</span>
    <span class="mono small">modern-renting-in-dubai-flexi-rent</span>
  </div>
</div>

<div class="pv-frame">
  <div class="pv-bar">
    $(Ico 'globe' 14)
    <span class="pv-url">https://emoversuae.com/?p=10455&amp;preview=true</span>
    <span class="chip chip-warn" style="font-size:10px">Unsaved preview</span>
    <button class="btn btn-sm">$(Ico 'external' 12) Open in new tab</button>
  </div>
  <div class="pv-body">
    <div class="pv-tag">Real Estate</div>
    <h1 style="margin-top:10px">Modern Renting in Dubai: How Flexi Rent and Payment Rewards Are Easing the Relocation Journey</h1>
    <p class="muted small">Aisha Rahman &middot; 26 September 2026 &middot; 4 min read</p>
    <h2>1. The Dubai Land Department&rsquo;s Flexi Rent Initiative</h2>
    <p>Moving into a new home in Dubai is an exciting milestone, whether you are settling into a high-rise
    apartment or a spacious townhouse. Unfortunately, the rental landscape has long been one of the most
    frustrating parts of that move.</p>
    <p>The Dubai Land Department (DLD) introduced the Flexi Rent scheme in partnership with major real
    estate developers. The idea is straightforward and, for renters, long overdue.</p>
    <p>Key public benefits include:</p>
    <ul>
      <li><strong>Zero Monthly Markup:</strong> The total annual rent remains exactly the same whether paid
      in a single cheque or split across twelve monthly instalments.</li>
      <li><strong>Cash Flow Alignment:</strong> Paying monthly allows residents to line up living expenses
      directly with their salary cycle.</li>
      <li><strong>Card-Based Transactions:</strong> Flexi Rent supports digital payments via credit or debit
      cards, moving the UAE closer to how rent works elsewhere.</li>
    </ul>
    <h2>2. Unlocking Value Through Digital Rent Rewards</h2>
    <p>Alongside regulatory flexibility, digital platforms are redefining the actual mechanics of paying
    rent. Using digital payment platforms, tenants can route rent payments through credit cards - even if
    the landlord has not registered for it.</p>
    <p><a href="#">Partner with the relocation specialists at E-Movers</a> and see what a calm move actually
    looks like.</p>
    <p style="margin-top:24px"><a class="btn btn-primary" href="#">Request a Proposal</a></p>
  </div>
</div>

<div class="alert alert-info" style="margin-top:16px">
  $(Ico 'info' 17)
  <div><h3>Preview is per site, not global</h3>
  <p>Switching the site above re-renders with that site's template, its theme, and its own taxonomy terms.
  A category that exists on one site may not exist on another, and the preview will say so rather than
  showing a term that will 400 on publish.</p></div>
</div>
"@

# ---------------------------------------------------------------- 16. failures
$rows = foreach ($f in $Failures) {
  '          <tr><td><span class="strong">' + $f.Site + '</span>' +
  '<div class="muted xs mono">' + $f.Code + '</div></td>' +
  '<td><span class="chip chip-neutral">' + $f.Stage + '</span></td>' +
  '<td class="small">' + $f.Msg + '</td>' +
  '<td class="right"><div class="row-tight" style="justify-content:flex-end">' +
  '<a class="btn btn-sm" href="deployments.html">Log</a>' +
  '<button class="btn btn-sm btn-primary">Fix and retry</button></div></td></tr>'
}
$rows = $rows -join "`n"

Page 'publishing-failures.html' 'Publishing failures' 'publishing-failures.html' '' @"
<div class="phead">
  <div class="grow"><h1>Publishing failures</h1>
  <p>Four partial failures from the last seven days. None of them rolled back a site that had already
  succeeded.</p></div>
  <div class="phead-acts">
    <a class="btn" href="deployments.html">Deployment history</a>
    <button class="btn">Retry all safe</button>
  </div>
</div>

<div class="alert alert-info" style="margin-bottom:20px">
  $(Ico 'info' 17)
  <div><h3>These are partial failures, not rollbacks</h3>
  <p>Each row is one <strong>publication</strong> that did not finish. The other three sites in the same
  run went live normally. Swan never deletes a post that succeeded because a sibling failed, and it never
  reports a run as "published" when only part of it was.</p></div>
</div>

<div class="card">
  <table class="tbl">
    <thead><tr><th>Site</th><th>Stage</th><th>What happened</th><th class="right">Action</th></tr></thead>
    <tbody>
$rows
    </tbody>
  </table>
  <div class="card-f">
    <span class="small muted">The first row is the emoversuae authentication problem, not a content
    problem. It needs a new Application Password before any retry can work.</span>
  </div>
</div>

<div class="grid-2" style="margin-top:20px">
  <div class="card">
    <div class="card-h"><h3>How failures are grouped</h3></div>
    <div class="card-b">
      <ul class="checks">
        <li><span class="ico bad">$(Ico 'xcirc' 17)</span>
          <div><div class="t">Blocker</div><div class="d">Nothing was written. The site rejected us at the
          first call, so there is no half-created post to clean up.</div></div></li>
        <li><span class="ico warn">$(Ico 'alert' 17)</span>
          <div><div class="t">Partial</div><div class="d">The post exists but something is missing - an
          image, a term. The post ID is recorded so a retry can update rather than duplicate.</div></div></li>
        <li><span class="ico info">$(Ico 'info' 17)</span>
          <div><div class="t">Advisory</div><div class="d">The post is live and correct, but Swan could not
          verify something it wanted to. Recorded, not raised as an error.</div></div></li>
      </ul>
    </div>
  </div>
  <div class="card">
    <div class="card-h"><h3>Retry safety</h3></div>
    <div class="card-b">
      <p class="small" style="margin-top:0">Retries are matched on slug, so retrying a partial failure
      updates the existing post instead of creating a second one. A retry that would create a duplicate
      asks first.</p>
      <p class="small muted" style="margin:0">Rows that failed at authentication cannot be retried until
      someone stores a new Application Password for that site - Swan will not keep trying a credential it
      already knows is dead.</p>
    </div>
  </div>
</div>
"@

# ---------------------------------------------------------------- 17. deployments
Page 'deployments.html' 'Deployments' 'publications.html' '' @"
<div class="phead">
  <div class="grow"><h1>Deployment</h1>
  <p>Modern Renting in Dubai &middot; started 26 Sep 2026 14:02 by Aisha Rahman</p></div>
  <div class="phead-acts">
    <a class="btn" href="publishing-failures.html">$(Ico 'alert') 1 failure</a>
    <a class="btn btn-primary" href="publications.html">All publications</a>
  </div>
</div>

<div class="split">
  <div>
    <div class="card">
      <div class="card-h"><h2>Stages</h2><span class="chip chip-ok" style="margin-left:auto">4 of 5 sites live</span></div>
      <div class="card-b">
        <ul class="stages">
          <li class="stage ok"><span class="dot">$(Ico 'check' 13)</span>
            <div><div class="t">Preflight</div>
            <div class="d">Checked credentials, content type, and required fields on all 5 sites.</div>
            <div class="meta">14:02:01 &middot; 2.4s</div></div></li>
          <li class="stage ok"><span class="dot">$(Ico 'check' 13)</span>
            <div><div class="t">Media upload</div>
            <div class="d">Featured image uploaded to 4 sites.</div>
            <div class="meta">14:02:04 &middot; 1.1s</div></div></li>
          <li class="stage ok"><span class="dot">$(Ico 'check' 13)</span>
            <div><div class="t">Post creation</div>
            <div class="d">4 posts created, 1 blocked at authentication.</div>
            <div class="meta">14:02:05 &middot; 6.8s</div></div></li>
          <li class="stage ok"><span class="dot">$(Ico 'check' 13)</span>
            <div><div class="t">Terms and meta</div>
            <div class="d">Categories, author, and SEO fields applied.</div>
            <div class="meta">14:02:12 &middot; 0.9s</div></div></li>
          <li class="stage bad"><span class="dot">$(Ico 'x' 13)</span>
            <div><div class="t">Verification</div>
            <div class="d">1 of 5 sites failed to verify.</div>
            <div class="meta">14:02:13 &middot; 0.4s</div></div></li>
        </ul>
      </div>
    </div>

    <div class="card" style="margin-top:16px">
      <div class="card-h"><h3>Results per site</h3></div>
      <table class="tbl">
        <thead><tr><th>Site</th><th>Result</th><th>Post ID</th><th>URL</th></tr></thead>
        <tbody>
          <tr><td class="strong">emoversuae.com</td>
              <td><span class="chip chip-ok"><span class="dot"></span>Live</span></td>
              <td class="mono small">10455</td>
              <td><a class="small" href="preview.html">modern-renting-in-dubai-flexi-rent</a></td></tr>
          <tr><td class="strong">emoversrss.com</td>
              <td><span class="chip chip-ok"><span class="dot"></span>Live</span></td>
              <td class="mono small">8812</td>
              <td><a class="small" href="preview.html">modern-renting-in-dubai-flexi-rent</a></td></tr>
          <tr><td class="strong">swanwomenswear.ae</td>
              <td><span class="chip chip-ok"><span class="dot"></span>Live</span></td>
              <td class="mono small">2190</td>
              <td><a class="small" href="preview.html">modern-renting-in-dubai-flexi-rent</a></td></tr>
          <tr><td class="strong">ladypackers.ae</td>
              <td><span class="chip chip-warn"><span class="dot"></span>Live, term substituted</span></td>
              <td class="mono small">640</td>
              <td><a class="small" href="preview.html">modern-renting-in-dubai-flexi-rent</a></td></tr>
          <tr><td class="strong" style="opacity:.6">emovers.com</td>
              <td><span class="chip chip-danger"><span class="dot"></span>Blocked</span></td>
              <td class="mono small">&mdash;</td>
              <td><a class="small" href="publishing-failures.html">See failure</a></td></tr>
        </tbody>
      </table>
    </div>
  </div>

  <div>
    <div class="card">
      <div class="card-h"><h3>Response log</h3></div>
      <div class="card-b">
        <div class="treeline p-strip"><span class="mono xs" style="width:62px;flex:none">14:02:01</span>
          <span class="mono xs">POST /wp-json/wp/v2/posts</span></div>
        <div class="treeline"><span class="mono xs" style="width:62px;flex:none">14:02:05</span>
          <span class="mono xs">201 Created &middot; id 10455</span></div>
        <div class="treeline"><span class="mono xs" style="width:62px;flex:none">14:02:06</span>
          <span class="mono xs">200 OK &middot; /media</span></div>
        <div class="treeline p-strip"><span class="mono xs" style="width:62px;flex:none">14:02:13</span>
          <span class="mono xs">401 Unauthorized &middot; move-on</span></div>
        <div class="note" style="margin-top:12px">Swan keeps the status code and the request id from every
        call. This is the evidence needed to prove what was and was not written to a client site.</div>
      </div>
    </div>
  </div>
</div>
"@
