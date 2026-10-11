
# ================================================================= part 2
# split review, content/SEO, approval, publication, preview, triage

# Short real excerpts, keyed by article id, used on the split-review cards.
$Sample = @{
  1 = @('Like many aspiring students, studying at IIM Ahmedabad was always a personal dream.',
        'When IIMA opened their campus in Dubai, they were actively looking for regional case studies to add to their curriculum.',
        '(Video could be IIM A campus)',
        'Next time you go for an Executive Education course to their campus here or in Ahmedabad, look us up.')
  2 = @('Upgrading your home is supposed to be a joyful milestone. Whether you are moving to a luxury villa or a compact studio, the excitement is often quickly followed by a strange sense of dread.',
        'Relocation Stress Syndrome was formally recognized as a clinical diagnosis by the North American Academy of Psychiatry.',
        'Empathetic and Specialized Staffing: For feminine attire, personal items, and delicate keepsakes...',
        'Ready to experience a calm, seamless move? Request a Proposal today or call us at 800-Emovers.')
  3 = @('Moving into a new home in Dubai is an exciting milestone, whether you are settling into a high-rise or a townhouse.',
        'The Dubai Land Department (DLD) introduced the Flexi Rent scheme in partnership with major real estate developers.',
        'Zero Monthly Markup: The total annual rent remains exactly the same whether paid in a single cheque or monthly.',
        'Planning your next home transition? Request a Proposal today or call us at 800-Emovers.')
  4 = @('Our Google Reviews are consistently rated 5-stars, and we do not have to go hunting for testimonials.',
        'Here are the five operational commitments we keep to earn those stars every day.',
        'We ensure complete financial transparency before a single item is packed.',
        'Planning your next transition? Request a Proposal today or call us at 800-Emovers to speak with our experts.')
}
$StatusChip = @{ 'Draft'='chip-neutral'; 'Needs SEO'='chip-info'; 'Ready'='chip-ok' }

# ---------------------------------------------------------------- 9. split review
$cards = foreach ($a in $Articles) {
  $sb = [System.Text.StringBuilder]::new()
  [void]$sb.Append('        <li class="cand">')
  [void]$sb.Append('          <div class="cand-h">')
  [void]$sb.Append('            <span class="ord">' + $a.Id + '</span>')
  [void]$sb.Append('            <div class="grow"><div class="t">' + $a.Title + '</div>')
  [void]$sb.Append('              <div class="muted xs" style="margin-top:3px">')
  [void]$sb.Append('                <span class="mono">Swan Label</span> ' + $a.Label + ' &middot; ')
  [void]$sb.Append('                <span class="mono">Title</span> ' + $a.Paras + ' paragraphs &middot; ')
  [void]$sb.Append([string]$a.Words + ' words &middot; ' + $a.H2s.Count + ' H2 &middot; ' + $a.Lists + ' list block')
  [void]$sb.Append('              </div></div>')
  [void]$sb.Append('            <span class="chip ' + $StatusChip[$a.Status] + '">' + $a.Status + '</span>')
  [void]$sb.Append('            <div class="row-tight">')
  [void]$sb.Append('              <button class="btn btn-sm btn-ghost" title="Rename">Rename</button>')
  [void]$sb.Append('              <button class="btn btn-sm btn-ghost" title="Drop this article">Drop</button>')
  [void]$sb.Append('            </div>')
  [void]$sb.Append('          </div>')
  [void]$sb.Append('          <div class="cand-b">')
  foreach ($line in $Sample[$a.Id]) {
    $isF = $line -match 'Request a Proposal|Video could be|Google Reviews'
    $cls = if ($isF) { 'p-lift' } else { 'p-keep' }
    $sty = if ($isF) { ' style="color:var(--danger);font-style:italic"' } else { '' }
    $txt = $line
    if ($txt.Length -gt 104) { $txt = $txt.Substring(0,104) + '&hellip;' }
    [void]$sb.Append('            <div class="treeline ' + $cls + '"><span class="style">body</span><span' + $sty + '>' + $txt + '</span></div>')
  }
  foreach ($h in $a.H2s) {
    $ht = $h
    if ($ht.Length -gt 84) { $ht = $ht.Substring(0,84) + '&hellip;' }
    [void]$sb.Append('            <div class="treeline"><span class="style">Heading 2</span><span class="strong">' + $ht + '</span></div>')
  }
  if ($a.Note) {
    [void]$sb.Append('            <div class="note"><strong>Advisory:</strong> ' + $a.Note + '</div>')
  }
  [void]$sb.Append('            <div class="row-tight" style="margin-top:12px">')
  [void]$sb.Append('              <button class="btn btn-sm">Move boundary up</button>')
  [void]$sb.Append('              <button class="btn btn-sm">Move boundary down</button>')
  [void]$sb.Append('              <a class="btn btn-sm btn-ghost" href="article-content.html">Open editor</a>')
  [void]$sb.Append('            </div>')
  [void]$sb.Append('          </div>')
  [void]$sb.Append('        </li>')
  $sb.ToString()
}
$cards = $cards -join "`n"

Page 'document-split.html' 'Review the split' 'documents.html' '<a href="documents.html">Documents</a><span class="sep">/</span><a href="document-report.html">Sep-Blogs-template-v2.docx</a><span class="sep">/</span>Review the split' @"
<div class="phead">
  <div class="grow"><h1>Review the split</h1>
  <p>Swan found <strong>4 articles</strong> in <span class="mono">Sep-Blogs-template-v2.docx</span>.
  Nothing is created until you confirm.</p></div>
  <div class="phead-acts">
    <a class="btn" href="document-report.html">Back</a>
    <a class="btn btn-primary" href="articles.html">Create 4 articles $(Ico 'arrowr')</a>
  </div>
</div>

<div class="split">
  <div>
    <ul class="splitlist">
$cards
    </ul>
    <div class="boundary" style="margin:4px 0 12px">
      <span class="ln"></span><span class="bd">end of file</span><span class="ln"></span>
    </div>
    <button class="btn btn-sm">+ Merge with the article below</button>
  </div>

  <div>
    <div class="card">
      <div class="card-h"><h3>Split summary</h3></div>
      <table class="tbl">
        <tbody>
          <tr><td>Articles</td><td class="right strong">4</td></tr>
          <tr><td>Total words</td><td class="right strong">1,926</td></tr>
          <tr><td>H2 headings</td><td class="right strong">14</td></tr>
          <tr><td>List blocks</td><td class="right strong">3</td></tr>
          <tr><td>CTAs found</td><td class="right strong">3</td></tr>
        </tbody>
      </table>
      <div class="card-b" style="border-top:1px solid var(--border)">
        <div class="alert alert-warn">
          $(Ico 'alert' 17)
          <div><h3>Two articles are thin</h3>
          <p>Article 1 (331 words) and article 4 (348) are under the 500-word minimum. Swan will not
          block you - it is advisory - but both will be flagged in review and will likely need
          expanding before they can rank.</p></div>
        </div>
      </div>
    </div>

    <div class="card" style="margin-top:16px">
      <div class="card-b">
        <div class="row-tight" style="margin-bottom:8px">$(Ico 'info' 16)<h3 style="font-size:15px">How boundaries work</h3></div>
        <p class="small muted" style="margin:0 0 8px">A new article starts at a
        <span class="mono">Title</span>, a <span class="mono">Heading 1</span>, or a
        <span class="mono">Swan Label</span>. The three buttons let you nudge a boundary onto a different
        paragraph if Swan drew it in the wrong place.</p>
        <p class="small muted" style="margin:0">The <span class="mono">BLOGS</span> line in the original file
        was a section divider for humans. Here it is a <span class="mono">Swan Label</span> on article 2, so
        the label is stored as metadata rather than printed.</p>
      </div>
    </div>
  </div>
</div>
"@

# ---------------------------------------------------------------- 10. articles list
Page 'articles.html' 'Articles' 'articles.html' '' @"
<div class="phead">
  <div class="grow"><h1>Articles</h1><p>4 articles from <span class="mono">Sep-Blogs-template-v2.docx</span>.</p></div>
  <div class="phead-acts"><a class="btn" href="documents.html">Back to document</a></div>
</div>
<div class="card">
  <table class="tbl">
    <thead><tr><th>#</th><th>Title</th><th>Label</th><th>Words</th><th>SEO</th><th>Approval</th><th class="right">Sites</th></tr></thead>
    <tbody>
      <tr><td class="muted">1</td>
          <td><a class="t" href="article-content.html">The IIM Ahmedabad Case Study</a>
              <div class="muted xs">No H2 structure</div></td>
          <td><span class="chip chip-neutral">Thought Leadership</span></td>
          <td><span class="chip chip-warn">331</span></td>
          <td><span class="chip chip-danger">Missing</span></td>
          <td><span class="chip chip-neutral">Not submitted</span></td>
          <td class="right muted">-</td></tr>
      <tr><td class="muted">2</td>
          <td><a class="t" href="article-content.html">What is Relocation Stress Syndrome</a>
              <div class="muted xs">Title is 76 characters</div></td>
          <td><span class="chip chip-neutral">Health &amp; Wellness</span></td>
          <td>618</td>
          <td><span class="chip chip-info">Draft</span></td>
          <td><span class="chip chip-info">2 of 3</span></td>
          <td class="right muted">-</td></tr>
      <tr><td class="muted">3</td>
          <td><a class="t" href="article-content.html">Modern Renting in Dubai</a>
              <div class="muted xs">Inconsistent H2 numbering</div></td>
          <td><span class="chip chip-neutral">Real Estate</span></td>
          <td>629</td>
          <td><span class="chip chip-ok"><span class="dot"></span>Complete</span></td>
          <td><span class="chip chip-ok"><span class="dot"></span>Approved</span></td>
          <td class="right"><a class="small" href="publication-new.html">2 chosen</a></td></tr>
      <tr><td class="muted">4</td>
          <td><a class="t" href="article-content.html">Why E-Movers Earns 5-Star Reviews</a>
              <div class="muted xs">Under 500 words</div></td>
          <td><span class="chip chip-neutral">Trust &amp; Reputation</span></td>
          <td><span class="chip chip-warn">348</span></td>
          <td><span class="chip chip-info">Draft</span></td>
          <td><span class="chip chip-neutral">Not submitted</span></td>
          <td class="right muted">-</td></tr>
    </tbody>
  </table>
</div>
<div class="card" style="margin-top:20px">
  <div class="card-h"><h3>The one writer journey, start to finish</h3></div>
  <div class="card-b">
    <ol class="small" style="margin:0;padding-left:20px;color:var(--text-muted);line-height:2">
      <li><a href="documents-new.html">Upload</a> &rarr; the real file is <a href="document-rejected.html">rejected</a></li>
      <li><a href="document-report.html">Re-upload corrected</a> &rarr; <a href="document-split.html">review the split</a></li>
      <li><a href="article-content.html">Edit structure</a> &rarr; <a href="article-seo.html">add SEO and a featured image</a></li>
      <li><a href="review.html">Send for approval</a> &rarr; collect sign-off</li>
      <li><a href="publication-new.html">Choose sites</a> &rarr; <a href="preview.html">preview</a> &rarr; publish</li>
      <li><a href="deployments.html">Watch the run</a> &rarr; <a href="publishing-failures.html">triage what failed</a></li>
    </ol>
  </div>
</div>
"@

# ---------------------------------------------------------------- 11. content editor
Page 'article-content.html' 'Edit article' 'articles.html' '<a href="articles.html">Articles</a><span class="sep">/</span>What is Relocation Stress Syndrome' @"
<div class="phead">
  <div class="grow">
    <div class="row-tight"><h1 style="font-size:22px">What is Relocation Stress Syndrome: Why Moving Feels So Overwhelming</h1></div>
    <p>Article 2 of 4 &middot; 618 words &middot; last saved 2 minutes ago</p>
  </div>
  <div class="phead-acts">
    <a class="btn" href="articles.html">Back</a>
    <a class="btn" href="article-seo.html">SEO &amp; image</a>
    <a class="btn btn-primary" href="review.html">Send for approval</a>
  </div>
</div>

<div class="split">
  <div>
    <div class="alert alert-warn" style="margin-bottom:16px">
      $(Ico 'alert' 17)
      <div><h3>Advisories from the split</h3>
      <p>Your title is <strong>76 characters</strong>. Search engines will cut it around 60, and it wraps
      to two lines on a phone. Everything else in this article is structurally sound.</p></div>
    </div>

    <div class="ed">
      <div class="ed-h">
        <span class="chip chip-neutral">Swan Label</span>
        <span class="strong small">Health &amp; Wellness</span>
        <span class="grow"></span>
        <button class="btn btn-sm btn-ghost">Change</button>
      </div>
    </div>

    <div class="ed" style="margin-top:12px">
      <div class="ed-h"><span class="lvl mono">Heading 1</span>
        <span class="grow"></span>
        <button class="btn btn-sm btn-ghost">Edit style</button></div>
      <div class="ed-b">
        <div class="contenteditable" style="font-size:19px;font-weight:600;line-height:1.3;color:var(--text-strong)">What is Relocation Stress Syndrome: Why Moving Feels So Overwhelming</div>
        <div class="row-tight" style="margin-top:8px">
          <span class="muted xs">76 characters</span>
          <span class="chip chip-warn" style="margin-left:auto">Longer than 60</span>
        </div>
      </div>
    </div>

    <div class="ed" style="margin-top:12px">
      <div class="ed-h"><span class="lvl mono">body</span>
        <span class="grow"></span>
        <span class="muted xs">4 paragraphs</span></div>
      <div class="ed-b">
        <p>Upgrading your home is supposed to be a joyful milestone. Whether you are moving to a luxury
        villa or a compact studio, the excitement is often quickly followed by a strange sense of dread.</p>
        <p>This hesitation is not mere procrastination. Medical science confirms that moving triggers a
        distinct, measurable physiological response in the body.</p>
      </div>
    </div>

    <div class="ed" style="margin-top:12px">
      <div class="ed-h"><span class="lvl mono">Heading 2</span>
        <span class="strong small">The Clinical Reality of Relocation Stress</span>
        <span class="grow"></span>
        <button class="btn btn-sm btn-ghost">Edit style</button></div>
      <div class="ed-b">
        <p>Relocation Stress Syndrome was formally recognized as a clinical diagnosis by the North
        American Academy of Psychiatry. Studies by researchers Kao, Travis, and Acton (2004)
        demonstrated that moving frequently triggers measurable psychological strain.</p>
      </div>
    </div>

    <div class="ed" style="margin-top:12px">
      <div class="ed-h"><span class="lvl mono">Heading 2</span>
        <span class="strong small">Why the Moving Process Exhausts the Brain</span>
        <span class="grow"></span>
        <button class="btn btn-sm btn-ghost">Edit style</button></div>
    </div>

    <div class="ed" style="margin-top:12px">
      <div class="ed-h"><span class="lvl mono">List Paragraph</span>
        <span class="muted small">3 items</span>
        <span class="grow"></span>
        <button class="btn btn-sm btn-ghost">Edit style</button></div>
      <div class="ed-b">
        <ul>
          <li><strong>Empathetic and Specialized Staffing:</strong> For feminine attire, personal items,
              and delicate keepsakes, our teams are trained to handle what general movers will not.</li>
          <li><strong>Complete Technical Relief:</strong> Our trained handyman movers manage the heavy
              lifting, furniture disassembly, and reassembly.</li>
          <li><strong>Verified Quality Standards:</strong> As a proudly FIDI accredited international
              mover, our audited operations meet global benchmarks.</li>
        </ul>
      </div>
    </div>

    <div class="ed" style="margin-top:12px">
      <div class="ed-h"><span class="lvl mono">Swan CTA</span>
        <span class="grow"></span>
        <span class="muted xs">rendered as a button, not body copy</span></div>
      <div class="ed-b">
        <p>Ready to experience a calm, seamless move? Request a Proposal today or call us at 800-Emovers.</p>
        <a class="btn btn-primary btn-sm" href="#">Request a Proposal</a>
      </div>
    </div>
  </div>

  <div>
    <div class="card">
      <div class="card-h"><h3>Outline</h3></div>
      <div class="card-b">
        <ul class="outline">
          <li><span class="grip">$(Ico 'grip' 12)</span><span class="lvl">H1</span><span class="l-1">What is Relocation Stress&hellip;</span></li>
          <li><span class="grip">$(Ico 'grip' 12)</span><span class="lvl">body</span><span class="l-2">Intro (2 paragraphs)</span></li>
          <li><span class="grip">$(Ico 'grip' 12)</span><span class="lvl">H2</span><span class="l-1">The Clinical Reality&hellip;</span></li>
          <li><span class="grip">$(Ico 'grip' 12)</span><span class="lvl">H2</span><span class="l-1">Why the Moving Process&hellip;</span></li>
          <li><span class="grip">$(Ico 'grip' 12)</span><span class="lvl">H2</span><span class="l-1">Defeat Relocation Stress&hellip;</span></li>
          <li><span class="grip">$(Ico 'grip' 12)</span><span class="lvl">list</span><span class="l-2">3 service pillars</span></li>
          <li><span class="grip">$(Ico 'grip' 12)</span><span class="lvl">CTA</span><span class="l-2">Request a Proposal</span></li>
        </ul>
        <p class="footnote">Drag to reorder. Levels cannot skip: an H1 cannot be followed straight by an H3.</p>
      </div>
    </div>

    <div class="card" style="margin-top:16px">
      <div class="card-h"><h3>Readability</h3></div>
      <table class="tbl">
        <tbody>
          <tr><td>Words</td><td class="right strong">618</td></tr>
          <tr><td>Reading time</td><td class="right strong">3 min</td></tr>
          <tr><td>Grade level</td><td class="right strong">Grade 11</td></tr>
          <tr><td>Passive sentences</td><td class="right strong">4</td></tr>
        </tbody>
      </table>
    </div>

    <div class="card" style="margin-top:16px">
      <div class="card-b">
        <h3 style="font-size:15px;margin-bottom:8px">Editorial note</h3>
        <p class="small muted" style="margin:0">The original paragraph 10 note
        (<em>"(Video could be IIM A campus)"</em>) was on article 1 and has already been removed. If an
        author leaves a note like this in again, it is flagged here rather than published.</p>
      </div>
    </div>
  </div>
</div>
"@
