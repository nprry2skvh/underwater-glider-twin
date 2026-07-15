# Task 2 report — slide content specification

Status: complete

Created `paper-ppt-assets/slide_spec.json` with 15 ordered slide records (title through conclusion/references). Every record includes `slide`, `title`, `takeaway`, `bullets`, `assets`, `animation_group`, and `source_reference`; bullets are capped at four items. The exact reported comparisons (11% vs 44%, Fig. 8 deltas, approximately 3 m Arctic discrepancy, and <15 W) are included only on the relevant slides.

## Validation

Command:

```powershell
$p='.superpowers\\sdd\\paper-ppt-assets\\slide_spec.json'; $j=Get-Content -Raw $p | ConvertFrom-Json; "slides=$($j.Count)"; $j | % { if ($_.bullets.Count -gt 4){throw "too many"}; foreach($f in 'slide','title','takeaway','bullets','assets','animation_group','source_reference'){if($null -eq $_.$f){throw "missing $f"}} }; 'VALID'
```

Output:

```
slides=15
VALID
```

Commit: initial Task 2 commit `17efc2526126cf5bd7e0ded837523c0da4147631`; follow-up language/source fix commit recorded below.

Concerns: The design-spec file is mojibake-encoded in the workspace; slide copy follows the unambiguous sequence and source-facts JSON. Slide 14 is explicitly labeled as an application inference rather than a paper-reported result.

Follow-up fix: slides 3–7 were translated to Chinese while retaining technical abbreviations; slide 13 now uses a sourced measurement-condition caveat; slide 14 visibly marks its content as application inference.
