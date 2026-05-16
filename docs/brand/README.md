# ARIA Brand Assets

Logo files and usage guidelines for the ARIA project.

## Logo Variants

| File                       | Use case                                  | Background             |
| -------------------------- | ----------------------------------------- | ---------------------- |
| `aria-logo-primary.svg`    | Docs hero, slide title, print             | Transparent (light bg) |
| `aria-logo-mark.svg`       | Mark only, no wordmark                    | Transparent            |
| `aria-logo-horizontal.svg` | README header, footer bars, narrow spaces | Transparent (light bg) |
| `aria-logo-mono-light.svg` | Dark mark on light backgrounds            | Transparent            |
| `aria-logo-mono-dark.svg`  | Light mark on dark backgrounds            | Dark (#1E1B2E)         |
| `aria-favicon.svg`         | Browser tab, npm, package icon            | Purple (#6B21A8)       |
| `aria-avatar.svg`          | GitHub profile, circular contexts         | Purple (#6B21A8)       |

## Color Palette

| Color          | Hex       | Usage                             |
| -------------- | --------- | --------------------------------- |
| Primary purple | `#6B21A8` | Logo, headings, primary brand     |
| Cyan           | `#0891B2` | Metamodel layer, satellite node   |
| Teal           | `#0D9488` | Marketplace layer, satellite node |
| Coral          | `#F97316` | Governance layer, satellite node  |
| Dark base      | `#1E1B2E` | Dark backgrounds, text on light   |

## Logo Anatomy

The ARIA mark represents the core concept of the framework:

- **Hexagonal hub** — an [OASF](https://schema.oasf.outshift.com/) Record, the canonical data structure
- **Outer ring** — the registry boundary (OCI, GitHub)
- **"A" letterform** — ARIA identity
- **Four satellite nodes** — the four relationship types radiating from
  every AI asset (invokes, governed_by, grounded_in, composed_by)
- **Four architecture lines** — metamodel, marketplace, governance, and
  distribution (three primary colored lines plus a fourth faded line)

## Usage Rules

- Minimum clear space: half the mark width on all sides
- Minimum size: 24×24px (favicon variant) or 32×32px (mark)
- Never rotate, skew, or add effects (shadows, gradients, glow)
- Never change the satellite node colors
- On dark backgrounds, use `aria-logo-mono-dark.svg` or `aria-avatar.svg`
- On light backgrounds, use `aria-logo-primary.svg` or `aria-logo-mono-light.svg`

## Generating PNGs

```bash
# Requires rsvg-convert (librsvg2-bin, installed by devcontainer)
rsvg-convert -w 512 aria-favicon.svg > aria-favicon-512.png
rsvg-convert -w 64 aria-favicon.svg > aria-favicon-64.png
rsvg-convert -w 256 aria-avatar.svg > aria-avatar-256.png
```
