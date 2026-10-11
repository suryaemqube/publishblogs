<#
  build-template.ps1
  Builds Swan-Article-Template.docx - the house-style template (PLAN-v2.md sec 6.4).

  Contains the four September 2026 articles from EmoversUAE-Sep-Blogs.docx, reformatted
  to the house style so the content team has a worked example rather than a rule sheet.

  The four styles Swan reads are defined here and must not be renamed:
      Title / Heading 1 / Heading 2 / Heading 3 / Swan Label / Swan Note / Swan CTA
#>

$ErrorActionPreference = 'Stop'
$projDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$outFile = Join-Path $projDir 'Swan-Article-Template.docx'

function X([string]$s) { [System.Security.SecurityElement]::Escape($s) }

# ---------------------------------------------------------------- content model
# style | text    inline markup:  **bold**   [[anchor|url]]   [[#anchor|url]]
$TITLE = 'Title'; $H1 = 'Heading1'; $H2 = 'Heading2'; $H3 = 'Heading3'
$P = 'Normal'; $L = 'ListParagraph'; $CAP = 'Caption'
$LBL = 'SwanLabel'; $NOTE = 'SwanNote'; $CTA = 'SwanCta'

$content = @(
"$NOTE|**How to use this template.** Every article starts with the **Title** style. Section headings use **Heading 1**, **Heading 2** and **Heading 3**. Body text is **Normal**. Never type a number into a heading - if something is a list, make it a real list.",
"$NOTE|**One file can hold many articles.** Start a new article with **Title**. A **Swan Label** marks a section for your own eyes only - it is used to group articles and is never published.",
"$NOTE|**Swan Note** is for you, not for readers. Production instructions, video ideas, placeholders - apply **Swan Note** and they are removed automatically.",
"$NOTE|**Swan CTA** is your closing call to action. It is lifted into a reusable block, so the same CTA is edited once instead of once per article.",
"$NOTE|**Featured image.** Documents are not expected to carry images. Set the featured image in Swan after upload, or accept the client default.",
"$NOTE|**Links.** Paste real URLs, never a Google search result. The September draft had three links pointing at a search page with the real address pasted into the query box - those are live 404s.",
"$LBL|INSTRUCTIONS - DELETE THIS BLOCK BEFORE SENDING",

"$TITLE|PLACEHOLDER ARTICLE - DELETE BEFORE SENDING",
"$P|This paragraph demonstrates **Normal** body text. Use it for everything that is not a heading or a list item.",
"$H1|Your first major section",
"$P|Supporting text under the section heading.",
"$L|First bullet point",
"$L|Second bullet point",
"$L|Third bullet point",
"$CAP|Figure 1 - captions use the **Caption** style and sit under their image.",
"$H2|A subsection",
"$P|Use Heading 2 only when a section genuinely has children. Do not use it to make body text look important.",
"$H3|A sub-subsection",
"$P|Heading 3 is the deepest level. Most articles stop at Heading 1.",
"$NOTE|Writer's note goes here. It will be removed before publishing.",
"$CTA|Ready to take the next step? [[Request a Proposal|https://emoversuae.com/get-a-free-quote/]] or call [[800-Emovers|https://emoversuae.com/contact-us/]].",
"$LBL|END INSTRUCTIONS - DELETE EVERYTHING ABOVE THIS LINE",

# ---------------------------------------------------------------- article 1
"$LBL|E-Movers TL Article",
"$TITLE|How an E-Movers Case Study Ended Up in the IIMA Library",
"$P|Like many aspiring students, studying at IIM Ahmedabad was always a personal dream. While I never got to sit in their classrooms, I am incredibly proud that the E-Movers case study made it there instead.",
"$P|When IIMA opened their campus in Dubai, they were actively looking for regional case studies to add to their curriculum. They were particularly interested in how a labor-intensive service company like ours managed to achieve and sustain exceptionally high customer satisfaction and Net Promoter Scores. The trigger for their study was our organic growth and the core values that continue to make us strive and succeed.",
"$H1|What the Qualification Involved",
"$P|The process was incredibly rigorous and humbling. Here is a brief look at what it involved:",
"$L|**Thorough Qualification:** The process began with a detailed questionnaire to ensure our story fit their academic needs.",
"$L|**Deep-Dive Interviews:** Once qualified, a team comprising two professors and an assistant was assigned to our case. They interviewed our top management to understand our operational philosophy.",
"$L|**On-the-Ground Fact-Checking:** They did not just take our word for it. To verify the claims we made in the boardroom, they interviewed our middle management, our frontline workers, and reached out to several of our clients.",
"$P|After comprehensive data collection, the draft was shared with us for a final fact-check. Today, this case study is officially part of the IIMA library. It is used as a case option for different courses, serving students well beyond just their Dubai campus.",
"$P|We keep a copy of the final document, but as I have come to realize, the text merely records our journey. The actual value of an IIMA case study lies entirely in the probing, challenging questions that the professors ask their students during classroom discussions.",
"$NOTE|(Video could be IIM A campus)",
"$CTA|Planning your next transition? [[Request a Proposal|https://emoversuae.com/get-a-free-quote/]].",

# ---------------------------------------------------------------- article 2
"$LBL|BLOGS",
"$TITLE|What is Relocation Stress Syndrome: Why Moving Feels So Overwhelming",
"$P|Upgrading your home is supposed to be a joyful milestone. Whether you are moving to a luxury villa in Al Barari or relocating to a modern apartment in Dubai, the prospect of a fresh start is exciting. Yet, for many families and professionals, the thought of actually moving creates deep hesitation. It is common to delay a necessary move for months simply because the process feels emotionally and physically paralyzing.",
"$P|This hesitation is not mere procrastination. Medical science confirms that moving triggers a distinct, documented psychological condition known as Relocation Stress Syndrome (RSS). Recognizing the root causes of this stress can help you navigate your next transition with clarity and confidence.",
"$H1|The Clinical Reality of Relocation Stress",
"$P|Relocation Stress Syndrome was formally recognized as a clinical diagnosis by the North American Nursing Diagnosis Association (NANDA) in 1992. It is defined in medical literature as a state in which an individual experiences physiological or psychosocial disturbances as a result of shifting from one living environment to another. Clinical research proves that uprooting a household creates a significant burden on both the mind and body.",
"$P|Studies by researchers Kao, Travis, and Acton (2004) demonstrated that moving frequently triggers physical symptoms, including acute sleep disruption, gastrointestinal distress, and heightened anxiety due to broken daily routines. A study published in *The Gerontologist* by Mirotznik and Kamp (2000) revealed that individuals undergoing a move experience a measurable decline in overall physical health compared to non-movers. Additional research by Laughlin et al. (2007) highlighted that symptoms of insecurity, apprehension, and mental exhaustion do not vanish when the boxes arrive; they can persist for up to four months after settling into a new residence.",
"$H1|Why the Moving Process Exhausts the Brain",
"$P|The primary trigger of Relocation Stress Syndrome is not the new location itself, but the overwhelming chaos of getting there. Moving forces your brain into continuous high-alert status, causing severe decision fatigue. Packing fragile items, managing timelines, coordinating logistics, and handling heavy physical labor rapidly exhaust your cognitive reserves.",
"$P|When you add the stress of handling delicate personal attire or assembling complex furniture in a new home, the emotional friction doubles. For residents planning a [[home relocation UAE|https://emoversuae.com/moving-homes/home-relocation/]] journey, navigating these demands without professional assistance turns a positive life step into a daunting chore. Choosing qualified [[house movers in Abu Dhabi|https://emoversuae.com/moving-homes/home-relocation/]] or experienced [[movers in Dubai|https://emoversuae.com/moving-homes/home-relocation/]] allows you to pass these complex physical burdens to experts who handle them daily.",
"$H1|Defeat Relocation Stress with E-Movers",
"$P|At E-Movers, we understand that moving is far more than an exercise in transport logistics. It is an emotional transition that requires care, respect, and meticulous planning. Since 2003, our entire business model has been built around a singular mission: delivering a true ~Q~No Mess, No Stress~Q~ experience for every client.",
"$P|We actively dismantle the triggers of Relocation Stress Syndrome through three key service pillars:",
"$L|**Empathetic and Specialized Staffing:** For feminine attire, personal items, and delicate keepsakes, our specialized [[Lady Packers|https://emoversuae.com/moving-homes/home-relocation/#lady_packers]] provide discrete, attentive care that removes personal discomfort from the packing process.",
"$L|**Complete Technical Relief:** Our trained [[handyman movers|https://emoversuae.com/moving-services/handyman-services/]] manage the heavy lifting, furniture disassembly, wall restoration, and reassembly, eliminating decision fatigue and physical strain so you can relax.",
"$L|**Verified Quality Standards:** As a proudly FIDI accredited international mover, our audited operational standards guarantee absolute reliability, preventing unexpected delays or transit damage.",
"$P|When you work with trusted [[professional movers and packers|https://emoversuae.com/moving-homes/home-relocation/]], you preserve your health, time, and peace of mind. Let our experienced teams handle the logistics while you focus on enjoying your new home.",
"$CTA|Ready to experience a calm, seamless move? [[Request a Proposal|https://emoversuae.com/get-a-free-quote/]] or call us at [[800-Emovers|https://emoversuae.com/contact-us/]] to speak with our relocation specialists.",

# ---------------------------------------------------------------- article 3
"$TITLE|Modern Renting in Dubai: How Flexi Rent and Payment Rewards Are Easing the Relocation Journey",
"$P|Moving into a new home in Dubai is an exciting milestone, whether you are settling into a high-rise apartment in Downtown or relocating your family to a quiet villa in Mirdif. However, for years, the single largest barrier for tenants has been the sheer financial weight of the leasing process. Handing over one to four post-dated cheques for an entire year's rent placed massive strain on liquidity. When combined with initial security deposits, agency fees, and setup costs, the upfront expenditure felt daunting.",
"$P|Fortunately, the rental landscape is undergoing a user-centric transformation designed to align real estate practices with modern personal finance. Through new public regulatory models and innovative digital payment platforms, renting a home in Dubai is becoming simpler, fairer, and far less stressful.",
"$H1|The Dubai Land Department's Flexi Rent Initiative",
"$P|The Dubai Land Department (DLD) introduced the Flexi Rent scheme in partnership with major real estate management companies to modernize how housing payments are structured. This framework directly addresses the liquidity burden by allowing eligible tenants to spread their annual rent across monthly, quarterly, or semi-annual installments instead of relying on massive upfront sums.",
"$P|Key public benefits include:",
"$L|**Zero Monthly Markup:** The total annual rent remains exactly the same whether paid in a single cheque or broken down into twelve equal payments.",
"$L|**Cash Flow Alignment:** Paying monthly allows residents to line up living expenses directly with their salary cycles rather than depleting emergency savings or taking out short-term personal loans.",
"$L|**Card-Based Transactions:** Flexi Rent supports digital payments via credit or debit cards, moving away from the operational anxiety of managing physical cheques.",
"$H1|Unlocking Value Through Digital Rent Rewards",
"$P|Alongside regulatory flexibility, digital platforms like Tern are redefining the actual mechanics of paying rent. Traditionally, housing rent was treated as a dead-weight administrative expense that offered no return.",
"$P|Using digital payment platforms, tenants can route rent payments through credit cards - even if their landlord or building manager traditionally requires standard bank transfers. This approach unlocks several practical financial advantages:",
"$L|**Earning Capital on Fixed Outflows:** By charging monthly rent to rewards-linked credit cards via payment platforms, tenants accumulate airline miles, cashback, or reward points on their single largest monthly line item.",
"$L|**Extended Payment Windows:** Utilizing credit cards gives renters a grace period between the rent due date and their credit statement settlement date, providing a buffer during tight months.",
"$L|**Building Credit Health:** Consistent, on-time digital rent settlements help demonstrate responsible credit utilization and strengthen personal financial standing.",
"$H1|Reclaiming Comfort and Reducing Move-Day Friction",
"$P|These financial innovations represent a welcome shift toward tenant-first convenience in the UAE real estate market. When upfront rental commitments are normalized into manageable monthly schedules, residents regain the financial flexibility needed to handle the rest of their move comfortably.",
"$P|Having a smoother rental setup allows tenants to allocate resources toward proper property care, such as utilizing professional [[handyman movers|https://emoversuae.com/moving-services/handyman-services/]] to restore their previous lease condition or hiring qualified [[movers in Dubai|https://emoversuae.com/moving-homes/home-relocation/]] to protect valuable furniture during transit.",
"$H1|Partner with the Relocation Specialists at E-Movers",
"$P|At E-Movers, we welcome every public initiative and digital innovation that reduces friction for residents across the UAE. Moving home should be a positive milestone, not a source of financial or physical overwhelm.",
"$P|Since 2003, our trained teams have provided a reliable ~Q~No Mess, No Stress~Q~ relocation experience. Whether you need expert [[home relocation UAE|https://emoversuae.com/moving-homes/home-relocation/]] support, specialized [[Lady Packers|https://emoversuae.com/moving-homes/home-relocation/#lady_packers]] for sensitive attire, or secure [[storage units Dubai|https://emoversuae.com/storage/long-term-storage/]] while transitioning between leases, we manage every detail with precision. As a proudly FIDI accredited international mover, we guarantee global standards of care for every step of your journey.",
"$CTA|Planning your next home transition? [[Request a Proposal|https://emoversuae.com/get-a-free-quote/]] or call us at [[800-Emovers|https://emoversuae.com/contact-us/]] to speak with our team.",

# ---------------------------------------------------------------- article 4
"$TITLE|Why E-Movers Service Continues to Earn 5-Star Reviews",
"$P|Our Google Reviews are consistently rated 5-stars, and we do not have to go hunting for testimonials as we receive them directly from satisfied clients. We measure our Net Promoter Score after every single move, asking customers, ~Q~How likely are you to recommend us to friends and relatives?~Q~ and we consistently score upwards of 9 out of 10.",
"$P|Here are the five operational commitments we keep to earn those stars every day.",
"$H1|Clear Financial Value",
"$P|We ensure complete financial transparency before a single item is packed. Every client receives a fixed, fully itemized quote after a pre-move survey, guaranteeing no hidden charges or unexpected expenses on move day.",
"$H1|Safe and Protected Transit",
"$P|Protecting your personal assets is our highest priority. We use specialized wrapping materials, including 5-ply corrugated boxes, padded edge guards, and custom wooden crates to shield every item from damage throughout the journey.",
"$H1|Process-Driven Execution",
"$P|Spontaneity has no place in logistics. Every relocation follows a standardized step-by-step workflow refined over twenty years, covering structured loading, systematic handling, and careful room-by-room setup.",
"$H1|Trained, Permanent Teams",
"$P|Our moves are executed by permanent, fully trained staff who specialize in structural furniture assembly and spatial handling. Dedicated team leaders coordinate with specialized personnel, including skilled [[handyman movers|https://emoversuae.com/moving-services/handyman-services/]] and specialized [[Lady Packers|https://emoversuae.com/moving-homes/home-relocation/#lady_packers]], to complete your setup seamlessly.",
"$H1|Practical Service Innovations",
"$P|We continually introduce customer-focused innovations that solve real relocation challenges. From specialized female packing crews to climate-controlled [[storage units Dubai|https://emoversuae.com/storage/long-term-storage/]] and [[moving out services|https://emoversuae.com/moving-services/moving-out-services/]], we adapt our solutions to meet modern client expectations.",
"$H1|Experience a True ~Q~No Mess, No Stress~Q~ Move",
"$P|Maintaining high customer satisfaction is at the core of our business. As a proudly FIDI accredited international mover and long-standing IAM member, our operations adhere to globally audited quality standards. Whether you need the [[best movers Dubai|https://emoversuae.com/moving-homes/home-relocation/]] for a villa transition, reliable [[house movers in Abu Dhabi|https://emoversuae.com/moving-homes/home-relocation/]], or expert [[international relocation services|https://emoversuae.com/moving-homes/international-relocation/]], our team is ready to assist.",
"$CTA|Planning your next transition? [[Request a Proposal|https://emoversuae.com/get-a-free-quote/]] or call us at [[800-Emovers|https://emoversuae.com/contact-us/]] to speak with our relocation specialists."
)

# ---------------------------------------------------------------- render runs
$script:rid = 0
$relList = New-Object System.Collections.Generic.List[string]
$hlinkStyle = '<w:rPr><w:rStyle w:val="Hyperlink"/></w:rPr>'

function New-Run([string]$text, [bool]$bold, [bool]$italic) {
  $rpr = ''
  if ($bold -or $italic) { $rpr = '<w:rPr>' + $(if($bold){'<w:b/>'}else{''}) + $(if($italic){'<w:i/>'}else{''}) + '</w:rPr>' }
  $t = X ($text.Replace('~Q~', [string][char]34))
  return "<w:r>$rpr<w:t xml:space=""preserve"">$t</w:t></w:r>"
}

function New-HyperlinkRun([string]$anchor, [string]$url) {
  $script:rid++
  $id = "rId$($script:rid)"
  $relList.Add("<Relationship Id=""$id"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"" Target=""$(X $url)"" TargetMode=""External""/>")
  $t = X $anchor
  return "<w:hyperlink r:id=""$id""><w:r>$hlinkStyle<w:t xml:space=""preserve"">$t</w:t></w:r></w:hyperlink>"
}

function New-Paragraph([string]$styleId, [string]$text) {
  $body = ''
  $i = 0
  $tokens = [regex]::Split($text, '(\*\*.*?\*\*|\[\[.*?\]\])')
  foreach ($tok in $tokens) {
    if ($tok -eq '') { continue }
    if ($tok -match '^\*\*(.+)\*\*$')   { $body += New-Run $Matches[1] $true  $false }
    elseif ($tok -match '^\*\*(.+?)\*\*$') { $body += New-Run $Matches[1] $true $false }
    elseif ($tok -match '^\[\[(.+?)\|(.+?)\]\]$') { $body += New-HyperlinkRun $Matches[1] $Matches[2] }
    elseif ($tok -match '^\[\[(.+?)\|(.+?)\]\]$') { $body += New-HyperlinkRun $Matches[1] $Matches[2] }
    else {
      # plain text, honour *italic* segments
      $sub = [regex]::Split($tok, '(\*[^*]+\*)')
      foreach ($s in $sub) {
        if ($s -eq '') { continue }
        if ($s -match '^\*(.+)\*$') { $body += New-Run $Matches[1] $false $true }
        else { $body += New-Run $s $false $false }
      }
    }
  }
  $ppr = if ($styleId -ne $P) { "<w:pPr><w:pStyle w:val=""$styleId""/></w:pPr>" } else { '' }
  return "<w:p>$ppr$body</w:p>"
}

# styleId / marker normalisation
$styleMap = @{
  'Title'=$TITLE; 'Heading1'=$H1; 'Heading2'=$H2; 'Heading3'=$H3
  'Normal'=$P; 'ListParagraph'=$L; 'Caption'=$CAP
  'SwanLabel'=$LBL; 'SwanNote'=$NOTE; 'SwanCta'=$CTA
}

$bodyXml = ''
foreach ($line in $content) {
  $parts = $line -split '\|', 2
  $sid = $styleMap[$parts[0]]
  $txt = if ($parts.Count -gt 1) { $parts[1] } else { '' }
  $bodyXml += New-Paragraph $sid $txt
}

$sectPr = '<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="708" w:footer="708" w:gutter="0"/></w:sectPr>'

$documentXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<w:body>$bodyXml$sectPr</w:body>
</w:document>
"@

# ---------------------------------------------------------------- styles.xml
$stylesXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/><w:sz w:val="22"/><w:szCs w:val="22"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="160" w:line="259" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>

<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:qFormat/></w:style>

<w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
<w:pPr><w:keepNext/><w:spacing w:before="240" w:after="120"/><w:outlineLvl w:val="0"/></w:pPr>
<w:rPr><w:rFonts w:asciiTheme="majorHAnsi" w:hAnsiTheme="majorHAnsi"/><w:b/><w:color w:val="1F3864"/><w:sz w:val="52"/><w:szCs w:val="52"/></w:rPr></w:style>

<w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
<w:pPr><w:keepNext/><w:spacing w:before="320" w:after="120"/><w:outlineLvl w:val="1"/></w:pPr>
<w:rPr><w:rFonts w:asciiTheme="majorHAnsi" w:hAnsiTheme="majorHAnsi"/><w:b/><w:color w:val="1F3864"/><w:sz w:val="32"/><w:szCs w:val="32"/></w:rPr></w:style>

<w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
<w:pPr><w:keepNext/><w:spacing w:before="280" w:after="100"/><w:outlineLvl w:val="2"/></w:pPr>
<w:rPr><w:rFonts w:asciiTheme="majorHAnsi" w:hAnsiTheme="majorHAnsi"/><w:b/><w:color w:val="2E5496"/><w:sz w:val="26"/><w:szCs w:val="26"/></w:rPr></w:style>

<w:style w:type="paragraph" w:styleId="Heading3"><w:name w:val="heading 3"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
<w:pPr><w:keepNext/><w:spacing w:before="240" w:after="80"/><w:outlineLvl w:val="3"/></w:pPr>
<w:rPr><w:b/><w:color w:val="2E5496"/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr></w:style>

<w:style w:type="paragraph" w:styleId="ListParagraph"><w:name w:val="List Paragraph"/><w:basedOn w:val="Normal"/><w:qFormat/>
<w:pPr><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr><w:spacing w:after="80"/><w:ind w:left="720"/><w:contextualSpacing/></w:pPr></w:style>

<w:style w:type="paragraph" w:styleId="Caption"><w:name w:val="caption"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
<w:pPr><w:spacing w:after="200"/></w:pPr><w:rPr><w:i/><w:color w:val="44546A"/><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr></w:style>

<w:style w:type="paragraph" w:styleId="SwanLabel"><w:name w:val="Swan Label"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
<w:pPr><w:pBdr><w:left w:val="single" w:sz="18" w:space="6" w:color="C00000"/></w:pBdr><w:spacing w:before="360" w:after="160"/><w:ind w:left="180"/><w:shd w:val="clear" w:color="auto" w:fill="FDF2F2"/></w:pPr>
<w:rPr><w:b/><w:caps/><w:color w:val="C00000"/><w:sz w:val="20"/><w:szCs w:val="20"/></w:rPr></w:style>

<w:style w:type="paragraph" w:styleId="SwanNote"><w:name w:val="Swan Note"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
<w:pPr><w:pBdr><w:left w:val="single" w:sz="18" w:space="6" w:color="7F6000"/></w:pBdr><w:spacing w:before="120" w:after="120"/><w:ind w:left="180"/><w:shd w:val="clear" w:color="auto" w:fill="FFFBEF"/></w:pPr>
<w:rPr><w:i/><w:color w:val="7F6000"/><w:sz w:val="20"/><w:szCs w:val="20"/></w:rPr></w:style>

<w:style w:type="paragraph" w:styleId="SwanCta"><w:name w:val="Swan CTA"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
<w:pPr><w:pBdr><w:top w:val="single" w:sz="6" w:space="6" w:color="1F3864"/><w:bottom w:val="single" w:sz="6" w:space="6" w:color="1F3864"/></w:pBdr><w:spacing w:before="240" w:after="240"/><w:ind w:left="120" w:right="120"/><w:shd w:val="clear" w:color="auto" w:fill="F2F5FA"/></w:pPr>
<w:rPr><w:b/><w:color w:val="1F3864"/><w:sz w:val="22"/><w:szCs w:val="22"/></w:rPr></w:style>

<w:style w:type="character" w:styleId="Strong"><w:name w:val="Strong"/><w:qFormat/><w:rPr><w:b/></w:rPr></w:style>
<w:style w:type="character" w:styleId="Hyperlink"><w:name w:val="Hyperlink"/><w:qFormat/><w:rPr><w:color w:val="0563C1"/><w:u w:val="single"/></w:rPr></w:style>
</w:styles>
'@

$numberingXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:numbering xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
<w:abstractNum w:abstractNumId="0"><w:multiLevelType w:val="hybridMultilevel"/>
<w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="&#8226;"/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="720" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Symbol" w:hAnsi="Symbol" w:hint="default"/></w:rPr></w:lvl>
<w:lvl w:ilvl="1"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="o"/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="1440" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Courier New" w:hAnsi="Courier New" w:hint="default"/></w:rPr></w:lvl>
<w:lvl w:ilvl="2"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="&#9642;"/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="2160" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Wingdings" w:hAnsi="Wingdings" w:hint="default"/></w:rPr></w:lvl>
</w:abstractNum>
<w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num>
</w:numbering>
'@

$contentTypes = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
<Default Extension="xml" ContentType="application/xml"/>
<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
<Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
<Override PartName="/word/numbering.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"/>
<Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/>
<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
<Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
</Types>
'@

$rootRels = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
<Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>
'@

$settingsXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:settings xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
<w:defaultTabStop w:val="720"/><w:compat/>
</w:settings>
'@

$coreXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
<dc:title>Swan Article Template - House Style v1.0</dc:title>
<dc:subject>Blog article template</dc:subject>
<dc:creator>Swan Content Publisher</dc:creator><cp:keywords>Swan; template; house style</cp:keywords>
<dc:description>Template for Swan Content Publisher. Use Title for article titles, Heading 1-3 for sections, Swan Label for writer-only section markers, Swan Note for production notes, Swan CTA for closing calls to action.</dc:description>
<cp:lastModifiedBy>Swan Content Publisher</cp:lastModifiedBy><cp:revision>1</cp:revision>
<dcterms:created xsi:type="dcterms:W3CDTF">2026-09-29T00:00:00Z</dcterms:created>
<dcterms:modified xsi:type="dcterms:W3CDTF">2026-09-29T00:00:00Z</dcterms:modified>
</cp:coreProperties>
'@

$appXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
<Application>Swan Content Publisher</Application><Template>Normal</Template><DocSecurity>0</DocSecurity>
<ScaleCrop>false</ScaleCrop><LinksUpToDate>false</LinksUpToDate><SharedDoc>false</SharedDoc><HyperlinksChanged>false</HyperlinksChanged>
</Properties>
'@

# ---------------------------------------------------------------- package
$docRels = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' + "`n" +
  '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
  '<Relationship Id="rIdS1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>' +
  '<Relationship Id="rIdS2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering" Target="numbering.xml"/>' +
  '<Relationship Id="rIdS3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings" Target="settings.xml"/>' +
  ($relList -join '') +
  '</Relationships>'

$parts = [ordered]@{
  '[Content_Types].xml'            = $contentTypes
  '_rels/.rels'                    = $rootRels
  'word/document.xml'              = $documentXml
  'word/_rels/document.xml.rels'   = $docRels
  'word/styles.xml'                = $stylesXml
  'word/numbering.xml'             = $numberingXml
  'word/settings.xml'              = $settingsXml
  'docProps/core.xml'              = $coreXml
  'docProps/app.xml'               = $appXml
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $outFile) { Remove-Item -LiteralPath $outFile -Force }
$zip = [System.IO.Compression.ZipFile]::Open($outFile, [System.IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($name in $parts.Keys) {
    $entry  = $zip.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal)
    $stream = $entry.Open()
    $bytes  = [System.Text.Encoding]::UTF8.GetBytes($parts[$name])
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Close()
  }
} finally { $zip.Dispose() }

# ---------------------------------------------------------------- verify
$doc = [xml]$documentXml
$W = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
$ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable); $ns.AddNamespace('w', $W)

Write-Output "Wrote $outFile  ($([math]::Round((Get-Item $outFile).Length/1KB,1)) KB)"
Write-Output ""
Write-Output "Paragraph count : $($doc.SelectNodes('//w:p',$ns).Count)"
Write-Output "Hyperlinks      : $($doc.SelectNodes('//w:hyperlink',$ns).Count)"
Write-Output ""
Write-Output "Style usage:"
$doc.SelectNodes('//w:pPr/w:pStyle',$ns) | ForEach-Object { $_.GetAttribute('val',$W) } |
  Group-Object | Sort-Object Name | ForEach-Object { "  {0,-16} {1}" -f $_.Name, $_.Count }
Write-Output ""
Write-Output "Titles detected (split boundaries):"
$doc.SelectNodes("//w:p[w:pPr/w:pStyle[@w:val='Title']]",$ns) | ForEach-Object {
  "  - " + (($_.SelectNodes('.//w:t',$ns) | ForEach-Object { $_.InnerText }) -join '')
}
Write-Output ""
Write-Output "Swan Label (not published):"
$doc.SelectNodes("//w:p[w:pPr/w:pStyle[@w:val='SwanLabel']]",$ns) | ForEach-Object {
  "  - " + (($_.SelectNodes('.//w:t',$ns) | ForEach-Object { $_.InnerText }) -join '')
}
Write-Output ""
Write-Output "Swan Note (stripped): $($doc.SelectNodes(""//w:p[w:pPr/w:pStyle[@w:val='SwanNote']]"",$ns).Count)"
Write-Output "Swan CTA  (lifted) : $($doc.SelectNodes(""//w:p[w:pPr/w:pStyle[@w:val='SwanCta']]"",$ns).Count)"

# ---------------------------------------------------------------- readback validation
# Re-open the written package and prove every part parses and every r:id resolves.
Write-Output ""
Write-Output "Package readback:"
$zip = [System.IO.Compression.ZipFile]::OpenRead($outFile)
try {
  $partNames = $zip.Entries | ForEach-Object { $_.FullName }
  foreach ($name in $parts.Keys) {
    if ($partNames -notcontains $name) { throw "missing part: $name" }
  }
  $partsChecked = 0
  foreach ($e in $zip.Entries) {
    if ($e.FullName -notlike '*.xml' -and $e.FullName -notlike '*.rels') { continue }
    $sr = New-Object System.IO.StreamReader($e.Open())
    $null = [xml]$sr.ReadToEnd()
    $sr.Close()
    $partsChecked++
  }
  Write-Output "  parts         : $($partsChecked) XML parts parsed OK"

  $relsXml = $zip.Entries | Where-Object { $_.FullName -eq 'word/_rels/document.xml.rels' }
  $sr = New-Object System.IO.StreamReader($relsXml.Open())
  $relsDoc = [xml]$sr.ReadToEnd(); $sr.Close()
  $relNs = 'http://schemas.openxmlformats.org/package/2006/relationships'
  $declared = @($relsDoc.Relationships.Relationship | ForEach-Object { $_.Id })
  $used = @($doc.SelectNodes('//w:hyperlink',$ns) | ForEach-Object { $_.GetAttribute('id','http://schemas.openxmlformats.org/officeDocument/2006/relationships') })
  $dangling = @($used | Where-Object { $declared -notcontains $_ })
  if ($dangling.Count -gt 0) { throw "dangling r:id -> $($dangling -join ', ')" }
  Write-Output "  relationships : $($declared.Count) declared, $($used.Count) used, 0 dangling"

  $ext = @($relsDoc.Relationships.Relationship | Where-Object { $_.TargetMode -eq 'External' })
  $google = @($ext | Where-Object { $_.Target -like '*google.com/search*' })
  if ($google.Count -gt 0) { throw "google-search placeholder links still present: $($google.Count)" }
  Write-Output "  link audit    : $($ext.Count) external links, 0 google-search placeholders"
} finally { $zip.Dispose() }
