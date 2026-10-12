# Swan Content Publisher — Plan v2.1

Supersedes v1. This revision restructures the data model, closes gaps at the seams
between the three integration types, and re-sequences the work.

v2.1 adds **§0.1 Verified findings from client data**, gathered by probing the five live
WordPress installs over the public REST API and by inspecting the reference document
`EmoversUAE-Sep-Blogs.docx`. Seven of this plan's assumptions were wrong. Sections
5.2, 6, 8.3, 12 and 14 have been amended to match.

---

## 0. What changed from v1, and why

| # | Change | Reason |
|---|--------|--------|
| 1 | Target **.NET 10** LTS, not .NET 8 | .NET 8 support ends **10 Nov 2026** (~6 weeks from writing). v1 would have started the product on a platform inside its final support quarter. |
| 2 | Split `Article` → `Article` + `ArticlePublication` | v1's Article pointed at one ClientSite. Writers need one article across many sites. Retrofitting this is a data migration; doing it now is a schema choice. |
| 3 | Added `ContentFormat: Classic \| Blocks` per site | Clean HTML POSTed to a block-editor site opens in Gutenberg as one unreadable "Classic" block. Breaks the client's own editors. |
| 4 | Added `PublishInProgress` and `PartialFailure` states | v1's own error example — *"post published, frontend deploy failed"* — is half-published, which is neither success nor `Failed`. |
| 5 | Added optimistic concurrency (`RowVersion`) + idempotency keys | v1 had no concurrency control. Two users clicking Publish on the same approved article creates two remote posts. |
| 6 | Gatsby preview specified as a **request-time** route | `gatsby-source-wordpress` is build-time; drafts never exist in a static build. A static-only Gatsby site structurally cannot preview a draft. |
| 7 | One scheduler owns the clock (app-side) | v1 had both a background job processor and WordPress scheduling available. Two schedulers = double publish. |
| 8 | `SiteCredentials` / `SitePreviewSettings` / `SitePublishingSettings` → `SiteIntegrations` + `SiteSecrets` | Three joins to render one settings screen. Secrets now sit in one row per secret, enabling per-secret rotation, revocation, and last-used auditing. |
| 9 | Added `SiteEndpoints` | A client site has several addresses (WP REST, WPGraphQL, frontend, preview, CDN). v1's single base URL can't express that. |
| 10 | Added `SiteFieldMappings` + `ICustomFieldProvider` | ACF and similar are where real client sites break. v1 would publish posts with every required custom field blank. |
| 11 | Added accessibility module | v1 had one alt-text warning. Content is published to real client websites. |
| 12 | Added NFRs, testing strategy, observability | Absent from v1 entirely, and they change architecture decisions. |
| 13 | Workstreams re-ordered; adapter interfaces land in Foundation | v1 put the editor (Workstream 3) behind two integration-heavy workstreams. |
| 14 | Dropped `PublishingLogs` | Redundant with `PublishingAttempts` + `AuditLogs`. |
| 15 | 15 features deferred to post-v1 | Listed in §14. |

### 0.1 Verified findings from client data

Gathered 29 Sep 2026 by (a) reading `/wp-json/wp/v2/types` and `/wp-json/wp/v2/taxonomies`
unauthenticated on all five WordPress installs, and (b) unpacking
`EmoversUAE-Sep-Blogs.docx` and analysing its OOXML. **These are observations, not assumptions.**

#### A. Site inventory — verified

| Client | WP origin | Public host | CPT key | `rest_base` | Taxonomies registered | Frontend |
|--------|-----------|-------------|---------|-------------|------------------------|----------|
| emqube.com | `wp.emqube.com` | `emqube.com` | `blogs` | `blogs` | `blog_cat` | Gatsby |
| swansol.com | `app.swansol.com` | `swansol.com` | `blog` | `blog` | **none on the CPT** | Gatsby |
| allaboutvat.com | `www.allaboutvat.com` | same | `announcements` ("News Updates") | `announcements` | **none on the CPT** | WordPress |
| electricway.com | `electricway.com` | same | `insights` | `insights` | `category` **and** `insights_category` | WordPress |
| emoversuae.com | `emoversuae.com` | same | `move-on` | **not exposed** | `blog` — **not exposed** | WordPress |

emoversuae's `wp-sitemap.xml` also lists three further post types, all equally invisible to REST:
`happenings`, `issues`, and `stories` (with `stories-category` / `stories-tags`), plus the
`issues-cat` taxonomy. This is a consistent, deliberate restriction rather than a single oversight.

Five consequences, all of which break v1 and parts of v2:

1. **The WordPress origin is not the public host** on emqube and swansol. `emqube.com/wp-json`
   returns 404 entirely. Every REST and GraphQL call must target the origin from
   `SiteEndpoints`, never a hostname derived from the client's public domain. This validates
   the `SiteEndpoints` split added in v2.
2. **No site uses the default `post` type.** The CPT key is arbitrary and per-site
   (`blogs`, `blog`, `announcements`, `insights`, `move-on`) and frequently differs from the
   display name ("News Updates" → `announcements`). `SiteIntegrations` now carries
   `PostTypeKey` and `RestBase` explicitly (§5.2). Nothing in the codebase may default to `post`.
3. **Taxonomies are custom and sometimes absent.** `blog_cat` on emqube; both `category` and
   `insights_category` on electricway (a duplicate-taxonomy trap — Swan must be told which is
   authoritative, and syncing to the wrong one is silently invisible); and on swansol and
   allaboutvat the blog CPT has **no registered taxonomy at all**, so "categories" there must
   be an ACF field rather than a WordPress term. One taxonomy model does not cover this fleet.
4. **emoversuae cannot be published to over REST today.** The `move-on` CPT and its `blog`
   taxonomy are both publicly registered — they appear in `wp-sitemap.xml` and
   `?post_type=move-on` serves a real archive — but **neither is exposed in REST**.
   `/wp/v2/types` does not list `move-on`, and `/wp/v2/taxonomies` does not list `blog`. The same
   is true of `happenings`, `issues` and `stories`. The REST adapter as specified **cannot publish
   to this client**. See §0.1.D — this needs a decision before any adapter is built.

#### A2. The `moveon` registration snippet does not match production, and is incomplete

Snippet supplied for review:

```php
register_post_type('moveon', array(
    'labels'     => array('name' => 'Move On', 'sigular_name' => 'Move On'),
    'public'     => true,
    'supports'   => array('title','editor','comments','thumbnail','custom-fields','post-formats'),
    'taxonomies' => array('blog'),
    'show_in_rest' => true,
    'has_archive' => true,
));
```

Four discrepancies, all of which matter:

| # | Snippet | Production | Consequence |
|---|---------|-----------|-------------|
| 1 | post type key `moveon` (no hyphen) | `move-on` (hyphen), per `move-on-sitemap.xml` and `?post_type=move-on` | The snippet as written would register a **second, different** post type. Admin URLs in the existing team's bookmarks and any saved links use `move-on` |
| 2 | `show_in_rest => true` | **not REST-exposed** | The snippet is a *proposed* change, not deployed code. Confirm whether it is merged |
| 3 | taxonomy `blog` | `blog` — matches, but **also not REST-exposed** | **The snippet does not fix the taxonomy.** `register_taxonomy('blog', ...)` needs its own `show_in_rest => true`, otherwise categories can be neither read nor assigned over REST even after the CPT is fixed |
| 4 | — | CPT supports `thumbnail` | Featured image is a first-class remote field on this client, reinforcing §6.7 |

Fixing the post type alone will **not** make emoversuae publishable. Both the CPT and the `blog`
taxonomy must be REST-exposed, and that is a change on the client's site, outside Swan's control.
Until it ships, emoversuae needs a different integration path or stays manual.

#### A3. The corrected client-side change

> **Correction (12 Oct 2026):** the `move-on` taxonomy is **`stories-category`**, not `blog`. The category
> filter on the live site (`edit.php?stories-category=…&post_type=move-on`) uses it, and it is shared with the
> `stories` post type. `show_in_rest => true` must be set on `stories-category`. Live state after the client's
> first change: `move-on` is REST-readable (200, rest_base `move-on`), but the type lists no attached taxonomies
> and `stories-category` returns 403, so it is still not readable. The `blog` steps below do not apply to this client.

Per decision L, the existing `move-on` registration and all URLs on it are preserved. The only
client-side change is adding `'show_in_rest' => true` to the two registrations that already exist.
**Do not add the `register_post_type` call from the snippet** — that is what would create the
duplicate.

```php
// 1. In the existing move-on register_post_type() call, ensure this key is present:
   'show_in_rest' => true,

// 2. In the existing register_taxonomy('blog', ...) call, ensure this key is present:
   'show_in_rest' => true,
   'rest_base'    => 'blog',
```

Nothing else changes: same post type key `move-on`, same `blog` taxonomy, same archive, same
`?post_type=move-on` URLs, same admin bookmarks. `rest_base` is stated explicitly rather than left
to default so Swan's `SiteIntegrations.RestBase` is not a guess.

| | Snippet as supplied | Corrected change |
|---|---|---|
| New post type registered | `moveon` — **yes, wrongly** | **none** |
| CPT made REST-visible | `move-on` | `move-on` |
| Taxonomy made REST-visible | `blog` — **missing** | `blog` |
| Existing URLs affected | all of them | **none** |

**Verification after deploy** — Swan's connection test fails loudly if this has not shipped, and
these two calls must return 200:

```
GET /wp-json/wp/v2/types/move-on
GET /wp-json/wp/v2/taxonomies/blog
```

Until both return 200, the emoversuae adapter is not buildable and publishing to that client stays
manual.

Measured 29 Sep 2026, unauthenticated: `/wp/v2/types/move-on` returns **401 `rest_cannot_read_type`**
and `/wp/v2/taxonomies/blog` returns **401 `rest_forbidden`** — not 404, because the type and taxonomy
are registered but withheld from REST rather than absent. `move-on` is absent from `/wp/v2/types`,
and `blog` is absent from `/wp/v2/taxonomies` (which lists only `category`, `post_tag`, `nav_menu`,
`dlm_download_category`, `dlm_download_tag`, `wp_pattern_category`). So the post-deploy assertion is
**200, not "no longer 404"** — and a test that only checked for non-404 would pass today.

This is also a useful negative test: the same calls for `happenings`, `issues` and `stories` should
keep returning 401 after the change, confirming it was scoped to the one post type Swan needs rather
than opening up the whole site. Note `/wp/v2/categories` *is* reachable on emoversuae (9 terms) — the
core `category` taxonomy is public even though the CPT's own `blog` taxonomy is not, so term
enumeration is not uniformly broken there.

#### A4. What the authenticated probe settled — decisions I and J

`tools/probe-connection.ps1` was run against all five origins on 29 Sep 2026. Two decisions are now
closed on evidence, and one is now closed as *definitively blocked* rather than merely open, which is
a more useful answer than "we don't know".

**Decision I — RESOLVED. `category` is authoritative on electricway.**

Both taxonomies are attached to the `insights` post type, so attachment cannot decide it. What
decides it is which one the editors actually use:

| Taxonomy | rest_base | Attached | Payload key | Posts with terms | Terms |
|----------|-----------|----------|-------------|------------------|-------|
| `category` | `categories` | yes | `categories` | **20 / 20** | 20 |
| `insights_category` | `insights_category` | yes | `insights_category` | 0 / 20 | 0 |

20/20 against 0/20 is not a judgement call. `insights_category` is registered-but-dead: it will
accept writes from Swan, and every write will go somewhere the site never displays. That is precisely
the silent failure the plan warned about, and it is now avoidable by configuration.

**Decision J — BLOCKED, with the unblock path known.**

| | swansol `blog` | allaboutvat `announcements` |
|---|---|---|
| Attached taxonomies | none | none |
| `acf` key in payload | present, empty | present, empty |
| `_acf_changed` meta | absent | **present** |
| Non-core field keys | none | none |
| Field values over REST | none | none |

`_acf_changed` is written by ACF itself, so on allaboutvat its presence **proves a field group is
attached to this post type** while withholding its values. That is stronger than the v2 assumption of
"almost certainly ACF" — it is ACF — but the field *names* and *types* remain unreadable over REST.
swansol is weaker: an `acf` key with no values and no `_acf_changed`, which is also consistent with a
field group that exists but has never been saved against a published post.

**Consequence for v1 scope:** Swan can ship electricway's category picker. It **cannot** ship one for
swansol or allaboutvat. Rather than block the whole build on a 10-minute wp-admin question, the plan
is: read the field group name and types from wp-admin, then store them as `SiteFieldDefinitions`
exactly as §8.3 already specifies. If they are `TaxonomyReference` fields, the term list comes from
`SiteTaxonomyRegistrations`; if they are `MultiSelect` of hard-coded strings, the picker is a static
list. Both paths are already modelled — only the configuration is missing.

**Two implementation traps this exposed, both now in the probe:**

1. **`rest_base` is not derivable from the taxonomy name.** `category` → `categories`, not
   `categorys`. Probing `/wp/v2/category` returns 404 and looks like the taxonomy is missing. The
   route must be read from `/wp/v2/taxonomies`, which is why `SiteTaxonomyRegistrations.RestBase` is
   now a stored column.
2. **The payload key is not the taxonomy name either** — `category` appears as `categories` on the
   post. Reading the wrong key returns an empty array and the taxonomy looks unused.

**A measurement caveat worth recording.** Windows PowerShell 5.1's default `Expect100Continue` and
2-connection limit made `Invoke-WebRequest` fail against these origins with "the connection was closed
unexpectedly" on the *second and subsequent* requests — which surfaces as a false 404 or a transport
error and is indistinguishable from the site being broken. Early in this probe, swansol and
electricway reported 404 for `/wp/v2/taxonomies` and both were reachable. Every figure in this section
was re-measured with `Expect100Continue = $false` and a 64-connection limit, and re-verified twice. The
shipped .NET adapter uses `HttpClient` and is unaffected, but the connection test in §8.4 must not be
written against a client with this default.

#### B. The reference document — verified

`EmoversUAE-Sep-Blogs.docx`, 31 KB, 7 pages, 2,177 words, authored by Mohammed Sutarwala.

| Property | Value | Design consequence |
|----------|-------|--------------------|
| Named styles applied | **0** | Style-based heading extraction returns nothing |
| Heading styles defined | all 9, plus Title, Subtitle | Writers have the styles available; they do not use them |
| `<w:outlineLvl>` | 0 | No structural intent recorded anywhere |
| Direct `<w:b/>` | 71 runs | **Bold is the only heading signal in the file** |
| Direct `<w:sz>` / `<w:color>` | 0 / 0 | No size-based fallback available either |
| Paragraphs | 72 (66 non-empty) | — |
| Tables | 0 | Table handling is untested against real input |
| Images | **0** (no `word/media/`) | Featured image is not supplied by the document |
| Hyperlinks | 18 | 14 point at 6 URLs; 3 are broken |
| Lists | 9 paragraphs, 4 `numId`s — **all bullets** | Real lists and hand-typed numbering coexist |
| `dc:title` | **empty** | Title must come from the first paragraph, never metadata |

Five further findings, all from the actual content:

1. **One file contains four separate articles**, not one. The paragraphs resolve into:
   an internal "E-Movers TL Article" section (paras 1–11, an IIM Ahmedabad case study),
   "What is Relocation Stress Syndrome" (13–31), "Modern Renting in Dubai" (32–54), and
   "Why E-Movers Service Continues to Earn 5-Star Reviews" (55–72). The filename
   ("Sep **Blogs**") confirms the monthly batch. **v1 and v2 both assumed one document equals
   one article; §6 is rewritten for N articles per document.**
2. **Internal labels are mixed into the content.** Para 1 "E-Movers TL Article" and para 12
   "BLOGS" are section markers for the writer, not publishable copy. Ingested naively they
   become the H1 and a stray H2.
3. **A writer's note is in the body.** Para 10: "(Video could be IIM A campus)". This is a
   production instruction and must never reach the published page.
4. **Headings and bold lead-ins are indistinguishable by bold alone.** Heading paragraphs are
   fully bold, but so are ~20 body paragraphs that open with a bold run-in phrase and continue
   in regular text — e.g. "**Thorough Qualification:** The process began with a detailed
   questionnaire…" (para 4), "**Zero Monthly Markup:** The total annual rent remains exactly the
   same…" (para 38). A rule of "starts with bold → heading" would wrongly promote these to H3
   and wreck the articles. §6.3 defines the run-in test that separates them.
5. **Three hyperlinks are broken, identically.** Links 1, 8 and 12 all point at
   `https://www.google.com/search?q=https://emoversuae.com/moving-homes/home-relocation/%23lady_packers`
   — a Google search URL with the real destination pasted in as the query and `#` percent-encoded.
   The anchor text reads "Lady Packers", so the intent was a `#lady_packers` fragment on the
   home-relocation page. This is a real, recurring authoring error, and it validates the
   "broken external link" rule in §8.5 as a check writers will genuinely use.
6. **CTA blocks recur at the end of every article** (paras 30, 53, 71 — "Request a Proposal",
   "call us at 800-Emovers"), always linking the same two URLs. This is a structured content
   pattern, not prose, and should round-trip as a CTA block rather than three separate
   hand-maintained copies.
7. **Hand-typed numbering coexists with real Word lists.** Paras 58–67 are bold headings reading
   "1. Clear Financial Value" … "5. Practical Service Innovations" as literal text, while paras
   26–28 and 38–46 are genuine Word bullet lists. Converting the typed numbers to real lists
   must not leave a literal "1." in the text.

#### C. What this changes, in order of severity

| # | Finding | Change made |
|---|---------|-------------|
| 1 | 4 articles in one document | §6 rewritten: split before convert; new `/api/documents/{id}/split-preview` |
| 2 | Zero named styles | **Resolved by decision — writers apply the house style.** §6.3 is now a style→HTML mapping with no inference; non-conforming input is rejected. The heuristic layer is cut, not built |
| 3 | Zero images | Featured image is a **required manual input** with a per-client default, not a validation error (§14.1) |
| 4 | Arbitrary CPTs, one not REST-exposed | `PostTypeKey` + `RestBase` in `SiteIntegrations`; REST-exposure check in connection test (§5.2, §8.4). `show_in_rest => true` to be enabled client-side |
| 5 | Custom or absent taxonomies | `SiteTaxonomies` gains `IsAuthoritative`; ACF taxonomy-reference fields handled in §8.3 |
| 6 | ACF confirmed on the fleet | `SiteFieldMappings` upgraded from string templates to **typed** field definitions (§5.2, §8.3) |

### v2.1 changes — driven by verified client data

| # | Change | Reason |
|---|--------|--------|
| 16 | §6 rewritten: **one document yields N articles**, with writer-reviewable split boundaries | `EmoversUAE-Sep-Blogs.docx` contains 4 articles. v1 and v2.0 both assumed 1:1 |
| 17 | Heading detection rebuilt around **fully-bold + run-in classification** | The document applies **zero** named styles and records **zero** outline levels. Style-based extraction returns nothing |
| 18 | Added boilerplate / writer's-note / CTA extraction stages | The document body contains section labels ("BLOGS"), a production note, and 3 recurring CTA blocks |
| 19 | `SiteIntegrations` gains `PostTypeKey`, `RestBase`, `DefaultFeaturedMediaId` | No site uses the default `post` type; the reference document has no images |
| 20 | `SiteFieldMappings` **replaced** by typed `SiteFieldDefinitions` + `SiteFieldValues` | String templates cannot express a required boolean, multi-select, or taxonomy reference — all three occur on the fleet |
| 21 | `SiteTaxonomies` gains `IsAuthoritative` | electricway registers both `category` and `insights_category` on `insights`; syncing to the wrong one fails silently |
| 22 | Connection test now **probes REST exposure of the actual post type** | `move-on` is absent from emoversuae's `/wp/v2/types` — risk R1 |
| 23 | Added `/api/documents/{id}/split-preview` and `/split-accept`; `/api/articles/{id}/field-values` | Backs the new split-review screen and typed fields |
| 24 | Open decisions **D** answered, **H/I/J/K/L** added | Seven assumptions replaced by evidence; four new questions the data raised |
| 25 | **Heuristic layer removed from v1 scope.** Extraction is a style→HTML mapping only | Decision: writers apply the house style, so structure is stated, not inferred. Cuts the run-in classifier, confidence scoring, and structure-review gate from the build |
| 26 | Non-conforming input is a **hard reject** at upload, plus an audited "import anyway (unverified)" override | A reject is only acceptable when the template is one click away. The override covers genuine deadlines without reintroducing inference |
| 27 | New **§6.4 house style and template** — `Swan Label` / `Swan Note` / `Swan CTA` styles plus a downloadable `Swan-Article-Template.docx` | Conforming input has to be the *easy* input. Custom styles remove the "BLOBS" label and "(Video could be IIM A campus)" note from the converter entirely, and `Title` / `Heading 1` make multi-article splitting deterministic |
| 28 | New **§0.1.D** comparing the `moveon` snippet to production | It uses the wrong post type key, and omits `show_in_rest` on the `blog` taxonomy — so deploying it as-is would register a duplicate CPT and still leave categories unwritable |
| 29 | emoversuae corrected: `move-on` **is** publicly registered, just not REST-exposed; three further CPTs found | `wp-sitemap.xml` shows `move-on`, `happenings`, `issues`, `stories`. The restriction is consistent and deliberate, not a one-off oversight |
| 30 | R1 closed | `show_in_rest => true` confirmed as the plan, client-side. Connection test re-verifies after deploy |
| 31 | **Decision I closed by measurement**: `category` on electricway, not `insights_category` | 20/20 sampled posts carry `categories`; 0/20 carry `insights_category` (0 terms exist). The latter is registered-but-dead and would silently swallow every write |
| 32 | New **`SiteTaxonomyRegistrations`** table; `SiteTaxonomies.SiteId` → `SiteRegistrationId` | The taxonomy *registration* and its *terms* are different things. `rest_base` is not derivable from the name (`category` → `categories`), and a taxonomy can be attached yet empty — neither was expressible before |
| 33 | Decision J reclassified from "open" to **"blocked with a known unblock"** | `_acf_changed` on allaboutvat proves an ACF field group is attached but withholding values. Category pickers for swansol/allaboutvat are explicitly out of v1 scope until wp-admin confirms the fields |
| 7 | Labels, notes, CTAs in the body | New conversion stages for boilerplate stripping and CTA extraction (§6.2) |

### Open decisions — these need your answer before code

| # | Question | Status / recommendation |
|---|----------|----------------|
| A | Is the content team on Microsoft Entra ID? | **Open.** If yes, use OIDC SSO — deletes password reset, invite flow, and most of the email settings surface. |
| B | Do any clients use ACF or other required custom fields? | **Answered — yes, across the fleet.** allaboutvat: `Is Sticky`, `Trending`, `Countries`. §8.3 is now core v1 scope, not deferred. |
| C | Do articles ever need to publish to multiple sites at once? | **Open, but recommended yes** — build `ArticlePublication` regardless; it is cheap now and a migration later. |
| D | Which email provider? | **Open.** SendGrid / Postmark / Graph, not SMTP. |
| E | Article volume, writer count, client-site count? | **Open.** Now known to be ≥6 sites. Decides sync vs queued conversion, polling vs webhooks, server-side paging. §13.1. |
| F | .docx retention policy? | **Open.** Originals may contain unpublished material. Now higher stakes: a single original maps to *N* articles (§6.1). |
| G | Approvals: named approvers per client, or any reviewer? | **Open.** Affects the review queue's data model. |
| H | Will emoversuae expose `move-on` **and** its `blog` taxonomy to REST? | **RESOLVED** — `show_in_rest => true` will be enabled. §0.1.D: the flag is needed on **both** registrations, and the snippet's post type key must be corrected to `move-on`. |
| I | On electricway, is `category` or `insights_category` authoritative? | **ANSWERED — `category`, by measurement not opinion.** Both are attached to `insights`, so attachment proves nothing. Sampling 20 published posts: **`category` 20/20 carry terms (20 terms exist); `insights_category` 0/20 and 0 terms.** `insights_category` is registered-but-dead. Sync to `category` only, and never auto-pick. §0.1.E, `SiteTaxonomyRegistrations`. |
| J | On swansol and allaboutvat, where are "categories" stored, given the CPTs have no taxonomy? | **Refined — still blocked, and now precisely characterised.** Both CPTs genuinely have zero attached taxonomies, and neither exposes any field value over REST. allaboutvat **does** ship `_acf_changed` meta, which proves an ACF field group is attached but withholding its values; swansol returns an `acf` key with nothing in it. So the assumption "almost certainly ACF" is confirmed as *likely* but the field names and types are still unreadable. **Swan cannot build a category picker for either site as they stand.** Closes with either `show_in_rest` on the ACF field group, or 10 minutes in wp-admin. §0.1.E. |
| K | Will the content team adopt the house style (§6.4)? | **RESOLVED — yes.** Extraction is Structured-only; the heuristic layer is cut from v1. |
| L | Is the `moveon` snippet intended to replace the current `move-on` registration, or be a new post type alongside it? | **RESOLVED — keep what exists.** The current `move-on` registration and every URL built on it stay exactly as they are. The snippet is **not** deployed: no new post type is registered. Client-side change is limited to adding `show_in_rest => true` to the two existing registrations. §0.1.D. |
| M | When can the September batch be re-delivered in the template format as a pilot? | **Effectively answered — the template is built and contains the September articles already reformatted.** §6.4.1. A conforming document now exists; the team deletes the two `INSTRUCTIONS` blocks and uploads. Formal sign-off still wanted before the pilot is treated as the team's accepted output. |

---

## 1. Project overview

**Name:** Swan Content Publisher

**Objective:** one secure application where the content team selects a client site, uploads a
`.docx`, converts it to clean web content, adds SEO and media, previews it in the client's real
frontend design, submits for review, then publishes immediately or on a schedule — across standard
WordPress, headless WordPress + Gatsby, and Netlify-hosted frontends.

### 1.1 Integration types

| Type | Content store | Frontend | Preview mechanism |
|------|---------------|----------|-------------------|
| **A. Standard WordPress** | WordPress | WordPress theme | Authenticated WP draft + WP preview |
| **B. Headless WP + Gatsby** (e.g. emqube.com) | WordPress | Gatsby (build-time) | Signed preview route rendering the WP draft through the live blog React components |
| **C. Netlify-hosted** (Gatsby/React/other) | WordPress or Swan | Static site on Netlify | Protected preview route on the deployed frontend |

**Rule that applies to all three:** the WordPress draft is the single canonical staging copy. Every
preview and every publish reads from it. Non-WordPress-content-store setups defer this to the
Swan `Article` as canonical and treat the site as a pure deployment target.

---

## 2. Architecture

```
  Content Writer
        |
        v
  Swan Content Publisher
  Angular 22 + Material
        |
        v
  ASP.NET Core 10 Web API
        |
        +---------------------------+
        |                           |
        v                           v
   SQL Server                  File Storage
   content, workflow,          original .docx,
   publications, audit         images, revisions
        |
        v
  Publishing & Preview Adapter Layer
        |
   +----+-----+----------+----------+-----------+
   |          |          |          |           |
   v          v          v          v           v
 WordPress  Gatsby     Netlify    Taxonomy    SEO /
 Adapter   Adapter     Adapter    Provider   Accessibility
   |          |          |          |           |
   v          v          v          v           v
 WP REST    WPGraphQL  Build      WP terms   Yoast/RankMath/
 +Preview   +Function   hooks     +authors   headless meta
```

**The adapter layer is the whole design.** Every client-specific behaviour — auth, content format,
taxonomy mapping, SEO field names, preview rendering, deploy triggering — is an implementation
behind an interface, configured by a `SiteIntegrations` row. No client-specific `if` statements in
business logic.

---

## 3. Technology stack

| Layer | Choice | Notes |
|-------|--------|-------|
| Frontend | Angular 22+, Angular Material | TipTap or TinyMCE editor. Reuse the calendar component for scheduling. |
| Backend | ASP.NET Core 10 (LTS) | Web API. Background worker host for scheduled jobs and retries. |
| ORM | EF Core 10 | SQL Server provider. |
| Scheduling | Quartz.NET or Hangfire with SQL-backed store | Job store must be shared across instances. See §9.3. |
| DOCX | Open XML SDK | Direct package access; no external conversion service needed. |
| Credentials | ASP.NET Core Data Protection + envelope encryption | Per-secret key version for rotation. §11.2. |
| Secrets store | Azure Key Vault (prod) / encrypted DB (dev) | DPAPI on Windows for single-box dev. |
| Observability | Serilog + OpenTelemetry, Sentry optional | Structured logging, per-adapter `/health`. §13.4. |
| Database | SQL Server 2022 | Rowversion for concurrency. |
| Storage | Azure Blob Storage (prod) / local filesystem (dev) | Behind `IFileStore`. |

---

## 4. Solution layout

```
SwanContentPublisher.sln
├── src/
│   ├── Swan.Api/                    ASP.NET Core host, DI, middleware
│   ├── Swan.Domain/                 Entities, enums, state machine, domain rules
│   ├── Swan.Infrastructure/         EF Core, storage, email, background jobs
│   ├── Swan.Adapters/
│   │   ├── Swan.Adapters.Abstractions/   IPreviewProvider, IPublisher, ISeoProvider, ...
│   │   ├── Swan.Adapters.WordPress/
│   │   ├── Swan.Adapters.Gatsby/
│   │   ├── Swan.Adapters.Netlify/
│   │   └── Swan.Adapters.Custom/
│   ├── Swan.Docx/                   Open XML extraction + conversion rules
│   └── web/
│       └── Swan.Web/                Angular app
├── tests/
│   ├── Swan.Domain.UnitTests/
│   ├── Swan.Docx.UnitTests/
│   ├── Swan.Api.IntegrationTests/   WebApplicationFactory + Testcontainers
│   ├── Swan.Adapter.ContractTests/  per-adapter, against a real WP fixture
│   └── e2e/                         Playwright
├── fixtures/
│   └── wordpress/                   docker-compose for contract tests
└── docs/
```

---

## 5. Data model

SQL Server. Enums stored as `int` with a check constraint; all tables carry `CreatedUtc`/`UpdatedUtc`;
soft deletes via `IsDeleted` on user-owned entities.

### 5.1 Identity and access

```sql
Users            (Id, Email, DisplayName, EntraObjectId NULL, PasswordHash NULL,
                   IsActive, MustChangePassword, LastLoginUtc, CreatedUtc)
Roles            (Id, Name, Description, IsSystem)
Permissions      (Id, Key, Description)            -- e.g. 'article.publish'
RolePermissions  (RoleId, PermissionId)
UserRoles        (UserId, RoleId)
RefreshTokens    (Id, UserId, TokenHash, ExpiresUtc, RevokedUtc, CreatedUtc)
```

If decision **A** resolves to Entra ID, `PasswordHash` and `RefreshTokens` are dropped in favour of
OIDC and the invite/reset UI is removed.

### 5.2 Clients, sites, integration config

```sql
Clients        (Id, Name, Status, PrimaryContactName, PrimaryContactEmail, Notes, IsActive)

Sites          (Id, ClientId, Name, Hostname, Kind,          -- WordPress | HeadlessWordPress
                 IsActive,                                    -- StaticNetlify | Custom
                 FrontendNotes)

SiteEndpoints  (Id, SiteId, Kind,          -- WordPressRest | WordPressGraphQL | Frontend
                 BaseUrl, AuthScheme,       -- Preview | Netlify | AssetCdn
                 IsPrimary, IsActive)

-- Non-secret configuration for a site. One row per site.
-- ConfigJson sections: content, seo, preview, publishing, media, linking, template
SiteIntegrations(Id, SiteId,
                 PostTypeKey,        -- NEW §0.1. 'blogs' | 'blog' | 'announcements' |
                                       --       'insights' | 'move-on'. NEVER assume 'post'.
                 RestBase,           -- NEW §0.1. REST collection for the CPT, e.g. 'blogs'
                 ConfigJson        NVARCHAR(MAX),
                 ContentFormat,    -- Classic | Blocks
                 UseBlockEditor    BIT,
                 SeoProviderKey,   -- YoastSeo | RankMathSeo | CustomFieldsSeo | HeadlessSeo
                 PreviewProviderKey,   -- WordPress | Gatsby | Netlify | Custom
                 PublishingProviderKey,-- WordPress | HeadlessDeploy | Custom
                 TimeZone,         -- IANA, e.g. 'Asia/Qatar'
                 DateFormat, Locale,
                 DefaultFeaturedMediaId,  -- NEW §0.1. reference document has zero images
                 RowVersion, UpdatedUtc)

-- Secrets: one row per secret, individually rotatable and revocable.
SiteSecrets    (Id, SiteId, SecretType,      -- WordPressAppPassword | GraphQLToken |
                 ProtectedValue,               -- NetlifyBuildHook | NetlifyToken
                 KeyVersion, LastUsedUtc,     -- MediaApiKey
                 CreatedUtc, RevokedUtc, CreatedByUserId)

-- NEW §0.1.E. The taxonomy REGISTRATION is a separate thing from its terms.
-- Probing found the registered name and the REST route differ ('category' ->
-- rest_base 'categories'), and a taxonomy can be attached to the post type yet
-- entirely empty (electricway's 'insights_category'). Without storing this,
-- IsAuthoritative on the term rows has nothing to hang off.
SiteTaxonomyRegistrations (Id, SiteId,
                  TaxonomyKey,       -- 'category' | 'insights_category' | 'blog_cat'
                  RestBase,          -- 'categories' | 'insights_category' - the route to
                                      --   query. Usually plural, and NOT derivable from
                                      --   TaxonomyKey by simple concatenation:
                                      --   'category' -> 'categories', not 'categorys'.
                  RestRouteReachable,-- §0.1.E. False when /wp/v2/{RestBase} 404s even
                                      --   though the taxonomy is attached. Blocks the
                                      --   term picker until the client enables
                                      --   show_in_rest on the taxonomy.
                  IsAttachedToPostType,  -- from /wp/v2/types/{PostTypeKey}.taxonomies
                  IsAuthoritative,   -- §0.1.E. RESOLVED: electricway = 'category'
                                      --   (20/20 posts carry terms). 'insights_category'
                                      --   is attached but 0/20 posts, 0 terms - dead.
                  IsEmpty,           -- zero terms in the site. Treat as not-in-use.
                  PostsWithTermsRate,-- sampled published posts carrying >=1 term, '20/20'
                  LastProbedUtc, SampleSize,
                  CreatedUtc, UpdatedUtc)

SiteTaxonomies (Id, SiteRegistrationId,  -- was SiteId; terms belong to a REGISTRATION
                  Kind,           -- Category | Tag
                  RemoteId, Name, Slug, ParentRemoteId,
                  IsActive, LastSyncedUtc)


SiteAuthors    (Id, SiteId, RemoteId, Name, Slug, Email, IsActive, LastSyncedUtc)

-- NEW §0.1. Typed, not string templates. allaboutvat's CPT 'announcements' has no
-- registered taxonomy, so its categories are ACF fields — one of them ('Countries')
-- references taxonomy terms that may not exist yet.
SiteFieldDefinitions (Id, SiteId,
                 FieldName,         -- 'is_sticky' | 'trending' | 'countries'
                 Label, HelpText,
                 FieldType,         -- Text | Textarea | Number | Boolean | Select |
                                     -- MultiSelect | Date | Image | Url | Email |
                                     -- TaxonomyReference | PostReference
                 AcfFieldName,      -- ACF field key, when TargetType = AcfField
                 TaxonomySlug,      -- for TaxonomyReference
                 OptionsJson,       -- allowed values for Select / MultiSelect
                 IsRequired, IsFilterable, DisplayOrder, IsActive)

SiteFieldValues (ArticleId, SiteId, FieldDefinitionId, ValueText)  -- NEW §0.1
                 -- TaxonomyReference values are stored as term IDs, ensuring the term exists
                 -- (created via the taxonomy API if absent) before the post is written.
                 PK (ArticleId, SiteId, FieldDefinitionId)
```

`SiteFieldMappings` from v2.0 is **replaced** by `SiteFieldDefinitions` + `SiteFieldValues`. The
original `ValueTemplate` string cannot express a required boolean, a multi-select, or a
taxonomy reference, and all three occur on the current fleet (§0.1.C, finding 6).

### 5.3 Content

```sql
Articles (Id, ClientId,
          Title, Slug, Excerpt,
          ContentHtml, ContentJson,        -- Blocks/ProseMirror JSON for round-trip
          ContentFormat,                   -- effective format after site resolution
          Status,                          -- ArticleStatus
          AuthorUserId, FeaturedMediaId,
          SourceDocumentId, SourceParagraphFrom, SourceParagraphTo,   -- v2.1 §6.1
          IsUnverified, ImportedByUserId,   -- v2.1 §6.3.1 audited "import anyway" override
          RowVersion,                      -- rowversion — optimistic concurrency
          CreatedUtc, CreatedByUserId, UpdatedUtc, UpdatedByUserId, ArchivedUtc)

ArticleVersions (Id, ArticleId, VersionNo, Title, ContentHtml, ContentJson, Excerpt,
                 ChangeSummary, CreatedByUserId, CreatedUtc, RestoredFromVersionId NULL)

-- v2.1: a Document belongs to a Client, NOT to an Article. One document yields N articles (§6.1).
ArticleDocuments (Id, ClientId,
                  FileName, FileSizeBytes, ContentHash, StorageKey,
                  ConversionStatus,          -- Pending | Processing | Completed | Failed
                  ConversionReportJson,      -- §6.6
                  ConvertedUtc, ErrorMessage,
                  UNIQUE (ClientId, ContentHash))

-- v2.1: the writer's reviewable proposal. No Article row exists until these are accepted.
-- v2.1. Boundaries come from Title/Heading1/SwanLabel styles, so they are exact,
-- not inferred. SplitConfidence was removed with the heuristic layer (§6.3).
ArticleCandidates (Id, ArticleDocumentId, CandidateIndex, ParagraphFrom, ParagraphTo,
                   ProposedTitle, TitleStyle, ClassificationJson,
                   State,            -- Proposed | Accepted | Merged | Rejected
                   AcceptedArticleId NULL, CreatedUtc, ReviewedByUserId, ReviewedUtc)

ArticleSeo (Id, ArticleId, SeoTitle, MetaDescription, FocusKeyword, CanonicalUrl,
            OgTitle, OgDescription, OgImageMediaId, Robots, SchemaType,
            ValidationJson, ValidatedUtc)

ArticleAccessibility (Id, ArticleId, Score, ValidationJson, ValidatedUtc)

ArticleTaxonomies (Id, ArticleId, SiteId, Kind, RemoteTermId, Name, Slug, IsPrimary)

ReviewComments (Id, ArticleId,
                AnchorType,        -- None | TextQuote
                AnchorJson,        -- { prefix, exact, suffix }  — §7.4
                Body, ParentCommentId,
                CreatedByUserId, CreatedUtc,
                IsResolved, ResolvedByUserId, ResolvedUtc)

WorkflowActions (Id, ArticleId, FromStatus, ToStatus, Action,
                 ActorUserId, Note, CreatedUtc)
```

### 5.4 Publications — the v1 → v2 structural change

```sql
ArticlePublications (
  Id, ArticleId, SiteId,
  Status,              -- PublicationStatus, §7.2
  ContentFormat,
  RemotePostId,        -- WordPress post ID, once known
  RemoteUrl,
  RemotePreviewUrl,
  WordPressDraftId,
  ScheduleState,       -- None | Scheduled | Immediate
  ScheduledUtc,        -- UTC instant, authoritative
  ClientTimeZone,      -- IANA, copied from SiteIntegrations at schedule time
  ScheduledLocalText,  -- what the writer saw, for audit display
  PublishedUtc,
  LastErrorJson,       -- §10
  AttemptCount, MaxAttempts,
  RowVersion, UpdatedUtc,
  UNIQUE (ArticleId, SiteId)
)

PublishingJobs (
  Id, PublicationId,
  JobType,             -- DraftSync | PreviewRefresh | Publish | Unpublish | Republish
  Status,              -- Queued | Running | Succeeded | Failed | Cancelled | Dead
  IdempotencyKey,      -- UNIQUE — §9.2
  AttemptCount, MaxAttempts,
  NextAttemptUtc,
  LockedByInstanceId, LockedUntilUtc,     -- distributed lock, §9.3
  CreatedUtc, StartedUtc, CompletedUtc,
  LastErrorStage, LastErrorMessage
)

PublishingAttempts (
  Id, JobId, AttemptNo,
  Stage,               -- §10.1
  Status,              -- Started | Succeeded | Failed | Skipped
  StartedUtc, CompletedUtc, DurationMs,
  RequestJson, ResponseJson,             -- redacted
  ErrorCode, ErrorMessage,
  RemoteIdAfter                            -- ID written by this attempt
)

DeploymentLogs (
  Id, PublicationId, JobId,
  Provider,            -- Netlify
  BuildId, DeployId, BuildUrl, DeployUrl,
  State,               -- Queued | Building | Ready | Error | Canceled
  StartedUtc, CompletedUtc, PayloadJson
)
```

`PublishingLogs` from v1 is **removed** — `PublishingAttempts` covers the technical detail and
`AuditLogs` covers who-did-what.

### 5.5 Media

```sql
MediaFiles (Id, ClientId, FileName, StorageKey, MimeType, ByteSize,
            Width, Height, ContentHash,          -- SHA-256, §8.2
            AltText, Title, Caption,
            Source,                              -- Upload | ExtractedDocx | Generated
            CreatedByUserId, CreatedUtc, IsDeleted)

ArticleMedia (Id, ArticleId, MediaFileId,
              Role,        -- Inline | Featured | Gallery
              SortOrder, BlockId, CaptionOverride, LinkOverride)

RemoteMediaMappings (
  Id, MediaFileId,
  DestinationKind,   -- WordPressMedia | FrontendAsset | Cdn
  SiteId, RemoteId, RemoteUrl,
  Status,            -- Pending | Uploaded | Failed
  CreatedUtc, UpdatedUtc,
  UNIQUE (MediaFileId, DestinationKind, SiteId)
)
```

v1's media dedup was WordPress-attachment-ID only. v2 keys on content hash and on
**destination**, not site — the same asset can legitimately need to exist in the WordPress media
library *and* on a frontend CDN.

### 5.6 Preview

```sql
PreviewSessions (Id, ArticleId, SiteId, ProviderKey, State,
                 WordPressDraftId, PreviewUrl,
                 TokenId, CreatedByUserId, CreatedUtc,
                 ExpiresUtc, RevokedUtc,
                 LastAccessedUtc, AccessCount)

PreviewTokens (Id, PreviewSessionId,
               TokenHash,                    -- never store the token itself
               ExpiresUtc, RevokedUtc,
               MaxUses, UseCount, CreatedUtc)
```

### 5.7 Notifications, audit, system

```sql
Notifications (Id, UserId, Type, Title, Body, LinkUrl, ArticleId,
               IsRead, ReadUtc, CreatedUtc)

EmailRules (Id, TriggerType, RecipientFilter, TemplateKey, IsEnabled, UpdatedUtc)

AuditLogs (Id, UserId, Action, EntityType, EntityId,
           ClientId, SiteId, IpAddress, UserAgent,
           BeforeJson, AfterJson, CreatedUtc)

CredentialAccessLogs (Id, UserId, SiteId, SecretType, Action,
                      Purpose, RequestId, CreatedUtc)

SystemSettings (Key, ValueJson, UpdatedByUserId, UpdatedUtc)
```

`CredentialAccessLogs` is a separate table from `AuditLogs` because credential access is
security-sensitive: it is retained longer, alerted on, and never shown to non-admin roles.

---

## 6. Document ingestion

### 6.1 One document is a batch, not an article

The reference document `EmoversUAE-Sep-Blogs.docx` contains **four** articles, and the filename
pattern ("Sep **Blogs**") shows this is the client's monthly delivery format. v1 and v2.0 both
assumed one document equals one article. The correct model is:

```
Document  1 ──1──n──▶  ArticleCandidate  ──1──1──▶  Article
   (immutable original)   (proposed split)         (created on accept)
```

The writer uploads one file, Swan proposes a split, the writer corrects the boundaries and titles
in a review screen, and only then are `Article` rows created. The original document is retained and
every article records which `ArticleDocument` and paragraph range it came from, so the batch can be
re-split or re-converted when a client asks for a change.

Detected boundaries in the reference document:

| Range | Article | Declared by |
|-------|---------|-------------|
| paras 1–11 | IIM Ahmedabad case study | `Title` |
| paras 12–31 | What is Relocation Stress Syndrome… | `Title`, preceded by a `Swan Label` "BLOGS" |
| paras 32–54 | Modern Renting in Dubai… | `Title` |
| paras 55–72 | Why E-Movers Earns 5-Star Reviews | `Title` |

Each `Title` paragraph opens a new candidate and `Swan Label` paragraphs are boundaries too, so a
section containing several articles splits correctly. Boundaries are exact because they are
**declared, not detected** — but the split is still presented as a reviewable proposal rather than
committed silently, since a writer can style a paragraph `Title` in error. Merging and re-splitting
candidates is a supported operation.

### 6.2 Pipeline

```
Upload .docx
  -> validate (extension, magic bytes, size, zip-bomb and XXE guard)
  -> store original, compute SHA-256
  -> dedup: same ClientId + ContentHash already ingested? return existing candidates
  -> CONFORM  check named styles against the house style (§6.4)
             no Title and no Heading 1 -> REJECT with the template link, stop here
             override used? -> import as one Article, IsUnverified = true, audit it (§6.3.1)
  -> enqueue conversion
  -> EXTRACT  paragraphs, runs, inline formatting, lists, tables, links, images, captions
  -> SPLIT    into ArticleCandidates on Title / Heading 1 / Swan Label boundaries (§6.1)
  -> MAP      each paragraph's style -> HTML via the §6.3 table. No inference.
             strip Swan Label and Swan Note, lift Swan CTA into a block
  -> CLEAN    strip Word cruft: mso- classes, <o:p>, empty spans, style attributes, rsid noise
  -> EXTRACT  images to storage, register MediaFiles   (this document has none — §6.7)
  -> APPLY    per-client conversion rules (§6.8)
  -> REPORT   ConversionReportJson (§6.6), including conformance
  -> on accept, create Articles + ArticleVersion 1
```

### 6.3 Structure extraction — Structured only

**Decision: writers apply the house style. Extraction reads named styles and nothing else.**

This is a deliberate simplification, and it removes work rather than adding it. Because structure
lives in the source document, the entire heuristic layer — bold-run classification, run-in
detection, confidence scoring, and the human review gate — is **out of v1 scope**. What remains is
a deterministic style-to-HTML mapping, which is both smaller and more reliable than inference.

The mapping:

| Word style | Output | Notes |
|------------|--------|-------|
| `Title` | article title | + split boundary (§6.1) |
| `Heading 1` | `<h2>` | + split boundary (§6.1) |
| `Heading 2` | `<h3>` | |
| `Heading 3` | `<h4>` | |
| `Swan Label` | *nothing* | split boundary, never published |
| `Swan Note` | *nothing* | stripped, reported |
| `Swan CTA` | CTA block | URLs lifted into fields |
| `Normal` | `<p>` | |
| `List Paragraph` | `<li>` | inside `<ul>` / `<ol>` per numbering definition |
| `Caption` | `<figcaption>` | inside a `<figure>` with its image |
| inline `w:b` / `w:i` | `<strong>` / `<em>` | runs only — never promotes a paragraph to a heading |
| unstyled paragraph | `<p>` | **allowed**, and cannot be a heading |

A paragraph carrying inline bold but no heading style is unambiguously a body paragraph. The
run-in problem identified in the reference document — where "**Zero Monthly Markup:** The total
annual rent remains…" could be mistaken for a heading — **cannot occur**, because promotion
requires a named style and there is no heuristic to promote on.

**A style is never inferred. A missing style is a defect, not a prompt to guess.**

#### 6.3.1 Non-conforming input

A document that applies no `Title` and no `Heading 1` anywhere is **rejected at upload** with a
message naming the offending condition and linking the template (§6.4). Rejection is only viable
because conforming input is one click away; while inference was the only path, a reject would have
meant blocking the writer with no route forward.

The one exception is a deliberate, audited override:

**Import anyway (unverified).** Available to a user with `article.import_unverified`, for the
occasional deadline or third-party document. It imports the file as a single `Article` with no
split, marks the article `IsUnverified = true`, forces a full review before approval, and writes an
`AuditLogs` row. It does **not** attempt to infer structure — that is the point. Deferring the
human-gated inference path from v2.0 to post-v1 removes a whole subsystem and leaves this honest
two-button decision in its place.

#### 6.3.2 Heading level comes from the style, not from position

In the reference document, all five structurally-bold paragraphs were fully bold and of similar
length (38–97 characters), so titles and section headings could not be separated by any
positional or formatting rule — levels had to be assigned after splitting. Here the writer states
the level directly, and §6.1's split points are simply the `Title` / `Heading 1` paragraphs. Both
problems disappear together.

Every paragraph's source style is recorded, so any level can be overridden from the editor without
re-upload, and the conversion report can show "mapped from Heading 2" for every heading.


### 6.4 Adoption: the house style and the template

Moving structure into the source document is the right call, but it only works if conforming input
is the *easy* input. Two things are required, and neither is optional:

**1. Conformance check on upload.** Cheap, and it prevents a bad publish rather than reporting one
afterwards.

| Check | Severity |
|-------|----------|
| `namedStylesUsed` = 0 | **blocking** — reject with the template link |
| No `Title` / `Heading 1` applied | **blocking** |
| `Heading 2` count < 2 in a multi-article document | warning |
| Direct-bold paragraphs remaining in body text | warning — will be preserved as inline bold, not promoted |
| `Swan Note` paragraphs found | info — stripped |
| `Swan Label` paragraphs found | info — used as split boundaries |
| Images without alt text | warning |
| Unreachable or redirect-wrapped links | error, per link |

**2. A template, not a rule.** Publish `Swan-Article-Template.docx` from the app — downloadable
from the upload screen, the editor toolbar, and the client settings page. It defines:

| Word style | Purpose | Maps to |
|------------|---------|---------|
| `Title` | Article title | H1 + split boundary |
| `Heading 1` / `Heading 2` / `Heading 3` | Section structure | H2 / H3 / H4 |
| `Swan Label` | Writer-only section marker, e.g. "BLOGS" | split boundary, never published |
| `Swan Note` | Production notes, e.g. "(Video could be IIM A campus)" | stripped, reported |
| `Swan CTA` | Closing call to action | structured CTA block |
| `List Paragraph` | Body text and list items | `<p>` / `<li>` |
| `Caption` | Image caption | `<figcaption>` |

`Swan Label`, `Swan Note` and `Swan CTA` are the important part. They give writers an explicit,
one-click way to mark non-publishable content, which removes both the "BLOGS" label and the
"(Video could be IIM A campus)" note from the converter's guesswork entirely — no detection logic,
no false positives, no review gate.

**The template is not documentation — it is the contract.** `Swan-Article-Template.docx` is built by
the application, version-controlled, and its styles are the only thing §6.3 reads. A writer who
starts from the template produces a conforming document by construction. A writer who starts from a
blank document has to notice the styles exist, which is why the template is offered on the upload
screen rather than in a help article.

**Onboarding is the only real risk.** The template must be in place before the first writer upload,
and the existing September batch should be re-delivered in the new format as a pilot. One properly
formatted document proves the pipeline end to end; the current file would be rejected at upload by
design.

#### 6.4.1 The template as built — verified 29 Sep 2026

`Swan-Article-Template.docx` is built and validated. It ships the September articles **already
reformatted to the house style** as the worked example, so the team sees the target rather than a
description of it. `tools/build-template.ps1` regenerates it deterministically.

| Property | Value |
|----------|-------|
| Size / pages / words | 11.4 KB / 7 pages / 2,216 words |
| Paragraphs | 91 |
| Named styles applied | 41 (the original September file applied **0**) |
| `Title` paragraphs | 5 — placeholder + the 4 real articles |
| `Swan Label` | 4 · `Swan Note` 8 · `Swan CTA` 5 |
| Headings | `Heading 1` ×15, `Heading 2` ×1, `Heading 3` ×1 |
| Bullets | 15, one real numbering definition (no typed `-` or `1.`) |
| Hyperlinks | 27, all resolving, **0** google-search placeholders |

Verification performed: all 9 style names resolve in Word (`Title`, `Heading 1–3`, `Swan Label`,
`Swan Note`, `Swan CTA`, `List Paragraph`, `Caption`); every OOXML part re-parses from the written
package; all 27 hyperlink relationships resolve with no dangling `r:id`.

The September file's four defects (§0.1.B findings 2, 3, 5, 7) are fixed in the template rather than
merely documented: the "BLOGS" label and the video note are now `Swan Label` / `Swan Note`, the three
broken Google-search links are corrected to real `#lady_packers` fragment URLs, and the hand-typed
"1." / "2." prefixes are gone because those sections are real headings now.

**This closes decision M in practice, not formally.** The team still has to produce a *real* batch in
this format — but the worked example inside the template is byte-for-byte the September content, so
the pilot upload is a matter of deleting the two `INSTRUCTIONS` blocks.


### 6.5 Stripped and lifted content

These classes are declared by style, not detected:

| Style | Example in the September batch | Handling |
|-------|-------------------------------|----------|
| `Swan Label` | "E-Movers TL Article" (para 1), "BLOGS" (para 12) | Split boundary, never published |
| `Swan Note` | "(Video could be IIM A campus)" (para 10) | Stripped, reported as `NOTE_STRIPPED` |
| `Swan CTA` | paras 30, 53, 71 — "Request a Proposal", "call us at 800-Emovers" | Structured CTA block, URLs lifted into fields |

Because all four articles in the batch end with the same CTA, promoting them to a block means the
client edits the copy once instead of four times.

### 6.6 Conversion report

Shown for a document produced from the template, with 4 articles and 3 stripped labels:

```json
{
  "status": "Completed",
  "extractionMode": "Structured",
  "conformance": {
    "status": "Conforming",
    "namedStylesUsed": 8,
    "titleStyled": true,
    "headingLevelsPresent": [1, 2, 3],
    "templateVersion": "1.0"
  },
  "documentHash": "sha256:9f2c…",
  "articleCount": 4,
  "candidates": [
    { "index": 0, "paragraphRange": "13-31", "title": "What is Relocation Stress Syndrome: Why Moving Feels So Overwhelming",
      "titleStyle": "Title", "sections": 1, "links": 6, "ctaBlocks": 1, "issues": [] },
    { "index": 1, "paragraphRange": "32-54", "title": "Modern Renting in Dubai: How Flexi Rent and Payment Rewards Are Easing the Relocation",
      "titleStyle": "Title", "sections": 3, "links": 7, "ctaBlocks": 1, "issues": [] }
  ],
  "detected": { "headings": 14, "images": 0, "tables": 0, "links": 18, "brokenLinks": 3, "swanLabels": 2, "swanNotes": 1, "swanCtas": 3 },
  "warnings": [
    { "code": "BROKEN_LINK",        "severity": "error",   "paragraph": 4,
      "message": "3 links point to a Google search URL with the destination pasted as the query. Intended target is likely the '#lady_packers' fragment on /moving-homes/home-relocation/.", "fixable": true },
    { "code": "LABEL_STRIPPED",     "severity": "info",   "paragraph": 12, "message": "'BLOGS' (Swan Label) used as a split boundary and removed." },
    { "code": "NOTE_STRIPPED",      "severity": "info",   "paragraph": 10, "message": "'(Video could be IIM A campus)' (Swan Note) removed." },
    { "code": "NO_FEATURED_IMAGE",  "severity": "warning", "message": "Document contains no images. Apply the client default or upload a featured image before publishing." },
    { "code": "TYPED_NUMBERING",    "severity": "info",   "paragraph": 58, "message": "7 headings use typed '1.' / '2.' prefixes. Convert to real ordered lists?" },
    { "code": "EMPTY_HEADING",      "severity": "error",   "message": "Heading at paragraph 42 has no text." },
    { "code": "IMAGE_NO_ALT",       "severity": "warning", "message": "Image 2 has no alternative text." },
    { "code": "TABLE_TOO_WIDE",     "severity": "warning", "message": "Table has 7 columns; will be wrapped in a scroll container on mobile." }
  ],
  "fixesApplied": [
    { "code": "TABLE_TOO_WIDE", "action": "wrapped in responsive scroll container" }
  ]
}
```

`fixable: true` means the UI offers a one-click action, not just a warning. `TABLE_TOO_WIDE` in
particular is not a warning — it is a transformation: wide tables get a horizontally scrollable
wrapper, and repeated tables convert to definition lists on narrow breakpoints.

Note which severities are `info` and not `error`: stripping a section label and a writer's note is
the pipeline working correctly, and a report that flagged routine successes as problems would train
writers to ignore it.

### 6.7 Images are not guaranteed

The reference document contains no images at all — the package has no `word/media/` part. Featured
images for these four articles must therefore come from outside the document. Since §8.5's SEO rules
treat "missing featured image" as a validation check, a per-client default
(`SiteIntegrations.DefaultFeaturedMediaId`) is required, and the editor must surface featured-image
selection as a first-class field rather than letting four articles fail the same check on save.

The media pipeline in §8.2 is unchanged and still required for other clients and for writer uploads.
The correction is to the **priority and the defaults**, not the mechanism.

### 6.8 Per-client conversion rules

Conversion behaviour is configured per client, not globally, and lives in the `content` section of
`SiteIntegrations.ConfigJson`:

- style mapping — which Word style becomes which HTML tag, and any per-client divergence from the
  §6.3 default table
- typed numbering — whether a heading's literal "1." prefix is converted to a real ordered list
- list handling — bullets vs ordered, multi-level depth
- table handling — column count before wrapping, definition-list conversion thresholds
- image filtering — drop tracking pixels below 1×1, drop Office artifacts
- link normalisation — strip UTM parameters, flag redirect and search-URL wrappers
- boilerplate detection — repeated headers and footers across pages

---

## 7. Workflow and state machines

### 7.1 Editorial state (Article)

```
        ┌──────────────────────────────────────────────┐
        │                                              │
     Draft ──submit──▶ InReview ──request_changes──▶ ChangesRequested
        ▲                    │                              │
        │                    │ approve                       │ (edit + resubmit)
        └────────────────────┤                              │
                             ▼                              ▼
                          Approved ──────────────────────── (loop back to InReview)
                             │
                             │ (Article has no terminal publish state —
                             │  delivery is per-site, see §7.2)
                             ▼
                          Archived ◀── any state, with reason
```

### 7.2 Delivery state (ArticlePublication) — new in v2

```
  NotStarted ──draft sync──▶ DraftSynced ──preview──▶ PreviewReady
      │                                                    │
      │ schedule (from Approved)                            │ publish (from Approved)
      ▼                                                    ▼
  Scheduled ──job fires──▶ PublishInProgress ──success──▶ Published
                                │
                                ├── all stages ok ──────────▶ Published
                                ├── some ok, some failed ───▶ PartialFailure
                                └── first stage failed ─────▶ Failed
                                                              │
                                                     retry (safe stages only)
                                                              ▼
                                                       PublishInProgress

  Any ──unpublish──▶ Unpublished      Published/Unpublished ──archive──▶ Archived
```

`PartialFailure` exists because v1's own error example describes a state the v1 model had no name
for. Concretely: *WordPress post published, Netlify build failed*. The article is live in the CMS
and missing from the site. That is not `Failed` and must not be retried from the beginning.

### 7.3 Transition rules

| From | Action | To | Permission | Guard |
|------|--------|----|-----------|-------|
| Draft | submit | InReview | `article.submit` | Title, content, featured image, SEO title, meta description all present. `IsUnverified` articles (§6.3.1) require `article.import_unverified` on the submitter |
| InReview | request_changes | ChangesRequested | `article.review` | Comment body required |
| InReview | approve | Approved | `article.review` | All blocking SEO/accessibility errors cleared |
| ChangesRequested | submit | InReview | `article.submit` | — |
| Approved | schedule | Scheduled | `article.publish` | Future instant, client's IANA timezone valid |
| Approved | publish now | PublishInProgress | `article.publish` | Publication RowVersion matches |
| Scheduled | cancel | NotStarted | `article.publish` | Job cancelled, no partial write |
| PublishInProgress | — | Published | system | All stages succeeded + URL verified |
| PublishInProgress | — | PartialFailure | system | Some stages succeeded |
| PublishInProgress | — | Failed | system | First stage failed, or retries exhausted |
| PartialFailure | retry | PublishInProgress | `article.publish` | Only stages without a stored remote ID are re-run |
| Published | unpublish | Unpublished | `article.publish` | — |

Guards are enforced server-side. The UI hides unavailable actions but is not the enforcement point.

### 7.4 Review comments use text-quote anchoring

Character offsets break the moment the underlying content is edited, which is the normal case in
a review cycle. Anchor by surrounding text instead:

```json
{ "prefix": "The team will contact ", "exact": "within 48 hours", "suffix": " of the move date." }
```

Re-anchor on render; if the exact text no longer exists, fall back to the prefix/suffix
neighbourhood and mark the comment `orphaned` rather than silently dropping it.

---

## 8. Adapter layer

### 8.1 Interfaces

```csharp
public interface IPreviewProvider
{
    Task<PreviewResult> CreatePreviewAsync(Article article, ClientSite site, CancellationToken ct);
    Task<PreviewResult> RefreshPreviewAsync(Article article, ClientSite site, CancellationToken ct);
    Task DeletePreviewAsync(Article article, ClientSite site, CancellationToken ct);
}

public interface IContentPublisher
{
    Task<RemoteDraftResult> UpsertDraftAsync(PublishContext ctx, CancellationToken ct);
    Task<RemotePublishResult> PublishAsync(PublishContext ctx, CancellationToken ct);
    Task<RemotePublishResult> UnpublishAsync(PublishContext ctx, CancellationToken ct);
    Task<IReadOnlyList<RemoteTaxonomy>> SyncTaxonomiesAsync(ClientSite site, CancellationToken ct);
    Task<IReadOnlyList<RemoteAuthor>> SyncAuthorsAsync(ClientSite site, CancellationToken ct);
}

public interface IMediaPublisher
{
    Task<RemoteMediaResult> EnsureUploadedAsync(MediaContext ctx, CancellationToken ct);
}

public interface ISeoMetadataProvider
{
    Task ApplyAsync(SeoContext ctx, CancellationToken ct);
}

public interface ICustomFieldProvider
{
    Task ApplyAsync(CustomFieldContext ctx, CancellationToken ct);
}
```

Provider selection is by `SiteIntegrations.*ProviderKey`. A site with no configured provider fails
validation at client-setup time, not at publish time.

### 8.2 Media workflow

```
Image (extracted from .docx or uploaded)
  -> validate type, dimensions, size
  -> compute SHA-256
  -> check MediaFiles for an existing file with the same ClientId + ContentHash
       -> if found, reuse (dedup)
  -> optimize (resize, strip metadata, convert to WebP + fallback)
  -> capture / require alt text
  -> for each target destination on the site:
       -> check RemoteMediaMappings for a completed mapping
            -> if found, reuse RemoteId  (idempotency, §9.2)
            -> else upload, then write the mapping
```

For WordPress destinations, store: local `MediaFileId`, WordPress attachment ID, media URL, site ID,
status, and the `RemoteMediaMappings` row. Updating a draft reuses the attachment ID rather than
re-uploading.

### 8.3 Custom fields (typed — revised in v2.1)

ACF is confirmed present across the client fleet (§0.1.A), and on allaboutvat the CPT carrying blog
content (`announcements`) has **no registered taxonomy at all** — its categories are ACF fields. So
this is core v1 scope, not the deferrable item v2.0 assumed.

Values come from `SiteFieldDefinitions` (typed) and are stored in `SiteFieldValues`. v2.0's
`ValueTemplate` string model has been removed: it cannot express a required boolean, a multi-select,
or a taxonomy reference, and all three occur on the current fleet.

| Observed field | Site | Type | Swan behaviour |
|----------------|------|------|----------------|
| `Is Sticky` | allaboutvat | Boolean | Toggle in the editor; default from `SiteFieldDefinitions` |
| `Trending` | allaboutvat | Boolean | Toggle; may additionally drive internal placement |
| `Countries` | allaboutvat | MultiSelect / TaxonomyReference | Multi-value picker sourced from the target taxonomy |

`TaxonomyReference` needs one extra guarantee: the referenced term must exist on the target site
**before** the post is written. If the writer picks a country that has no term yet, the taxonomy
stage creates it first, then writes the post. Skipping this produces a post that silently drops the
value — the exact half-populated failure this module exists to prevent.

Order matters. `ResolveTaxonomies` (creates any missing terms) must precede `CreateOrUpdateDraft`
(which references them by ID).

Unmapped or unset required fields block the publish with a message naming the field and the site.
Fields whose `FieldType` is unrecognised are surfaced in the connection test rather than silently
skipped.

Built-in SEO providers (Yoast, Rank Math) remain separate adapters; a `HeadlessSeo` provider writes
SEO into Gatsby frontmatter instead of WordPress meta for the headless sites.

### 8.4 WordPress specifics

- **Post type is per-site and must never be assumed.** Verified values: `blogs` (emqube), `blog`
  (swansol), `announcements` (allaboutvat), `insights` (electricway), `move-on` (emoversuae).
  Every REST call is built from `SiteIntegrations.PostTypeKey` / `RestBase`. See §16, R1 for the
  unresolved emoversuae case.
- **The REST origin is not always the public host.** emqube's WordPress lives at `wp.emqube.com`
  while `emqube.com/wp-json` 404s; swansol's lives at `app.swansol.com`. All calls resolve through
  `SiteEndpoints` of kind `WordPressRest`, never from the client's public hostname.
- **Connection test must verify REST exposure of the actual post type**, not just authenticate.
  It checks: the REST root responds, the credentials authenticate, the target `PostTypeKey` is
  present in `/wp/v2/types`, its taxonomies are enumerable, and it probes a write against a
  throwaway draft. A CPT that is missing from `/wp/v2/types` fails the test with an explicit
  message — this is the check that would have caught emoversuae before any adapter was written.
- **Test for 200, not for "not 404".** A post type registered with `show_in_rest => false` returns
  **401** with a specific code (`rest_cannot_read_type`, `rest_forbidden`), not 404. emoversuae
  returns 401 for `move-on` and for its `blog` taxonomy today. A "did it 404?" check passes on a
  site that is completely unpublishable, so the assertion is a positive 200.
- **Never derive a taxonomy's REST route from its name.** `rest_base` must be read from
  `/wp/v2/taxonomies` and stored (`SiteTaxonomyRegistrations.RestBase`). `category` lives at
  `/wp/v2/categories`; probing `/wp/v2/category` 404s and reads as "no such taxonomy". The same trap
  applies to the *post payload key*, where `category` appears as `categories` — reading the wrong key
  returns an empty array and makes an actively-used taxonomy look dead. §0.1.E.
- **Taxonomy attachment is not usage.** A taxonomy can be attached to the post type and still be
  empty. The connection test records `PostsWithTermsRate` over a sample of published posts so
  `IsAuthoritative` is set from evidence, and dead taxonomies are never offered as a sync target.
- **The HTTP client must not use a default `Expect100Continue` with a low connection limit.** Against
  these origins that produces spurious transport failures and false 404s from the *second* request
  onward. `HttpClient` with `SocketsHttpHandler` is unaffected; anything reusing
  `ServicePointManager` defaults is not. §0.1.E.
- **Auth:** Application Passwords (WP 5.6+). No plugin, scoped, individually revocable, visible in
  the WP user profile. Store encrypted at rest; never return the value to the UI; log every
  decrypt as a `CredentialAccessLog` row.
- **Content format:** detect `use_block_editor` on connection test and persist it. `ContentFormat:
  Blocks` serialises paragraph, heading, list, list-item, image, quote, table and HTML blocks via
  `content` in the REST payload. Unsupported constructs fall back to a `core/html` block.
- **Drafts:** every article gets a WordPress draft on first save, regardless of frontend. That
  draft is the canonical staging copy that previews and publishes read from.
- **Rate limits:** REST API responses carry `X-Ratelimit-*`; the adapter honours them rather than
  letting a scheduled batch trip the limit.

### 8.5 SEO and accessibility

Both are adapters over the same `Article` and `SiteIntegrations` context, because Yoast, Rank Math
and headless implementations store metadata under different keys.

**SEO validation** (all blocking or advisory, explicitly marked):
missing meta title, missing meta description, empty slug, missing featured image, missing image alt
text, multiple H1 headings, missing internal links, broken external links, duplicate title within
the client, duplicate slug, missing canonical configuration.

**Accessibility validation:** heading hierarchy skips, empty headings, links with no discernible
text, images missing alt text, tables missing `<th>` headers, missing `<caption>`, blockquote used
for non-quote content, and colour contrast on featured images against the template background.

The two run in one pass and surface together on the editor's check panel — writers see one list,
not two.

---

## 9. Publishing pipeline

### 9.1 Stages

`Validate` → `BuildContent` → `ResolveTaxonomies` → `UploadMedia` → `ApplyCustomFields` →
`ApplySeo` → `ApplyAccessibility` → `CreateOrUpdateDraft` → `PreviewRender` → `Publish` →
`TriggerBuild` → `AwaitDeployment` → `VerifyUrl`

Each stage is one `PublishingAttempt` row. A stage that succeeds is never re-run on retry.

### 9.2 Idempotency contract

This is the single most important correctness rule in the product. Every outbound adapter call must:

1. Be invoked with an `IdempotencyKey` unique to the logical operation. Retries reuse the key.
2. **Read the stored remote ID before writing.** If `ArticlePublications.RemotePostId` is already
   populated, update — never create. Same for `RemoteMediaMappings`.
3. Write the returned remote ID to the mapping row in the *same* transaction as the attempt record,
   so a crash between "remote created" and "ID stored" is the only recoverable window. Recovery for
   that window: on retry, look up the remote object by a deterministic marker (post slug +
   `meta_swan_publication_id`, media by SHA-1 of the file per the WP media API) before creating.
4. Emit a structured result on every attempt, success or failure.

### 9.3 Concurrency and locking

- `Articles` and `ArticlePublications` both carry `RowVersion`. Every mutating API call sends the
  version it read; a mismatch returns `409 Conflict` with the current state.
- Background jobs take a distributed lock (`LockedByInstanceId` + `LockedUntilUtc`) before running,
  so a horizontally scaled deployment cannot double-publish the same job.
- `PublishingJobs.IdempotencyKey` is unique. A second enqueue of the same logical operation is a
  no-op, not a duplicate job.

### 9.4 Scheduling

**The application owns the clock.** WordPress scheduling (`status: future`) is explicitly disabled
for sites Swan manages — leaving it available creates two schedulers racing for the same post.

On schedule:

1. Writer picks a local date/time. It is resolved against `SiteIntegrations.TimeZone` (IANA) to a UTC
   instant.
2. Store `ScheduledUtc` (authoritative), `ClientTimeZone`, and `ScheduledLocalText` (the literal
   string the writer saw) so a later timezone-database change can never silently shift a post.
3. Scheduler enqueues a `Publish` job for the UTC instant, at least one polling interval early.
4. The job re-verifies the instant before publishing and skips if the article was cancelled,
   unapproved, or edited into an unpublishable state since scheduling.

### 9.5 Netlify deployment

1. Trigger via build hook. **The build-hook URL is a secret** — it embeds a token — and is stored in
   `SiteSecrets`, not in `SiteIntegrations`.
2. Record the returned build ID in `DeploymentLogs`.
3. Receive status via Netlify's deploy webhook (preferred) or poll the Netlify API (fallback).
4. On `Ready`, verify the published URL: HTTP 200 **and** the expected article marker present in
   the payload. Both must hold before the publication becomes `Published`.
5. On `Error` or verification failure: `PartialFailure` if the WordPress publish already succeeded,
   `Failed` otherwise. Both are retryable.

### 9.6 Preview architecture

**A. Standard WordPress.** Upsert the WP draft, return
`{SiteUrl}/?p={draftId}&preview=true` with the session's credentials attached server-side. The
writer is never handed WordPress admin credentials.

**B. Gatsby (headless WordPress).** The Gatsby site must expose a **request-time** preview route —
a Gatsby Function (`src/api/preview.ts`) or an SSR/DSG page. It cannot be a statically built page,
because `gatsby-source-wordpress` runs at build time and a draft does not exist in any build.

The function:

1. Validates a signed token (HMAC, `PreviewTokens.TokenHash`, short TTL, single article scope)
2. Fetches the draft from WPGraphQL at request time
3. Renders it with the same React components and styles as the production blog template
4. Sets `X-Robots-Tag: noindex, nofollow` and `<meta name="robots" content="noindex">`
5. Records the access in `PreviewSessions` (`AccessCount`, `LastAccessedUtc`)

Token revocation is immediate — the function checks `RevokedUtc` on every request, which is why
`PreviewTokens` is a separate table rather than a column.

**C. Netlify.** The deployed frontend exposes `/preview/:token`, which calls back into the Swan API
to resolve the token to article content, then renders using its own production components.

**Netlify Deploy Previews are not used for article preview.** They are the right tool for code and
template changes (Workstream 7), not for day-to-day writer edits — a fresh build per edit is
minutes of latency and unbounded build spend.

---

## 10. Error handling

Every attempt produces a structured result:

```json
{
  "articleId": 1042,
  "publicationId": 551,
  "clientSiteId": 7,
  "jobId": 8823,
  "stage": "AwaitDeployment",
  "status": "PartialFailure",
  "remotePostId": 651,
  "remoteUrl": "https://emoversqatar.com/storage-hacks-for-qatar/",
  "completedStages": ["Validate", "UploadMedia", "CreateOrUpdateDraft", "Publish", "TriggerBuild"],
  "failedStages": ["AwaitDeployment"],
  "message": "The WordPress post was published, but the Netlify build failed. Retry will re-run deployment only.",
  "retryAllowed": true,
  "nextAttemptUtc": "2026-10-02T09:05:00Z"
}
```

### 10.1 Stage taxonomy

`Validate`, `BuildContent`, `ResolveTaxonomies`, `UploadMedia`, `ApplyCustomFields`, `ApplySeo`,
`ApplyAccessibility`, `CreateOrUpdateDraft`, `PreviewRender`, `Publish`, `TriggerBuild`,
`AwaitDeployment`, `VerifyUrl`.

### 10.2 Retry policy

Retry only stages with no stored remote ID and no external side effect that cannot be reconciled:

| Stage | Retryable | Rule |
|-------|-----------|------|
| `Validate`, `BuildContent`, `ResolveTaxonomies` | Yes | Pure computation |
| `UploadMedia` | Yes | Guarded by `RemoteMediaMappings` |
| `ApplyCustomFields`, `ApplySeo`, `ApplyAccessibility` | Yes | Re-applied over the existing draft |
| `CreateOrUpdateDraft` | Yes | Guarded by `RemotePostId` |
| `PreviewRender` | Yes | — |
| `Publish` | **No** | Guarded by `RemotePostId`; a retry that finds a published remote post proceeds to `TriggerBuild` instead |
| `TriggerBuild` | Yes | Netlify build hooks are safe to re-trigger |
| `AwaitDeployment`, `VerifyUrl` | Yes | Read-only observation |

Exponential backoff: 1 min, 5 min, 15 min, 1 hour, 6 hours. `MaxAttempts` per publication (default
5). After exhaustion the job goes `Dead` and raises a notification and an alert. Failed publications
appear on the dashboard and in the Failed Publications report until resolved.

---

## 11. Security

### 11.1 Authentication and authorisation

- OIDC (Entra ID if available) or local accounts with Argon2id + mandatory MFA for
  `article.publish` and any administrative role
- Short-lived access tokens, rotating refresh tokens, revocation on password change
- Permissions are capability keys checked in one place (policy/middleware), never scattered
- Default roles: Writer, Reviewer, Publisher, Client Manager, Administrator

### 11.2 Credentials

- Envelope encryption: per-`SiteSecret` DEK wrapped by a KEK in Key Vault (prod) or DPAPI (dev)
- `KeyVersion` on every row enables rotation without a big-bang re-encrypt
- Secrets are write-only through the API — the UI never receives a decrypted value again
- `LastUsedUtc` supports access review; `CredentialAccessLogs` is append-only and separately retained
- Connection test decrypts only the specific secret required, and logs it

### 11.3 Application security

- File upload: extension + magic-byte validation, size cap, zip-bomb and XXE guards on Open XML
  parsing, stored files served from a separate origin with `Content-Disposition: attachment` and a
  non-executable content type
- XSS: sanitise all stored HTML on write *and* render, using an allow-list that matches the site's
  permitted Gutenberg block set. Never trust the DOCX or the editor.
- CSRF protection on state-changing cookie-authenticated endpoints
- Rate limiting on auth and preview-token endpoints
- Every preview URL is a bearer token — short TTL, single-scope, revocable, and every access audited
- No credentials in logs; `RequestJson`/`ResponseJson` are redacted before persistence

---

## 12. API surface

```
/api/auth                        login, refresh, logout, sso/{provider}, reset-password
/api/users                       list, invite, update, deactivate, roles
/api/roles                       list, permissions, assign

/api/clients                     list, create, update
/api/sites                        list, create, update          flat; client is a filter
/api/sites/{id}/test-connection   run the §0.1.A4 probe now
/api/sites/{id}/taxonomies        list registrations + terms, set the authoritative one
/api/sites/{id}/secrets          PUT (write-only), DELETE, rotate, reveal-never
/api/sites/{id}/authors          sync, list
/api/sites/{id}/field-definitions typed ACF + custom field definitions (list, create, update)
/api/sites/{id}/taxonomy-terms   browse values for TaxonomyReference fields

/api/articles                    list (paged + filtered + ?q=), create, get, update, archive
/api/articles/{id}/versions      list, get, restore
/api/articles/{id}/media         attach, detach, reorder, set-featured
/api/articles/{id}/seo           get, put, validate
/api/articles/{id}/field-values  typed custom field values, per publication
/api/articles/{id}/accessibility validate
/api/articles/{id}/comments      list, create, resolve
/api/articles/{id}/submit
/api/articles/{id}/approve
/api/articles/{id}/request-changes
/api/articles/{id}/publications  list sites this article targets, create (add a site)

/api/documents/upload            multipart
/api/documents/{id}/process
/api/documents/{id}/report
/api/documents/{id}/split-preview proposed ArticleCandidate boundaries from Title/Heading1 styles
/api/documents/{id}/split-accept  confirm or override boundaries, merge/split, create Articles
/api/documents/{id}/import-unverified  audited override: one Article, IsUnverified, no split

/api/media                       list, upload, get, update, delete, optimize, alt-text

/api/previews/{publicationId}/create
/api/previews/{publicationId}/refresh
/api/previews/{publicationId}/revoke

/api/publishing/{publicationId}/publish
/api/publishing/{publicationId}/schedule
/api/publishing/{publicationId}/cancel
/api/publishing/{publicationId}/unpublish
/api/publishing/{publicationId}/retry          one stage, or all safe stages
/api/publishing/{publicationId}/deployments
/api/publishing/failures         list PartialFailure with the full stage log — backs §12.1

/api/calendar
/api/reports/{kind}             articles-by-client | by-status | schedule | writer-workload | failed
/api/reports/{kind}/export       csv | xlsx
/api/audit-logs                  content | publication | login | configuration | credentials
/api/notifications               list, mark-read, preferences
/api/health                      + /api/health/adapters/{key}
```

Note the shift: preview and publishing routes are keyed by **`publicationId`**, not `articleId`,
because both operations are per-site.

All list endpoints use server-side paging and filtering, and every content list accepts `?q=` for the
global header search (§12.1). The calendar and reports are dataset queries, not client-side
operations.

### 12.1 Application sitemap

The nav is derived from the data model, not drawn separately: every top-level item is either a
workflow stage, an entity that already exists, or configuration that gates publishing. The rule is
that **`ArticlePublication` is the unit of action** — an article is never published, scheduled or
previewed, only a publication is — so publication is reachable from three places and is a global
context selector rather than a buried route.

```
/login  ·  /password-reset  ·  /sso/{provider}
/first-run                                  ★ welcome, role, client assignment (decision A)

/app
├── /dashboard                      my queue, due this week, failures needing attention
│
├── /documents                      the ingest workflow
│   ├── /documents/new              upload → conformance check (§6.4)
│   ├── /documents/{id}             conversion report (§6.6)
│   ├── /documents/{id}/split       ★ split review: N articles from 1 file (§6.1)
│   └── /documents/{id}/...         "import anyway (unverified)" = modal on the report,
│                                    not a route. The report screen offers the template
│                                    download too, for the same reason.
│
├── /articles                       content
│   ├── /articles?client=&status=   list, server-paged
│   ├── /articles/{id}
│   │   ├── /articles/{id}/content      structure editor, split/merge headings
│   │   ├── /articles/{id}/media        gallery, featured image (§6.7)
│   │   ├── /articles/{id}/seo          title/meta/slug + validate
│   │   ├── /articles/{id}/fields       ★ typed custom fields, per site (§8.3)
│   │   ├── /articles/{id}/accessibility
│   │   ├── /articles/{id}/comments     anchored review threads
│   │   ├── /articles/{id}/versions
│   │   └── /articles/{id}/publications/new   ★ "send this article to another site" —
│   │                                          the entry point the tree previously lacked
│   └── /articles/{id}/publications/{pubId}
│       ├── .../preview              render in the client's real design
│       ├── .../schedule             now, or pick a slot (§9.4)
│       └── .../deployments         stage log, retry, unpublish
│
├── /review                         approval queue (decision G — drawn flexibly, see below)
├── /publishing/failures            ★ PartialFailure triage (§7.2). Per-stage retry,
│                                    not a blanket "retry" — the whole reason the state
│                                    exists is that re-running completed stages is wrong.
├── /calendar                       scheduled + publishing windows, per site
├── /media                          shared asset library
│
├── /sites                          ★ FLAT list, not only reachable via /clients. One row
│   │                                per site with live health: post type, rest_base,
│   │                                connection status, blocked decisions. This is the
│   │                                screen you open first when a publish fails.
│   └── /sites/{siteId}
│       ├── /connection             ★ re-runs the probe on demand (§0.1.A4)
│       ├── /taxonomies             ★ which taxonomy is authoritative
│       ├── /fields                 typed field definitions
│       ├── /authors                remote author sync
│       ├── /secrets                write-only; never revealed
│       └── /preview-design         per-site preview theming
│   └── /clients                    client grouping is a filter on /sites, not a parent route
│
├── /reports                        by client · by status · schedule · workload · failed
├── /audit                          content · publication · login · config · credentials
└── /settings                       two audiences, two places
    ├── /settings/team              users · roles            — admin only
    └── /settings/workspace         house style v{n} · notification templates · defaults
```

★ = new in v2.1, created by a verified client-data finding rather than by preference.

**Two context selectors, not a nest.** Client and site are chosen from a sticky header and persist
across routes; publication is chosen inside the article. Deep-linking a publication without the
article in the breadcrumb is allowed, because an editor switching between five sites on one article
should not lose their place.

**Search is global, not per-list.** With ≥6 sites and server-side paging, every list endpoint also
accepts `?q=`, and one search box in the header searches articles, documents, publications and
media together. Per-list filters stay on the list; typing a query should not be a dead end.

| Screen | Primary actor | Notes |
|--------|---------------|-------|
| `/documents/new` | Writer | Rejection links straight to the template download; that round trip is the whole adoption strategy |
| `/documents/{id}` | Writer | Report. Hosts the unverified-import modal rather than a separate route — the choice is made *in response to* a report, not in isolation |
| `/documents/{id}/split` | Writer | Shows the proposed boundaries **before** creating anything, and lets the writer move them. One doc → N articles is the single biggest change from v1 |
| `/articles/{id}/content` | Writer | No heuristics, so it edits what the styles said. Reorder = a deliberate writer action |
| `/articles/{id}/fields` | Writer | Fields differ per site, so they render per publication, not per article |
| `/articles/{id}/publications/new` | Writer | The payoff of `ArticlePublication`. One article, chosen site, per-site fields inline |
| `/articles/{id}/publications/{pubId}/preview` | Writer, Reviewer | The only screen that proves anything, because it renders the client's own components |
| `/publishing/failures` | Writer, Admin | Lists `PartialFailure` with the **stage log visible** and per-stage retry. A publication that is live in WordPress but missing from Netlify must be obvious within seconds, not diagnosable |
| `/sites` | Admin | Health-first. "electricway — OK", "emoversuae — REST not exposed", "swansol — categories blocked" as literal row states, because these are the three real states this fleet has |
| `/sites/{siteId}/connection` | Admin | Re-runs the probe from §0.1.A4 on demand. Decision **I** is answered by it automatically; **J** stays blocked until wp-admin confirms the ACF fields. Read-only except credentials |
| `/sites/{siteId}/taxonomies` | Admin | Forcing a human to answer "which one is authoritative" is cheaper than discovering a silent sync failure in production |
| `/review` | Reviewer | Populated per §7.3 |
| `/settings/team` | Admin | Split from workspace settings because the audiences differ — most writers can never see it, and hiding it behind the wrong nav item just produces support questions |

**Deliberately absent:** no global "content" mega-menu, no separate "posts" section, no per-site
duplicate of articles, and no `/clients/{id}` detail route that only wraps a site list. One article,
N publications — the alternative is the writer maintaining the same article five times, which is the
problem `ArticlePublication` exists to solve.

#### 12.1.1 Two screens whose shape depends on an undecided decision

Both are prototyped in a form that survives either answer, so the prototype does not block on them
and the later decision is a configuration change rather than a redesign.

| Decision | Undecided | Prototyped as | Becomes |
|----------|-----------|---------------|---------|
| **A** — Entra ID SSO? | open | `/login` offers **both** a password form and a "Continue with Microsoft" button; `/sso/{provider}` is a redirect stub. `/first-run` assumes local accounts and says so | SSO-only hides the password form, deletes `/password-reset`, and moves role assignment into `/first-run` |
| **G** — named approvers per client, or any reviewer? | open | `/review` is a shared queue filtered by client, with a per-article **approver chip** showing who is assigned. An article whose approver is not you shows "waiting on {name}" | Named approvers adds reassignment as an action and filters the queue to what you can approve. Any-reviewer drops the chip and shows "approved by {name}" only |

#### 12.1.2 Prototype scope — the first build

The HTML prototype covers **one writer journey end to end**, not every route, because the value is in
the states and transitions that are easy to get wrong, not in the breadth of the nav:

```
/login  →  /documents/new  →  /documents/{id} (rejected)  →  template download
                        →  /documents/{id} (conforming)   →  /documents/{id}/split
                        →  /articles/{id}/content  →  /articles/{id}/seo
                        →  /review  →  approved
                        →  /articles/{id}/publications/new  →  /preview
                        →  publish  →  /publishing/failures  →  /deployments
```

That path deliberately includes the two failure states — conformance rejection and `PartialFailure` —
because a publishing tool's credibility rests on how honestly it shows failure, and both are cheap
to get wrong and expensive to discover late. `/sites` and `/settings` are static pages if time allows;
nothing else is.

##### What was actually built

22 pages in `prototype/`, all linked with real `<a href>`, generated by
`tools/build-prototype.ps1` so the shared shell is defined once. No JavaScript, no framework, no
build step — open `prototype/index.html`.

| Page | Stands in for | Note |
|------|---------------|------|
| `login.html` | `/login` | Both decision-A options, with a note saying why both are there |
| `first-run.html` | `/first-run` | Connect sites; emovers.com visibly 401 |
| `dashboard.html` | `/` | Three blockers, site health, most recent document |
| `documents.html` | `/documents` | |
| `documents-new.html` | `/documents/new` | States the five checks up front, not after a failure |
| `document-rejected.html` | `/documents/{id}` | **The real `EmoversUAE-Sep-Blogs.docx`** — see below |
| `document-report.html` | `/documents/{id}` | The corrected upload, 5 of 5 checks passing |
| `document-split.html` | `/documents/{id}/split` | 4 candidates, boundary controls, advisories |
| `articles.html` | `/articles` | Lists the whole journey as a walkthrough |
| `article-content.html` | `/articles/{id}/content` | Style-per-block editor, outline, readability |
| `article-seo.html` | `/articles/{id}/seo` | SEO title/description/slug/category + featured image |
| `review.html` | `/review` | Approver chips for decision G; blocked-on-SEO state |
| `publication-new.html` | `/articles/{id}/publications/new` | Per-site rows with category existence; preflight warnings |
| `preview.html` | `/preview` | Rendered inside a per-site frame |
| `deployments.html` | `/deployments` | Stage list + per-site results + response log |
| `publishing-failures.html` | `/publishing/failures` | The 4 real failure shapes, grouped by severity |
| `publications.html` | `/articles/{id}/publications` | |
| `sites.html` | `/sites` | Flat list, 9 sites |
| `search.html` | `?q=` | Cross-type results |
| `settings-team.html`, `settings-workspace.html` | `/settings/*` | |

**The prototype uses the real September document, not invented content.** `EmoversUAE-Sep-Blogs.docx`
is 72 paragraphs and 1,926 words, and it genuinely fails the house template: it uses **zero named
styles**, and all 18 of its headings are direct bold formatting on `Normal` paragraphs. That is the
entire reason the split cannot be done, and it is shown on `document-rejected.html` paragraph by
paragraph rather than described. The four articles it contains (331 / 618 / 629 / 348 words, at
paragraphs 1, 13, 32 and 55) are carried through the conforming upload and the split review, so the
numbers on every later page match the real file.


#### 12.1.3 Branding — taken from emqube.com

The Swan prototype uses **emqube.com's** identity, extracted from the live site on 29 Sep 2026
rather than approximated. The logo and the green are the same assets the emQube client site ships, so
the prototype is recognisably "the emQube family" to the content team.

| Token | Value | Source |
|-------|-------|--------|
| `--brand-green` | `#4E9C5A` | The site's own `--light_green`, and the fill inside `emqube-logo.svg` — so logo and UI cannot drift apart |
| `--brand-green-tint` | `#87D292` | used for the light-green variant of accents |
| `--ink` | `#121A21` | the site's own `--dark_grey` |
| `--logo-ink` | `#171717` | the 19 fills inside `emqube-logo-black.svg` |
| `--muted` | `#767676` | the site's own `--light_grey` |
| Neutral ramp | `#000 #171717 #3d3d3d #525252 #767676 #9d9c9c #c4c4c4 #d9d9d9 #fff` | observed across the site and inlined CSS |
| Logo | `emqube-logo-black.svg` / `emqube-logo-white.svg`, 196×76 | `wp.emqube.com/wp-content/uploads/2026/05/`, saved to `prototype/assets/` |

**Two deliberate substitutions, both because the real thing is licensed:**

1. **Helvetica Now Display** (Lineto — light 300 / reg 400 / medium 500 / bold 700) is the site's
   entire text face. It is a commercial licence, so the prototype uses **Inter** at the same four
   weights, which is the closest free neo-grotesque. If Swan ever buys the licence, only
   `--font-family-display` changes.
2. **`Untitled` is an icon font, not a text face** — it is only ever used for private-use glyph
   codepoints (`content:"\f00b"`). The prototype uses **inline SVG icons** instead, which is
   better practice regardless and removes the dependency.

Both substitutions live in one token file (`prototype/assets/tokens.css`) so re-skinning is a
single-file change. No other brand decision is baked into the page markup.

---

## 13. Non-functional requirements

### 13.1 Targets (confirm — see decision E)

| Metric | Target |
|--------|--------|
| DOCX conversion | p95 < 20 s for a 40-page document; queued beyond 10 MB |
| Preview generation | p95 < 5 s |
| Publish-to-live (WordPress type) | p95 < 60 s |
| Publish-to-live (headless) | p95 < build time + 2 min |
| API p95 (non-upload) | < 300 ms |
| Concurrent writers | 20 |
| Client sites | 50 |
| Articles / month | 500 |
| Uptime | 99.5% (business hours coverage acceptable) |
| Accessibility | WCAG 2.2 AA for the Swan UI itself |
| Browser support | Chrome/Edge current-2, Safari current-2 |

### 13.2 Performance

Article lists, calendar and reports are all server-paged. Search uses full-text indexing over
title/excerpt/content. Conversion and deployment never run inside an HTTP request.

### 13.3 Testing

- **Unit:** state machine transitions and guards, SEO/accessibility rules, conversion heuristics,
  idempotency helpers, template rendering
- **Contract:** every adapter against a real WordPress fixture in Docker, plus recorded HTTP
  fixtures for Netlify and WPGraphQL. Contract tests are the gate for any adapter change.
- **Integration:** EF Core against a real SQL Server via Testcontainers — no in-memory provider
- **E2E:** Playwright over the critical path — upload → edit → preview → submit → approve → publish
- **Idempotency tests:** deliberately replay every publishing stage and assert no duplicate remote
  object is created. These are the highest-value tests in the product.

### 13.4 Observability

Structured JSON logs with correlation IDs threaded from HTTP request through job to adapter call.
`/api/health` for liveness; `/api/health/adapters/{key}` for per-adapter reachability. Alerts on:
job entering `Dead`, publish failure rate above threshold, credential decrypt failure, preview token
rate-limit breach, job queue depth above N.

### 13.5 Backup and retention

SQL Server nightly full + log shipping. Original `.docx` retained per policy (decision F).
`AuditLogs` and `CredentialAccessLogs` retained independently of content — deleting an article
deletes its content, not the record that it existed.

---

## 14. Feature map

### 14.1 In scope for v1

Login (SSO or local) · Dashboard · Content calendar (month/week/list, filters) · Articles (all
statuses, create, edit) · **Batch split review — one upload, N proposed articles, writer-corrected
boundaries and titles** (§6.1) · **Conversion report with per-issue one-click fixes** (§6.6) ·
**Conformance check with hard reject, plus audited "import anyway" override** (§6.3.1) ·
**Downloadable house-style template** (§6.4) ·
Article details (overview, editor, original document, media, featured image, SEO, **typed custom
fields**, preview, comments, versions, WordPress info, publish history, activity) · Review queue ·
Clients and site configuration **including typed field definitions and authoritative-taxonomy
selection** · Media library · Publishing (queue, scheduled, successes, failures, retry, logs) ·
Reports with export · Users and roles · Notifications · Audit logs · Settings.

### 14.2 Deferred to post-v1

Revision **diff and compare** (history storage ships in v1; the compare UI does not) · internal
linking rule engine · writer-workload report · custom visual block builder · bulk multi-site
publish · content calendar drag-to-reschedule · per-client user accounts.

---

## 15. Workstreams

Reordered from v1 so the editor — the critical path — is not blocked behind integrations.

| # | Workstream | Contents | Depends on |
|---|-----------|----------|-----------|
| **1** | **Foundation** | Solution skeleton, Docker Compose for local SQL Server, OIDC/local auth, users/roles/permissions, full schema + migrations, audit logging, clients and site configuration, connection test, **adapter interfaces with no-op implementations**, health endpoints, structured logging | — |
| **2** | **Document ingestion** | Upload, file validation, storage, SHA-256, Open XML extraction, **conformance check against the house style** (§6.3), **house-style template `Swan-Article-Template.docx`** (§6.4), **style→HTML mapping, no inference** (§6.3), **multi-article splitting on `Title`/`Heading 1`** (§6.1), `Swan Label`/`Swan Note`/`Swan CTA` handling (§6.5), HTML cleanup, image extraction, responsive table transformation, per-client conversion rules, conversion report, split review screen, **audited "import anyway (unverified)" override** | 1 |
| **3** | **Content workspace** | Editor, autosave, versions, media library, **featured image with client default** (§6.6), categories/tags, SEO fields, **typed custom fields** (§8.3), accessibility panel, template application, draft WP sync (basic) | 1, 2 |
| **4** | **Workflow** | Submit, review comments with text-quote anchoring, request changes, approve, notifications, workflow history, guards server-side | 3 |
| **5** | **Standard WordPress integration** | Application Password auth, **connection test incl. post-type REST-exposure probe** (§8.4), per-site `PostTypeKey` / `RestBase`, taxonomy and author sync, **authoritative-taxonomy selection**, media upload with mapping, draft create/update, WP preview, publish, schedule, block/classic serialisation, **typed ACF write with create-term-if-missing** | 3, 4 |
| **6** | **Gatsby / emqube preview** | Gatsby preview Function, signed tokens, WPGraphQL draft retrieval, reuse of production blog components, noindex, token revocation, access auditing | 3, 5 |
| **7** | **Headless deployment** | Netlify build hook (as a secret), deploy-status tracking via webhook with polling fallback, published-URL verification, partial-failure reporting, safe retry | 5 |
| **8** | **Calendar, reporting, admin** | Content calendar, filters, reports, export, dashboard widgets, users/roles UI, audit log UI, settings UI | 4–7 |

**Critical path:** 1 → 2 → 3 → 4 → 5 → 6. Workstream 7 parallelises from 5. Workstream 8 parallelises
from 4.

**Sequencing note:** `IPreviewProvider`, `IContentPublisher`, `ISeoMetadataProvider` and
`ICustomFieldProvider` are defined with no-op implementations in Workstream 1, so the editor in
Workstream 3 is never blocked waiting on an integration.

**Do this first, before Workstream 5 gets design attention.** Build the connection probe from the
Workstream 1 connection test as a standalone CLI and run it against all five installs with
Application Passwords. Its output — which CPTs are REST-exposed, which taxonomies are authoritative,
whether `move-on` exists in REST at all, which ACF fields are present and of what type — should
populate `SiteIntegrations` and `SiteFieldDefinitions` from evidence rather than from assumption. It
is a day of work that can invalidate weeks of adapter design, and the reference document has already
invalidated seven assumptions.

---

## 16. Risks

| Risk | Impact | Mitigation |
|------|--------|-----------|
| ~~**R1. emoversuae is not REST-exposed at all.**~~ | ~~One of six clients unshippable on the REST path~~ | **RESOLVED — `show_in_rest => true` will be enabled client-side.** The `move-on` CPT and its `blog` taxonomy are publicly registered (§0.1.A). Both registrations need the flag — fixing only the CPT leaves categories unwritable. Connection test must re-verify after the client deploys, not assume. |
| Gatsby preview cannot be static | Blocks emqube.com | Decided in §9.6 — request-time Function. Confirm the emqube frontend team can host a Function on `wp.emqube.com`'s paired frontend. |
| ACF is confirmed across the fleet, and typed fields add real surface | Publish produces incomplete posts; build cost rises | §8.3 typed definitions; populate from the connection probe rather than hand-entry; contract tests per field type |
| WordPress client sites vary wildly (plugins, CPTs, taxonomies, block editor on/off) | Adapter maintenance burden | `PostTypeKey` + `RestBase` + `ContentFormat` + `SiteFieldDefinitions` absorb variation; contract tests pin behaviour |
| **Documents are multi-article batches, not single articles** | Ingestion built on the wrong unit produces 1 useless article per upload | §6.1 rewritten after verifying 4 articles in the reference file; split boundaries are writer-reviewable before any `Article` is created |
| **Documents use no named styles**, so structure exists only as direct bold | Naive extraction yields 66 undifferentiated paragraphs | §6.3 run-in classifier + per-classification rule reporting + writer override; validated against the reference file as a test fixture |
| Duplicate remote posts after partial failure | Client-visible defects | Idempotency contract §9.2 + concurrency control §9.3, covered by dedicated tests |
| Broken/redirect-wrapped links ship to clients | Live 404s, SEO loss | §6.8 link normalisation flags search-URL wrappers and redirect chains; 3 confirmed in the reference file |
| **A non-conforming document is a hard reject** | A writer on a deadline cannot upload, and workarounds (pasting into Word to strip structure) will be attempted | This is accepted, not mitigated: the template is one click from the upload screen, so the reject carries a route forward. "Import anyway (unverified)" (§6.3.1) exists for genuine deadlines and is audited |
| **Writers do not adopt the house style** | Every upload is rejected until they do; the pilot slips | Template must be in the app **before** the first writer upload, and the September batch re-delivered in the new format as a pilot. Report conformance rate per writer so adoption is a visible metric |
| Headless build times | Writers see a long publish lag | Show build state in the UI; verify URL before marking Published |
| Scope: 16 screens, 3 integration types, 6 client sites, 8 workstreams | Schedule slip | §14.2 deferral list; per-client configuration absorbs variation instead of new screens |
