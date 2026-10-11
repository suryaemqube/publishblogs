# pages.ps1 - page bodies, dot-sourced by build-prototype.ps1
# Ico(), Page(), Bare(), $Articles, $Reviewers, $Failures are already in scope.

$MS = '<svg class="ms-mark" viewBox="0 0 23 23" aria-hidden="true"><rect x="1" y="1" width="9" height="9" fill="#f25022"/><rect x="12" y="1" width="10" height="9" fill="#7fba00"/><rect x="1" y="12" width="9" height="10" fill="#00a4ef"/><rect x="12" y="12" width="10" height="10" fill="#ffb900"/></svg>'

# Compact table body for the detected-article list, reused by several pages.
$DetectedRows = foreach ($a in $Articles) {
  $t = if ($a.Title.Length -gt 54) { $a.Title.Substring(0,54) + '&hellip;' } else { $a.Title }
  '          <tr><td class="muted">' + $a.Id + '</td>' +
  '<td><span class="strong">' + $t + '</span></td>' +
  '<td>' + $a.Words + '</td><td>' + $a.H2s.Count + '</td></tr>'
}
$DetectedRows = $DetectedRows -join "`n"

# ---------------------------------------------------------------- 1. entry
Bare 'index.html' 'Sign in' @"
<div style="display:none"></div>
<meta http-equiv="refresh" content="0; url=login.html">
<a href="login.html">Continue to Swan</a>
"@

# ---------------------------------------------------------------- 2. login
# Decision A is deliberately unresolved, so BOTH options are drawn here.
Bare 'login.html' 'Sign in' @"
<div class="auth">
  <div class="auth-side">
    <img src="assets/emqube-logo-white.svg" alt="emQube">
    <h2>One document in.<br><em>Every site out.</em></h2>
    <p style="max-width:42ch">Swan takes a single Word file, confirms it follows the house template,
       splits it into articles, and publishes each one to the right client sites - with a record of
       who approved what and exactly what shipped.</p>
    <ul>
      <li>$(Ico 'check') Structure is verified before anything is written</li>
      <li>$(Ico 'check') One upload can fan out to several client sites</li>
      <li>$(Ico 'check') Partial failures are listed, never silently swallowed</li>
      <li>$(Ico 'check') Every article keeps its own approvals and versions</li>
    </ul>
    <div class="auth-foot">emqube.com &middot; internal tool</div>
  </div>

  <div class="auth-card">
    <div class="auth-form">
      <h1>Sign in to Swan</h1>
      <p>Use your emQube workspace account.</p>

      <a class="btn btn-sso btn-lg" href="first-run.html" style="justify-content:flex-start">
        $MS <span style="margin-left:6px">Continue with Microsoft</span>
      </a>

      <div class="divider">or</div>

      <form action="first-run.html" method="get">
        <div class="field">
          <label for="email">Work email</label>
          <input class="input" type="email" id="email" name="email" value="aisha@emqube.com" autocomplete="username">
        </div>
        <div class="field">
          <label for="pw">Password</label>
          <input class="input" type="password" id="pw" name="pw" value="password" autocomplete="current-password">
        </div>
        <div class="row" style="margin-bottom:18px">
          <label class="checkline"><input type="checkbox" checked> Keep me signed in</label>
          <a class="small" href="login.html" style="margin-left:auto">Forgot password?</a>
        </div>
        <button class="btn btn-primary btn-lg" type="submit" style="width:100%">Sign in</button>
      </form>

      <p class="footnote">Both sign-in options are shown because <span class="mono">decision A</span> is
      still open in the plan. Password is the default; Microsoft is the alternative. Pick one before build
      and this panel collapses to a single button.</p>
    </div>
  </div>
</div>
"@

# ---------------------------------------------------------------- 3. first run
Page 'first-run.html' 'Get started' 'dashboard.html' '' @"
<div class="firstrun">
  <div class="crumbs"><a href="dashboard.html">Workspace</a><span class="sep">/</span>Get started</div>
  <h1 style="font-size:28px">Let's set up your workspace</h1>
  <p class="muted" style="max-width:60ch;margin-top:6px">Three short steps. You can change all of this
  later in Settings, and nothing here touches a client site.</p>

  <div class="card" style="margin-top:24px">
    <div class="card-b">
      <div class="row" style="margin-bottom:18px">
        <span class="chip chip-green">1</span><strong>Link a client</strong>
      </div>
      <div class="field">
        <label for="fr-client">Client</label>
        <select class="select" id="fr-client">
          <option>E-Movers International</option>
          <option>Swan Womenswear</option>
          <option>Lady Packers</option>
        </select>
        <p class="hint">A client is a parent brand. Each client owns one or more WordPress sites.</p>
      </div>
    </div>
    <div class="card-b" style="border-top:1px solid var(--border)">
      <div class="row" style="margin-bottom:18px">
        <span class="chip chip-green">2</span><strong>Connect the sites</strong>
      </div>
      <table class="tbl">
        <thead><tr><th>Site</th><th>REST reachable</th><th>Application password</th><th class="right">Use</th></tr></thead>
        <tbody>
          <tr><td class="strong">emoversuae.com</td>
              <td><span class="chip chip-ok"><span class="dot"></span>200</span></td>
              <td><span class="chip chip-ok"><span class="dot"></span>Stored</span></td>
              <td class="right"><input type="checkbox" checked aria-label="Use emoversuae.com"></td></tr>
          <tr><td class="strong">emoversrss.com</td>
              <td><span class="chip chip-ok"><span class="dot"></span>200</span></td>
              <td><span class="chip chip-ok"><span class="dot"></span>Stored</span></td>
              <td class="right"><input type="checkbox" checked aria-label="Use emoversrss.com"></td></tr>
          <tr><td class="strong">swanwomenswear.ae</td>
              <td><span class="chip chip-ok"><span class="dot"></span>200</span></td>
              <td><span class="chip chip-ok"><span class="dot"></span>Stored</span></td>
              <td class="right"><input type="checkbox" checked aria-label="Use swanwomenswear.ae"></td></tr>
          <tr><td class="strong">ladypackers.ae</td>
              <td><span class="chip chip-ok"><span class="dot"></span>200</span></td>
              <td><span class="chip chip-ok"><span class="dot"></span>Stored</span></td>
              <td class="right"><input type="checkbox" checked aria-label="Use ladypackers.ae"></td></tr>
          <tr><td class="strong">emovers.com (move-on)</td>
              <td><span class="chip chip-danger"><span class="dot"></span>401</span></td>
              <td><span class="chip chip-warn"><span class="dot"></span>Not set</span></td>
              <td class="right"><input type="checkbox" disabled aria-label="Use emovers.com move-on"></td></tr>
        </tbody>
      </table>
      <div class="alert alert-warn" style="margin-top:16px">
        $(Ico 'alert' 17)
        <div>
          <h3>emovers.com is not connected yet</h3>
          <p>Its <span class="mono">move-on</span> type and <span class="mono">blog</span> taxonomy both
          answer <span class="mono">401</span> today. Someone with plugin access has to add
          <span class="mono">show_in_rest =&gt; true</span> to the existing registrations - Swan must not
          register a duplicate. Until then this site stays switched off and is never offered for publishing.</p>
        </div>
      </div>
    </div>
    <div class="card-b" style="border-top:1px solid var(--border)">
      <div class="row" style="margin-bottom:18px">
        <span class="chip chip-green">3</span><strong>Confirm who approves</strong>
      </div>
      <p class="muted small" style="margin-top:-4px">Swan can require named approvers per client, or let
      anyone on the team approve. <span class="mono">Decision G</span> is still open, so both are offered
      here; the choice is remembered per client.</p>
      <div class="row wrap">
        <label class="checkline"><input type="radio" name="appr" checked> Named approvers only</label>
        <label class="checkline"><input type="radio" name="appr"> Any team member</label>
      </div>
    </div>
    <div class="card-f">
      <a class="btn btn-primary" href="dashboard.html">Finish and open Swan $(Ico 'arrowr')</a>
      <a class="btn btn-ghost" href="login.html">Cancel</a>
    </div>
  </div>
</div>
"@

# ---------------------------------------------------------------- 4. dashboard
Page 'dashboard.html' 'Dashboard' 'dashboard.html' '' @"
<div class="phead">
  <div class="grow">
    <h1>Good morning, Aisha</h1>
    <p>Three things need you. Nothing is blocked on a client site.</p>
  </div>
  <div class="phead-acts">
    <a class="btn" href="documents.html">All documents</a>
    <a class="btn btn-primary" href="documents-new.html">$(Ico 'upload') New document</a>
  </div>
</div>

<div class="grid-3" style="margin-bottom:20px">
  <div class="stat"><div class="k">Needs review</div><div class="v">3</div>
    <div class="s">2 waiting on SEO, 1 on you</div></div>
  <div class="stat"><div class="k">Ready to publish</div><div class="v">2</div>
    <div class="s">Across 4 sites</div></div>
  <div class="stat"><div class="k">Failed in the last 7 days</div><div class="v" style="color:var(--danger)">4</div>
    <div class="s"><a href="publishing-failures.html">Triage now</a></div></div>
</div>

<div class="grid-2">
  <div class="card">
    <div class="card-h"><h2>Waiting on you</h2>
      <a class="btn btn-sm" href="review.html" style="margin-left:auto">Open queue</a></div>
    <table class="tbl">
      <thead><tr><th>Article</th><th>Blocked on</th><th class="right">Age</th></tr></thead>
      <tbody>
        <tr><td><a class="t" href="article-content.html">What is Relocation Stress Syndrome</a></td>
            <td><span class="chip chip-info">SEO metadata</span></td>
            <td class="right muted">2d</td></tr>
        <tr><td><a class="t" href="article-content.html">The IIM Ahmedabad Case Study</a></td>
            <td><span class="chip chip-warn">Under 500 words</span></td>
            <td class="right muted">4d</td></tr>
        <tr><td><a class="t" href="article-content.html">Why E-Movers Earns 5-Star Reviews</a></td>
            <td><span class="chip chip-info">No featured image</span></td>
            <td class="right muted">4d</td></tr>
      </tbody>
    </table>
  </div>

  <div class="card">
    <div class="card-h"><h2>Site health</h2>
      <a class="btn btn-sm" href="sites.html" style="margin-left:auto">All sites</a></div>
    <table class="tbl">
      <tbody>
        <tr><td class="strong">emoversuae.com</td>
            <td><span class="chip chip-ok"><span class="dot"></span>Healthy</span></td>
            <td class="right muted small">2 posts today</td></tr>
        <tr><td class="strong">emoversrss.com</td>
            <td><span class="chip chip-warn"><span class="dot"></span>1 media failure</span></td>
            <td class="right"><a class="small" href="publishing-failures.html">Review</a></td></tr>
        <tr><td class="strong">ladypackers.ae</td>
            <td><span class="chip chip-warn"><span class="dot"></span>Preflight blocked</span></td>
            <td class="right"><a class="small" href="publishing-failures.html">Review</a></td></tr>
        <tr><td class="strong">emovers.com</td>
            <td><span class="chip chip-danger"><span class="dot"></span>Not connected</span></td>
            <td class="right"><a class="small" href="first-run.html">Set up</a></td></tr>
      </tbody>
    </table>
  </div>
</div>

<div class="card" style="margin-top:20px">
  <div class="card-h"><h2>Most recent document</h2></div>
  <div class="card-b row wrap" style="gap:24px">
    <div class="grow">
      <div class="row-tight">
        <strong>EmoversUAE-Sep-Blogs.docx</strong>
        <span class="chip chip-danger">$(Ico 'x' 12) Rejected</span>
      </div>
      <p class="muted small" style="margin:6px 0 0">Uploaded 24 Sep by Ravi K. &middot; 72 paragraphs &middot;
      1,926 words &middot; 4 articles detected</p>
    </div>
    <a class="btn" href="document-rejected.html">See what failed</a>
  </div>
</div>
"@

# ---------------------------------------------------------------- 5. documents list
Page 'documents.html' 'Documents' 'documents.html' '' @"
<div class="phead">
  <div class="grow"><h1>Documents</h1><p>Every Word file uploaded into this workspace.</p></div>
  <div class="phead-acts"><a class="btn btn-primary" href="documents-new.html">$(Ico 'upload') New document</a></div>
</div>
<div class="card">
  <table class="tbl">
    <thead><tr><th>File</th><th>Uploaded by</th><th>Structure</th><th>Articles</th><th>Status</th><th class="right">Updated</th></tr></thead>
    <tbody>
      <tr><td><a class="t" href="document-rejected.html">EmoversUAE-Sep-Blogs.docx</a></td>
          <td class="muted">Ravi K.</td>
          <td><span class="chip chip-danger"><span class="dot"></span>Failed</span></td>
          <td>4</td>
          <td><span class="chip chip-danger">Rejected</span></td>
          <td class="right muted small">24 Sep</td></tr>
      <tr><td><a class="t" href="document-report.html">Sep-Blogs-template-v2.docx</a></td>
          <td class="muted">Aisha R.</td>
          <td><span class="chip chip-ok"><span class="dot"></span>Conforming</span></td>
          <td>4</td>
          <td><span class="chip chip-info">Split review</span></td>
          <td class="right muted small">25 Sep</td></tr>
      <tr><td><a class="t" href="document-report.html">LadyPackers-Aug-Recurring.docx</a></td>
          <td class="muted">Aisha R.</td>
          <td><span class="chip chip-ok"><span class="dot"></span>Conforming</span></td>
          <td>2</td>
          <td><span class="chip chip-ok">Published</span></td>
          <td class="right muted small">02 Sep</td></tr>
      <tr><td><a class="t" href="document-rejected.html">Rss-Sep-draft-final-FINAL.docx</a></td>
          <td class="muted">Nadia S.</td>
          <td><span class="chip chip-danger"><span class="dot"></span>Failed</span></td>
          <td>-</td>
          <td><span class="chip chip-neutral">Rejected</span></td>
          <td class="right muted small">21 Sep</td></tr>
    </tbody>
  </table>
</div>
"@

# ---------------------------------------------------------------- 6. new document
Page 'documents-new.html' 'New document' 'documents.html' '<a href="documents.html">Documents</a><span class="sep">/</span>New' @"
<div class="phead">
  <div class="grow"><h1>New document</h1>
  <p>Upload a Word file. Swan checks it against the house template before it becomes anything.</p></div>
</div>

<div class="split">
  <div>
    <div class="drop" id="drop">
      $(Ico 'upload' 34)
      <h3>Drop a .docx here</h3>
      <p>One file may contain several articles. Swan splits it on the template's boundary styles.</p>
      <a class="btn btn-primary" href="document-rejected.html">Choose file</a>
      <div class="or">.docx only &middot; up to 25 MB &middot; macros and tracked changes are stripped on read</div>
    </div>

    <div class="card" style="margin-top:20px">
      <div class="card-h"><h2>What Swan checks</h2></div>
      <div class="card-b">
        <ul class="checks">
          <li><span class="ico good">$(Ico 'okcirc' 17)</span>
            <div><div class="t">Every paragraph carries a named style</div>
            <div class="d">Structure is read from styles only. Direct bold and manual font sizes are
            never guessed at, because guessing is what puts a heading in the middle of a paragraph.</div></div></li>
          <li><span class="ico good">$(Ico 'okcirc' 17)</span>
            <div><div class="t">Each article starts with a boundary style</div>
            <div class="d">A <span class="mono">Title</span>, a <span class="mono">Heading 1</span>, or a
            <span class="mono">Swan Label</span> opens a new article. Without one, Swan cannot tell where
            the previous article ended.</div></div></li>
          <li><span class="ico good">$(Ico 'okcirc' 17)</span>
            <div><div class="t">No heading jumps a level</div>
            <div class="d">H1 to H3 directly breaks the outline and the on-page navigation.</div></div></li>
          <li><span class="ico good">$(Ico 'okcirc' 17)</span>
            <div><div class="t">No empty paragraphs used as spacers</div>
            <div class="d">Use paragraph spacing. Stray blanks become stray breaks on the live site.</div></div></li>
          <li><span class="ico good">$(Ico 'okcirc' 17)</span>
            <div><div class="t">Internal links point at a real target</div>
            <div class="d">Checked inside this document and against the sites this article will ship to.</div></div></li>
        </ul>
        <div class="note">A file that fails any of these is <strong>rejected, not repaired</strong>. The
        <span class="mono">Swan-Article-Template.docx</span> download below is the fastest fix, and
        Swan links to the exact paragraph that broke so nobody has to hunt for it.</div>
      </div>
      <div class="card-f">
        <a class="btn" href="#">$(Ico 'dl') Swan-Article-Template.docx</a>
        <a class="btn btn-ghost" href="#">Style reference</a>
      </div>
    </div>
  </div>

  <div>
    <div class="card">
      <div class="card-h"><h3>Recent uploads</h3></div>
      <div class="card-b stack">
        <div><a href="document-rejected.html" class="strong small">EmoversUAE-Sep-Blogs.docx</a>
          <div class="chip chip-danger" style="margin-top:4px">Rejected</div></div>
        <div><a href="document-report.html" class="strong small">Sep-Blogs-template-v2.docx</a>
          <div class="chip chip-info" style="margin-top:4px">Split review</div></div>
        <div><a href="document-rejected.html" class="strong small">Rss-Sep-draft-FINAL.docx</a>
          <div class="chip chip-neutral" style="margin-top:4px">Rejected</div></div>
      </div>
    </div>
    <div class="card" style="margin-top:16px">
      <div class="card-b">
        <div class="k muted xs" style="text-transform:uppercase;letter-spacing:.06em">Also available</div>
        <p class="small muted" style="margin:8px 0 0">Import an unverified file as a single article
        instead. Everything is highlighted as untrusted and needs a full manual review - it cannot
        be approved in bulk.</p>
        <button class="btn btn-sm btn-danger" style="margin-top:12px">Import anyway (unverified)</button>
      </div>
    </div>
  </div>
</div>
"@

# ---------------------------------------------------------------- 7. REJECTED (real file)
Page 'document-rejected.html' 'EmoversUAE-Sep-Blogs.docx' 'documents.html' '<a href="documents.html">Documents</a><span class="sep">/</span>EmoversUAE-Sep-Blogs.docx' @"
<div class="phead">
  <div class="grow">
    <div class="row-tight"><h1>EmoversUAE-Sep-Blogs.docx</h1>
      <span class="chip chip-danger">$(Ico 'x' 12) Rejected</span></div>
    <p>Uploaded 24 Sep 2026 by Ravi K. &middot; 72 paragraphs &middot; 1,926 words</p>
  </div>
  <div class="phead-acts">
    <a class="btn" href="documents.html">Back</a>
    <a class="btn" href="document-report.html">Upload a corrected file</a>
  </div>
</div>

<div class="alert alert-danger" style="margin-bottom:20px">
  $(Ico 'alert' 17)
  <div>
    <h3>This file cannot be split into articles</h3>
    <p>It has <strong>no named styles at all</strong>. Every heading in this document is direct bold
    formatting on a normal paragraph, so Swan has no machine-readable boundary and cannot tell where one
    article ends and the next begins. Nothing has been saved and nothing was published.</p>
  </div>
</div>

<div class="grid-2">
  <div class="card">
    <div class="card-h"><h2>Findings</h2>
      <span class="chip chip-danger" style="margin-left:auto">3 blocking &middot; 2 advisory</span></div>
    <ul class="checks">
      <li><span class="ico bad">$(Ico 'xcirc' 17)</span>
        <div><div class="t">Paragraph 1 &ndash; 72 use no named styles</div>
        <div class="d">All 72 paragraphs are Word's built-in <span class="mono">Normal</span>. Zero
        paragraphs use <span class="mono">Title</span>, <span class="mono">Heading 1</span>, or
        <span class="mono">Swan Label</span>.</div></div></li>
      <li><span class="ico bad">$(Ico 'xcirc' 17)</span>
        <div><div class="t">18 paragraphs are faked headings</div>
        <div class="d">Paragraph 1, 12, 13, 16, 19, 23, 32, 35, 41, 47, 50, 55, 58, 60, 61, 62, 63, 64, 65,
        66, 67 and 68 are bold body text. This is what makes the document look right in Word and
        publish wrong everywhere else.</div></div></li>
      <li><span class="ico bad">$(Ico 'xcirc' 17)</span>
        <div><div class="t">No article boundary can be found</div>
        <div class="d">Worth knowing: there are clearly <strong>4</strong> articles here - paragraphs
        1, 13, 32 and 55 each start a new piece. But that is a human read, not a rule, and Swan will
        not infer it.</div></div></li>
      <li><span class="ico warn">$(Ico 'alert' 17)</span>
        <div><div class="t">6 empty paragraphs used as spacers</div>
        <div class="d">Paragraphs 11, 22, 31, 54, 70 and 72. On the live site these render as gaps in
        the flow rather than as paragraph spacing.</div></div></li>
      <li><span class="ico warn">$(Ico 'alert' 17)</span>
        <div><div class="t">Editorial note left in the body copy</div>
        <div class="d">Paragraph 10 reads <em>"(Video could be IIM A campus)"</em>. That would go straight
        onto the page.</div></div></li>
      <li><span class="ico info">$(Ico 'info' 17)</span>
        <div><div class="t">Same closing paragraph three times</div>
        <div class="d">Paragraphs 30, 53 and 71 are all the "Request a Proposal / 800-Emovers" sign-off.
        Two of them are probably copy-paste, but Swan cannot tell which is intended.</div></div></li>
    </ul>
  </div>

  <div>
    <div class="card">
      <div class="card-h"><h2>What the file looks like</h2>
        <span class="chip chip-neutral" style="margin-left:auto">Paragraphs 10-19</span></div>
      <div class="card-b" style="font-size:13px;line-height:1.6">
        <div class="muted xs" style="margin-bottom:8px">Rendering the paragraph text, with the bold runs marked:</div>
        <div class="treeline"><span class="muted" style="width:34px;flex:none">[10]</span><span style="color:var(--danger);font-style:italic">(Video could be IIM A campus)</span></div>
        <div class="treeline"><span class="muted" style="width:34px;flex:none">[11]</span><span class="muted xs">(empty paragraph)</span></div>
        <div class="treeline p-keep"><span class="muted" style="width:34px;flex:none">[12]</span><span style="font-weight:700;color:var(--text-strong)">BLOGS</span></div>
        <div class="treeline p-keep"><span class="muted" style="width:34px;flex:none">[13]</span><span style="font-weight:700;color:var(--text-strong)">What is Relocation Stress Syndrome: Why Moving Feels So Overwhelming</span></div>
        <div class="treeline"><span class="muted" style="width:34px;flex:none">[14]</span><span>Upgrading your home is supposed to be a joyful milestone. Whether you are moving to a luxury villa or a compact studio, the excitement is often quickly followed by a strange sense of dread.</span></div>
        <div class="treeline"><span class="muted" style="width:34px;flex:none">[15]</span><span>This hesitation is not mere procrastination. Medical science confirms that moving triggers a distinct, measurable physiological response in the body.</span></div>
        <div class="treeline p-keep"><span class="muted" style="width:34px;flex:none">[16]</span><span style="font-weight:700;color:var(--text-strong)">The Clinical Reality of Relocation Stress</span></div>
        <div class="treeline"><span class="muted" style="width:34px;flex:none">[17]</span><span>Relocation Stress Syndrome was formally recognized as a clinical diagnosis by the North American Academy of Psychiatry.</span></div>
        <div class="note" style="margin-top:14px">The two styles Swan actually needs are
        <span class="mono">Heading 1</span> for the article title and <span class="mono">Heading 2</span>
        for sections. The bold on lines like "The Clinical Reality of Relocation Stress" has to become a
        real <span class="mono">Heading 2</span>.</div>
      </div>
    </div>

    <div class="card" style="margin-top:16px">
      <div class="card-b">
        <h3 style="font-size:15px;margin-bottom:8px">Fix it in about ten minutes</h3>
        <ol class="small" style="margin:0;padding-left:20px;color:var(--text-muted)">
          <li style="margin-bottom:6px">Open <span class="mono">Swan-Article-Template.docx</span> and look at the September worked example - it is this same content, correctly styled.</li>
          <li style="margin-bottom:6px">Select each bold line and set a real heading style.</li>
          <li style="margin-bottom:6px">Delete the six empty spacer paragraphs and the video note in paragraph 10.</li>
          <li style="margin-bottom:6px">Re-upload. Swan will re-check every rule in seconds.</li>
        </ol>
        <div class="row" style="margin-top:14px">
          <a class="btn btn-primary btn-sm" href="document-report.html">Upload corrected file</a>
          <a class="btn btn-sm" href="#">$(Ico 'dl') Get the template</a>
        </div>
      </div>
      <div class="card-f">
        <button class="btn btn-danger btn-sm">Import anyway (unverified)</button>
        <span class="xs muted">Creates 1 article. Untrusted. Cannot be bulk-approved.</span>
      </div>
    </div>
  </div>
</div>
"@

# ---------------------------------------------------------------- 8. conforming report
Page 'document-report.html' 'Sep-Blogs-template-v2.docx' 'documents.html' '<a href="documents.html">Documents</a><span class="sep">/</span>Sep-Blogs-template-v2.docx' @"
<div class="phead">
  <div class="grow">
    <div class="row-tight"><h1>Sep-Blogs-template-v2.docx</h1>
      <span class="chip chip-ok">$(Ico 'check' 12) Conforming</span></div>
    <p>Uploaded 25 Sep 2026 by Aisha R. &middot; 4 articles &middot; 1,926 words &middot; 22 headings</p>
  </div>
  <div class="phead-acts">
    <a class="btn" href="documents.html">Back</a>
    <a class="btn btn-primary" href="document-split.html">Review the split $(Ico 'arrowr')</a>
  </div>
</div>

<div class="grid-2">
  <div class="card">
    <div class="card-h"><h2>Structure check</h2>
      <span class="chip chip-ok" style="margin-left:auto">All 5 passed</span></div>
    <ul class="checks">
      <li><span class="ico good">$(Ico 'okcirc' 17)</span>
        <div><div class="t">Named styles throughout</div>
        <div class="d">72 paragraphs, 0 in <span class="mono">Normal</span>. Every paragraph resolves to one
        of the 9 house styles and all 9 are defined in the document's own style part.</div></div></li>
      <li><span class="ico good">$(Ico 'okcirc' 17)</span>
        <div><div class="t">4 article boundaries found</div>
        <div class="d">Detected on <span class="mono">Title</span> and <span class="mono">Swan Label</span>.
        Next page lets you confirm or move each one.</div></div></li>
      <li><span class="ico good">$(Ico 'okcirc' 17)</span>
        <div><div class="t">Heading levels never skip</div>
        <div class="d">18 <span class="mono">Heading 1</span> (4 article titles + 14 section heads are
        H2), 4 <span class="mono">Heading 2</span>, 0 skipped levels.</div></div></li>
      <li><span class="ico good">$(Ico 'okcirc' 17)</span>
        <div><div class="t">No spacer paragraphs</div>
        <div class="d">0 empty paragraphs. The 6 that were in the original are gone.</div></div></li>
      <li><span class="ico good">$(Ico 'okcirc' 17)</span>
        <div><div class="t">27 internal links resolve</div>
        <div class="d">All 27 declared relationships point at a real target; 0 dangling. Cross-references to
        <span class="mono">#lady_packers</span> are corrected.</div></div></li>
    </ul>
  </div>

  <div>
    <div class="card">
      <div class="card-h"><h3>Detected articles</h3></div>
      <table class="tbl">
        <thead><tr><th>#</th><th>Title</th><th>Words</th><th>H2s</th></tr></thead>
        <tbody>
$DetectedRows
        </tbody>
      </table>
    </div>
    <div class="card" style="margin-top:16px">
      <div class="card-b">
        <div class="row-tight" style="margin-bottom:8px">$(Ico 'info' 16)<h3 style="font-size:15px">Next step</h3></div>
        <p class="small muted" style="margin:0">Swan has proposed the split but has not committed it. On the
        next page you can move a boundary, drop an article, or merge two - before anything becomes a
        record in its own right.</p>
      </div>
    </div>
  </div>
</div>
"@
