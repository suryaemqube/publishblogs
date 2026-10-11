# prototype-simple/

Same writer journey as `../prototype/`, stripped of the explanatory copy and
secondary actions. The HTML files are generated — do not edit them by hand.

Regenerate after any change to `../prototype/` with:

```
powershell -File tools\simplify-prototype.ps1
```

`tools/simplify-prototype.ps1` copies `prototype/` to `prototype-simple/`,
applies the rules below, and preserves this README across runs. Every page,
link target, fixture number and screen layout is identical to the original;
only the following changes are made.

## What was removed, and why

| # | Rule | What it strips | Why it is in the simple version |
|---|------|----------------|---------------------------------|
| 1 | `note-div` | `<div class="note">` blocks (e.g. "The publisher keeps the status code and the request id…", "A file that fails any of these is rejected, not repaired…") | Production/policy asides for the dev team, not for the writer using the screen |
| 2 | `footnote-p` | `<p class="footnote">` (e.g. login's "Both sign-in options are shown because decision A is still open…") | Explains prototype decisions to reviewers, says nothing to a user |
| 3 | `hint-p` | `<p class="hint">` (e.g. "Advisory. A short article is flagged in review…") | Advisory copy that duplicates what the screen already shows |
| 4 | `alert-warn` | Yellow "Advisory/category cannot be verified/thin article" banners | The state is already visible in the table above the banner; the banner is a lecture |
| 5 | `decision-p` | `<p>` paragraphs naming a "Decision A/G/H" | Open-decision explanations for the planning team, removed from every user-facing page |
| 6 | `advisory-li` | The "Advisory" row in the failure-triage legend | Third copy of the same idea on the same page |
| 7 | `advisory-row` | The trailing "See decision H." in the triage table | Cross-reference to a planning doc |
| 8 | `advisory-d` | `<div class="d">Advisory only - …</div>` under the slug check | Redundant given the advisory chip |
| 9 | `checks-explain` | The `<div class="d">` paragraph under each "What gets checked" title (documents-new only) | Names stay; the justification paragraph is removed. Other pages keep their `.d` text — there it is real status data, not instruction |
| 10 | `split-actions` | `Rename`, `Drop`, `Move boundary up`, `Move boundary down` buttons on the split review page | Lowest-value mouse actions; `Open editor` and `+ Merge with the article below` remain |
| 11 | `nav-product` | Product wordmark beside the logo | Brand text beside the logo adds noise; the logo is enough |

## Deliberately kept

- `first-run.html`'s yellow "emovers.com is not connected yet" banner — that is
  live status, not advice.
- The "3 blocking · 2 advisory" summary chip on document-rejected — it is data.
- All primary-path buttons and every page link.

## If the simple version looks wrong, check in this order

1. Was the source changed and `tools\simplify-prototype.ps1` not re-run?
2. Did a rule regex need a new `[^>]*` allowance for an added attribute?
   (Most rules already tolerate extra attributes; `advisory-row` and
   `checks-explain` are the literal ones.)
3. The `decision-p` rule deletes a whole `<p>`; if a user-facing paragraph ever
   needs to mention a decision letter, reword it or narrow the rule.
4. Regex pitfall already hit once: `<p[^>]*>` also matches `<path …>` SVG
   tags, which ate buttons and whole panels on pages containing both. Always
   anchor paragraph rules as `<p(?:\s[^>]*)?>`.
