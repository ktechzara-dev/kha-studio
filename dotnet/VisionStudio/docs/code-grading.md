# Barcode & 2D Code Print Quality Grading (ISO/IEC 15415 & 15416)

## 1. Overview
VisionStudio implements print quality grading compliant with ISO/IEC 15415 (2D Matrix symbols such as DataMatrix and QR Code) and ISO/IEC 15416 (1D linear barcodes).

---

## 2. Grading Metrics

| Parameter | Metric | Scale | Description |
|---|---|:---:|---|
| **DEC** | Decode | 4.0 / 0.0 | Whether the code decodes using reference decode algorithm |
| **SC** | Symbol Contrast | 4.0 to 0.0 | $R_{\max} - R_{\min}$ across symbol reflectance ($A \ge 70\%$, $B \ge 55\%$, $C \ge 40\%$, $D \ge 20\%$, $F < 20\%$) |
| **MOD** | Modulation | 4.0 to 0.0 | Ratio of minimum edge contrast to symbol contrast |
| **ANU** | Axial Non-Uniformity | 4.0 to 0.0 | Aspect ratio distortion along X and Y symbol axes ($A \le 6\%$) |
| **DEF** | Defects | 4.0 to 0.0 | Presence of voids in dark modules or spots in light modules |

### Overall Grade
Per ISO/IEC standard rules, the overall symbol grade equals the **lowest** grade among all individual tested metrics:
$$\text{Overall Grade} = \min(\text{DEC}, \text{SC}, \text{MOD}, \text{ANU}, \text{DEF})$$
Reported as letter grade `A` (4.0), `B` (3.0), `C` (2.0), `D` (1.0), `F` (0.0).
