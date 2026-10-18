# Manual vs. Klarrow Benchmark Protocol

**Objective:** Define a rigorous, reproducible empirical study to evaluate the speed, accuracy, and auditability of Klarrow compared with traditional manual spreadsheet-based recall investigation.

---

## 1. Study Design & Methodology

### 1.1 Participant Cohort
- **Target Size:** 12-16 participants.
- **Profile:** Food Safety & Quality Assurance (FSQA) professionals, regulatory compliance analysts, or supply chain inventory specialists with at least 2 years of experience conducting mock recalls.
- **Design:** Within-subject crossover design. Each participant evaluates two equivalent difficulty scenarios:
  - Scenario A via Manual Standard Operating Procedure (Microsoft Excel / Adobe Acrobat / Google Drive).
  - Scenario B via Klarrow Workbench.
  - Alternating scenario order to eliminate learning bias.

### 1.2 Benchmark Scenarios
Each benchmark scenario consists of an uncurated evidence bundle representing a 4-tier food manufacturing operation:
1. **Raw Material Delivery Slips:** 2 vendor delivery notes (PDF & CSV) with supplier names and lot numbers.
2. **Batch Production Log:** 1 production record (Excel spreadsheet) showing multi-ingredient recipe mixing and rework additions.
3. **Packaging Record:** 1 packing slip (text report) linking production batches to finished product lots.
4. **Distribution Dispatches:** 2 dispatch manifests (CSV) listing customer consignees.
5. **Complicating Factors (Injects):**
   - 1 ambiguous barcode typo (e.g. `RM-093340` vs. `RM-O93340`).
   - 1 mass balance quantity discrepancy (e.g. 500 kg input vs 350 kg finished without waste records).
   - 1 missing downstream customer record (unresolved gap).

---

## 2. Quantitative Evaluation Metrics

| Metric | Definition | Measurement Method | Target Hypothesis |
|--------|------------|--------------------|-------------------|
| **$T_{first}$ (Time to First Suspect Trace)** | Elapsed seconds from receiving alert to identifying initial finished product lot candidates. | Screen recording & digital timestamp. | $\ge 60\%$ reduction with Klarrow. |
| **$T_{complete}$ (Time to Complete Report)** | Elapsed seconds from alert to generating final customer distribution list and signed audit export. | Recorded session completion timestamp. | $\ge 75\%$ reduction with Klarrow. |
| **$P_{scope}$ (Affected Scope Precision)** | True positive finished lots identified / (True positives + False positives). | Scored against pre-defined ground truth key. | $\ge 98\%$ precision (exclusion of unaffected lots). |
| **$R_{scope}$ (Affected Scope Recall)** | True positive finished lots identified / (True positives + False negatives). | Scored against pre-defined ground truth key. | $100\%$ recall (no affected finished lots missed). |
| **$D_{gap}$ (Missing Evidence Detection Rate)** | Percentage of injected gaps (missing customer, missing waste log) correctly identified and documented. | Scored binary checklist (0-100%). | $\ge 90\%$ gap detection in Klarrow vs $\le 40\%$ manual. |
| **$A_{prov}$ (Provenance Grounding Ratio)** | Percentage of reported link assertions that cite an exact line/page coordinate in source documents. | Audit check of exported evidence citations. | $100\%$ grounded in Klarrow vs $\le 50\%$ in manual Excel notes. |

---

## 3. Execution Protocol

### Step 1: Pre-Test Briefing (10 minutes)
- Explain participant rights, confidentiality, and experimental goals.
- 5-minute interface orientation (for Klarrow condition) or workflow reminder (for manual condition).

### Step 2: Timed Investigation Run (Max 45 minutes)
- Hand participant a simulated FDA Recall Alert stating:  
  *"Suspect raw material lot `RM-ACCEPT-01` contains undeclared allergen / chemical contaminant. Identify all affected finished product lots, determine customer exposure, and reconcile total mass balance."*
- Start recording software (screen + audio).
- Operator conducts trace using assigned toolset.

### Step 3: Post-Test Debrief & Survey (15 minutes)
- Standardized System Usability Scale (SUS) survey (10 questions, 1-5 scale).
- Cognitive load assessment using NASA-TLX (Mental Demand, Temporal Demand, Effort, Frustration).
- Semi-structured qualitative interview regarding confidence in final recall scope.

---

## 4. Scoring Rubric & Ground Truth Key

```
+-----------------------------------------------------------------------+
| SCORING CHECKLIST (Total 100 Points)                                  |
+-----------------------------------------------------------------------+
| 1. Correct Suspect Raw Lot Identified & Isolated             [10 pts] |
| 2. All Affected Production Batches Identified                [15 pts] |
| 3. All Affected Finished Lots Identified                     [20 pts] |
| 4. Unaffected Finished Lots Correctly Excluded               [15 pts] |
| 5. All Customer Consignees Correctly Identified              [15 pts] |
| 6. Near-Match Identifier Typo Correctly Flagged/Resolved     [10 pts] |
| 7. Mass Balance Discrepancy Documented                       [10 pts] |
| 8. Missing Customer Dispatch Record Flagged as Gap           [5 pts]  |
+-----------------------------------------------------------------------+
```

---

## 5. Statistical Analysis Plan

- **Hypothesis Testing:** Paired two-tailed Student's t-test on $T_{complete}$ (completion time) and Wilcoxon signed-rank test on SUS scores.
- **Significance Level:** $\alpha = 0.05$.
- **Power Calculation:** A sample size of $N = 14$ provides $>80\%$ power to detect an effect size of $d = 0.85$ (large effect).
