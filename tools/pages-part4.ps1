
# ================================================================= part 4
# supporting pages: publications, sites, search, settings

# ---------------------------------------------------------------- 18. publications
Page 'publications.html' 'Publications' 'publications.html' '' @"
<div class="phead">
  <div class="grow"><h1>Publications</h1>
  <p>An article on one site is a publication. Scheduling and deployment both live here.</p></div>
</div>
<div class="card">
  <table class="tbl">
    <thead><tr><th>Article</th><th>Site</th><th>State</th><th>Publish</th><th class="right">Post ID</th></tr></thead>
    <tbody>
      <tr><td><a class="t" href="article-content.html">Modern Renting in Dubai</a></td>
          <td>emoversuae.com</td><td><span class="chip chip-ok"><span class="dot"></span>Live</span></td>
          <td class="muted small">26 Sep 14:02</td><td class="right mono small">10455</td></tr>
      <tr><td><a class="t" href="article-content.html">Modern Renting in Dubai</a></td>
          <td>emoversrss.com</td><td><span class="chip chip-ok"><span class="dot"></span>Live</span></td>
          <td class="muted small">26 Sep 14:02</td><td class="right mono small">8812</td></tr>
      <tr><td><a class="t" href="article-content.html">Modern Renting in Dubai</a></td>
          <td>swanwomenswear.ae</td><td><span class="chip chip-ok"><span class="dot"></span>Live</span></td>
          <td class="muted small">26 Sep 14:02</td><td class="right mono small">2190</td></tr>
      <tr><td><a class="t" href="article-content.html">Modern Renting in Dubai</a></td>
          <td>ladypackers.ae</td><td><span class="chip chip-warn"><span class="dot"></span>Live, term substituted</span></td>
          <td class="muted small">26 Sep 14:02</td><td class="right mono small">640</td></tr>
      <tr><td><a class="t" href="article-content.html">Modern Renting in Dubai</a></td>
          <td class="muted">emovers.com</td>
          <td><span class="chip chip-danger"><span class="dot"></span>Blocked</span></td>
          <td class="muted small">-</td>
          <td class="right"><a class="small" href="publishing-failures.html">401</a></td></tr>
      <tr><td><a class="t" href="article-content.html">What is Relocation Stress Syndrome</a></td>
          <td>emoversuae.com</td><td><span class="chip chip-warn"><span class="dot"></span>Queued</span></td>
          <td class="muted small">28 Sep 09:00</td><td class="right mono small">&mdash;</td></tr>
      <tr><td><a class="t" href="article-content.html">The IIM Ahmedabad Case Study</a></td>
          <td class="muted">&mdash;</td><td><span class="chip chip-neutral">No site chosen</span></td>
          <td class="muted small">&mdash;</td>
          <td class="right"><a class="small" href="publication-new.html">Choose</a></td></tr>
    </tbody>
  </table>
</div>
"@

# ---------------------------------------------------------------- 19. sites
Page 'sites.html' 'Sites' 'sites.html' '' @"
<div class="phead">
  <div class="grow"><h1>Sites</h1>
  <p>One flat list, not nested by client. Nine sites, eight connected.</p></div>
  <div class="phead-acts"><button class="btn">$(Ico 'plus') Connect a site</button></div>
</div>
<div class="card">
  <table class="tbl">
    <thead><tr><th>Site</th><th>Client</th><th>Content type</th><th>REST</th><th>Credentials</th><th>Health</th></tr></thead>
    <tbody>
      <tr><td class="strong">emoversuae.com</td><td class="muted">E-Movers International</td>
          <td><span class="chip chip-neutral">move-on</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>200</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>Stored</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>Healthy</span></td></tr>
      <tr><td class="strong">emoversrss.com</td><td class="muted">E-Movers International</td>
          <td><span class="chip chip-neutral">post</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>200</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>Stored</span></td>
          <td><span class="chip chip-warn"><span class="dot"></span>Media failures</span></td></tr>
      <tr><td class="strong">swanwomenswear.ae</td><td class="muted">Swan Womenswear</td>
          <td><span class="chip chip-neutral">post</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>200</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>Stored</span></td>
          <td><span class="chip chip-warn"><span class="dot"></span>Term missing</span></td></tr>
      <tr><td class="strong">ladypackers.ae</td><td class="muted">Lady Packers</td>
          <td><span class="chip chip-neutral">post</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>200</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>Stored</span></td>
          <td><span class="chip chip-warn"><span class="dot"></span>Taxonomy unverifiable</span></td></tr>
      <tr><td class="strong" style="opacity:.6">emovers.com</td><td class="muted">E-Movers International</td>
          <td><span class="chip chip-neutral">move-on</span></td>
          <td><span class="chip chip-danger"><span class="dot"></span>401</span></td>
          <td><span class="chip chip-warn"><span class="dot"></span>Not set</span></td>
          <td><span class="chip chip-danger"><span class="dot"></span>Not connected</span></td></tr>
      <tr><td class="strong">allaboutvat.com</td><td class="muted">All About VAT</td>
          <td><span class="chip chip-neutral">post</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>200</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>Stored</span></td>
          <td><span class="chip chip-warn"><span class="dot"></span>Fields unknown</span></td></tr>
      <tr><td class="strong">electricway.ae</td><td class="muted">Electricway</td>
          <td><span class="chip chip-neutral">post</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>200</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>Stored</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>Healthy</span></td></tr>
      <tr><td class="strong">swansol.ae</td><td class="muted">Swan Solutions</td>
          <td><span class="chip chip-neutral">post</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>200</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>Stored</span></td>
          <td><span class="chip chip-warn"><span class="dot"></span>Fields unknown</span></td></tr>
      <tr><td class="strong">IORA Global</td><td class="muted">IORA Global</td>
          <td><span class="chip chip-neutral">post</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>200</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>Stored</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>Healthy</span></td></tr>
    </tbody>
  </table>
</div>
<div class="card" style="margin-top:20px">
  <div class="card-b">
    <div class="row-tight" style="margin-bottom:8px">$(Ico 'info' 16)<h3 style="font-size:15px">Why this is a flat list</h3></div>
    <p class="small muted" style="margin:0">Sites are the thing that actually fails, and they are the thing
    you search for. Nesting them under clients adds a click to every troubleshooting step for no gain &mdash;
    the client name is a column, not a hierarchy.</p>
  </div>
</div>
"@

# ---------------------------------------------------------------- 20. search
Page 'search.html' 'Search' 'dashboard.html' '' @"
<div class="phead">
  <div class="grow"><h1>Results for &ldquo;dubai&rdquo;</h1>
  <p>Searched articles, sites, and deployments.</p></div>
</div>

<div class="card" style="margin-bottom:16px">
  <div class="card-h"><h3>Articles <span class="chip chip-neutral" style="margin-left:8px">3</span></h3></div>
  <table class="tbl">
    <tbody>
      <tr><td><a class="t" href="article-content.html">Modern Renting in Dubai: How Flexi Rent and Payment Rewards Are Easing the Relocation Journey</a>
          <div class="muted xs">Real Estate &middot; 629 words &middot; live on 4 sites</div></td></tr>
      <tr><td><a class="t" href="article-content.html">What is Relocation Stress Syndrome</a>
          <div class="muted xs">Health &amp; Wellness &middot; 618 words &middot; awaiting approval</div></td></tr>
      <tr><td><a class="t" href="article-content.html">The IIM Ahmedabad Case Study</a>
          <div class="muted xs">Thought Leadership &middot; 331 words &middot; draft</div></td></tr>
    </tbody>
  </table>
</div>

<div class="card">
  <div class="card-h"><h3>Sites <span class="chip chip-neutral" style="margin-left:8px">2</span></h3></div>
  <table class="tbl">
    <tbody>
      <tr><td><a class="t" href="sites.html">swanwomenswear.ae</a>
          <div class="muted xs">Swan Womenswear &middot; healthy</div></td></tr>
      <tr><td><a class="t" href="sites.html">ladypackers.ae</a>
          <div class="muted xs">Lady Packers &middot; taxonomy unverifiable</div></td></tr>
    </tbody>
  </table>
</div>
"@

# ---------------------------------------------------------------- 21. settings team
Page 'settings-team.html' 'Team' 'settings-team.html' '' @"
<div class="phead">
  <div class="grow"><h1>Team</h1><p>Who can write, review, and publish.</p></div>
  <div class="phead-acts"><button class="btn">$(Ico 'plus') Invite</button></div>
</div>
<div class="card">
  <table class="tbl">
    <thead><tr><th>Person</th><th>Clients</th><th>Role</th><th>Approval</th><th>Sign-in</th></tr></thead>
    <tbody>
      <tr><td><div class="row-tight"><span class="avatar">AR</span><div><div class="strong">Aisha Rahman</div><div class="muted xs">aisha@emqube.com</div></div></div></td>
          <td class="muted">All 9</td><td><span class="chip chip-neutral">Writer</span></td>
          <td><span class="chip chip-ok">Approver</span></td><td><span class="chip chip-info">Microsoft</span></td></tr>
      <tr><td><div class="row-tight"><span class="avatar" style="background:#525252">VS</span><div><div class="strong">Vikram S.</div><div class="muted xs">vikram@emqube.com</div></div></div></td>
          <td class="muted">All 9</td><td><span class="chip chip-neutral">SEO</span></td>
          <td><span class="chip chip-ok">Approver</span></td><td><span class="chip chip-info">Microsoft</span></td></tr>
      <tr><td><div class="row-tight"><span class="avatar" style="background:#767676">RK</span><div><div class="strong">Ravi K.</div><div class="muted xs">ravi@emqube.com</div></div></div></td>
          <td class="muted">3</td><td><span class="chip chip-neutral">Writer</span></td>
          <td><span class="chip chip-neutral">None</span></td><td><span class="chip chip-neutral">Password</span></td></tr>
      <tr><td><div class="row-tight"><span class="avatar" style="background:#3d3d3d">MI</span><div><div class="strong">Meera Iyer</div><div class="muted xs">meera@emqube.com</div></div></div></td>
          <td class="muted">5</td><td><span class="chip chip-neutral">Legal</span></td>
          <td><span class="chip chip-ok">Approver</span></td><td><span class="chip chip-info">Microsoft</span></td></tr>
    </tbody>
  </table>
</div>
<div class="card" style="margin-top:20px">
  <div class="card-b">
    <div class="row-tight" style="margin-bottom:8px">$(Ico 'info' 16)<h3 style="font-size:15px">Sign-in method</h3></div>
    <p class="small muted" style="margin:0">The login screen currently offers both a password and
    &ldquo;Continue with Microsoft&rdquo; because <span class="mono">decision A</span> is open. Once it is
    settled, this column becomes the only thing that matters &mdash; per person, not per workspace.</p>
  </div>
</div>
"@

# ---------------------------------------------------------------- 22. settings workspace
Page 'settings-workspace.html' 'Workspace' 'settings-workspace.html' '' @"
<div class="phead">
  <div class="grow"><h1>Workspace settings</h1><p>Defaults that apply unless a client overrides them.</p></div>
</div>
<div class="grid-2">
  <div class="card">
    <div class="card-h"><h2>Publishing</h2></div>
    <div class="card-b">
      <div class="field">
        <label for="w-words">Minimum word count</label>
        <input class="input" type="number" id="w-words" value="500">
        <p class="hint">Advisory. A short article is flagged in review, never blocked.</p>
      </div>
      <div class="field">
        <label for="w-slug">Slug length limit</label>
        <input class="input" type="number" id="w-slug" value="75">
        <p class="hint">Longer slugs are shortened automatically and the change is logged.</p>
      </div>
      <div class="field" style="margin-bottom:0">
        <label class="checkline"><input type="checkbox" checked> Require a featured image before publishing</label>
      </div>
    </div>
  </div>
  <div class="card">
    <div class="card-h"><h2>Structure</h2></div>
    <div class="card-b">
      <div class="field">
        <label for="w-min">Minimum upload compliance</label>
        <select class="select" id="w-min">
          <option>Reject anything that fails a check</option>
          <option>Warn, and let the author continue</option>
        </select>
        <p class="hint">Only the strict option is recommended, and it is what the template was built for.</p>
      </div>
      <div class="field" style="margin-bottom:0">
        <label class="checkline"><input type="checkbox" checked> Record an audit entry for every publish</label>
      </div>
    </div>
  </div>
</div>
<div class="card" style="margin-top:16px">
  <div class="card-b">
    <div class="row-tight" style="margin-bottom:8px">$(Ico 'info' 16)<h3 style="font-size:15px">Nothing here touches a client site</h3></div>
    <p class="small muted" style="margin:0">Changing a default applies to future deployments only. Articles
    already live are never edited retroactively &mdash; a live post changes only when someone publishes a
    new version of it, which is itself a logged publication.</p>
  </div>
</div>
"@
