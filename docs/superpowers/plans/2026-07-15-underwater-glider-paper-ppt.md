# 水下滑翔机论文组会 PPT Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a 15-page Chinese academic PPT about the provided underwater-glider navigation paper, with a Tianjin University-inspired blue theme, original paper figures, and restrained academic transitions.

**Architecture:** Extract paper figures and text facts from the local PDF, build slides with PptxGenJS, and add consistent slide-level OOXML transitions after generation. Keep content/data in a slide specification object so layout and copy can be reviewed independently of rendering.

**Tech Stack:** Node.js, PptxGenJS, PowerPoint OOXML zip editing, local PDF extraction via PyMuPDF, original PDF figures.

## Global Constraints

- 16:9 widescreen; deep blue, light blue, and gold accent palette.
- Chinese content; preserve technical abbreviations, variables, DOI, and bibliographic titles where useful.
- Exactly 15 pages or fewer; target 15 pages including title, paper-introduction, conclusion, and references.
- Use original paper figures wherever suitable; redraw only explanatory flow diagrams.
- Do not invent metrics or conclusions absent from the paper.
- Animation style: restrained fade/wipe/smooth transitions; static view must remain complete.
- Output: `D:\Desktop\水下滑翔机论文组会汇报_中文.pptx`.

---

### Task 1: Prepare paper assets and source facts

**Files:**
- Create: `ppt_assets/` extracted PNGs and a source-facts JSON.
- Read: `D:\Desktop\An Autonomous Underwater Glider With Improved.pdf`

- [ ] Extract the paper’s figures, captions, author information, experimental locations, sensor names, and reported comparison statements into local assets.
- [ ] Name assets by slide purpose (`fig_system_architecture.png`, `fig_bmc.png`, `fig_bathymetry.png`, `fig_arctic_survey.png`).
- [ ] Verify each extracted figure is readable at 16:9 presentation scale and retain the original caption/figure number in the source-facts file.

### Task 2: Create the slide content specification

**Files:**
- Create: `ppt_assets/slide_spec.json`

- [ ] Define 15 slide records: title; paper/authors; background; navigation challenge; contributions; system architecture; DVL-Odo; carrot following; platform/sensors; Puerto Rico survey; Arctic survey; error/accuracy; discussion/limitations; digital-twin implications; conclusion/references.
- [ ] For every slide define `title`, `takeaway`, `bullets`, `assets`, `animation_group`, and `source_reference`.
- [ ] Keep each content slide to one takeaway and no more than four short bullets.
- [ ] Put the paper DOI, journal, year, authors, and author-background summary on slide 2.

### Task 3: Implement the PPTX visual system

**Files:**
- Create: `scripts/build_underwater_glider_ppt.js`
- Create: `scripts/add_ppt_transitions.js`

- [ ] Implement a reusable theme: deep navy background/header, Tianjin-University-inspired gold rule, blue cards, consistent Chinese font fallback, slide number, and source footer.
- [ ] Implement reusable helpers for title, section label, body card, figure panel, callout, arrow flow, and citation footer.
- [ ] Use original figures on experiment and system slides; draw only the explanatory sensor-to-navigation-to-control flow and digital-twin mapping.
- [ ] Generate all 15 slides from `slide_spec.json`; use short text blocks sized for 10–15 minute group-meeting delivery.

### Task 4: Add restrained animations/transitions

**Files:**
- Modify: `scripts/add_ppt_transitions.js`
- Output: `D:\Desktop\水下滑翔机论文组会汇报_中文.pptx`

- [ ] Add a consistent fade or smooth slide transition to each slide through presentation OOXML.
- [ ] Add progressive reveal ordering where supported: method flow left-to-right, experiment image before interpretation, and conclusion bullets last.
- [ ] Ensure transitions do not alter the static slide content or create hidden/off-slide objects.

### Task 5: Verify and review the deck

**Files:**
- Verify: `D:\Desktop\水下滑翔机论文组会汇报_中文.pptx`
- Create: `ppt_assets/verification.json`

- [ ] Open the generated package as a ZIP and verify all slide XML, media relationships, and transition XML are valid and present.
- [ ] Confirm slide count ≤15, all expected original-figure assets are embedded, and no text or image extends outside the slide bounds in the generated geometry.
- [ ] Render representative slides to images and inspect title, paper-introduction, system architecture, one experiment, and conclusion slides for clipping and contrast.
- [ ] Record verification results and any known limitation (PowerPoint version-dependent element-level entrance animations) in `verification.json`.

### Task 6: Final handoff

- [ ] Provide the clickable PPTX path and briefly state the page count, animation behavior, and any remaining limitation.
- [ ] Do not claim the deck is complete until Task 5 verification output confirms it.
