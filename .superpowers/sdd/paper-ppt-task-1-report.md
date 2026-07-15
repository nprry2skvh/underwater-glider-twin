# Task 1 report — paper assets and source facts

Status: COMPLETE

## Files created

- `.superpowers/sdd/paper-ppt-assets/fig_system_architecture.png` (2000×588)
- `.superpowers/sdd/paper-ppt-assets/fig_bmc.png` (4200×1028)
- `.superpowers/sdd/paper-ppt-assets/fig_bathymetry.png` (4200×2700)
- `.superpowers/sdd/paper-ppt-assets/fig_arctic_survey.png` (2000×2555)
- `.superpowers/sdd/paper-ppt-source-facts.json`

The four images are direct figure extractions from the supplied PDF (Fig. 3, Fig. 5, Fig. 8, and Fig. 9 respectively), converted to true PNG files without resizing. Captions retain the paper's figure numbers and wording in the JSON.

## Verification

Commands run:

```powershell
python -c "import fitz; ..."                 # inspected 11-page PDF, text, image xrefs, captions
python -c "... d.extract_image(x) ..."        # extracted xrefs 132, 142, 157, 165
python -c "from PIL import Image; ..."        # converted extracted images to PNG and checked dimensions
python -c "import json; json.load(open(...))" # JSON parse check
```

Observed image dimensions: 2000×588, 4200×1028, 4200×2700, and 2000×2555 pixels. At a 16:9 slide width of 13.33 in these correspond to approximately 150–315 dpi, so labels remain readable when placed at presentation scale.

## Concerns

- PDF figure resources are stored as JPEG/PNG streams; PNG conversion is lossless with respect to decoded pixels but cannot restore detail absent from the source stream.
- Figure 8 caption reports truncated display ranges; the JSON preserves that caveat and does not infer additional metrics.
- The paper describes the Arctic comparison as an approximately 3-m discrepancy and vessel-depth-sounder verification; no stronger accuracy claim is made.
