# ─────────────────────────────────────────────────────────────
# ARIA — Makefile
# Build documents, slides, and .NET projects
# ─────────────────────────────────────────────────────────────

.PHONY: all docs slides architecture build test clean help site watch-site aria-npm-pack aria-npm-pack-dry-run

# Output directories
OUT_DIR := out
DOCS_OUT := $(OUT_DIR)/docs
SLIDES_OUT := $(OUT_DIR)/slides

# Site sources and outputs
SITE_TEMPLATE := site/template.html
SITE_LINK_FILTER := scripts/pandoc-md-links-to-html.lua
SITE_BRAND_SRC := $(wildcard docs/brand/*.svg)
SITE_BRAND_OUT := $(patsubst docs/brand/%,site/brand/%,$(SITE_BRAND_SRC))
SITE_ARCH_SVG_SRC := $(wildcard docs/architecture/*.svg)
SITE_ARCH_SVG_OUT := $(patsubst docs/architecture/%,site/docs/%,$(SITE_ARCH_SVG_SRC))
SITE_FAVICON_SRC := docs/brand/aria-favicon.svg
SITE_FAVICON_OUT := site/favicon.svg
SITE_TUTORIAL_SRC := $(filter-out tutorial/README.md,$(wildcard tutorial/*.md))
SITE_TUTORIAL_HTML := $(patsubst tutorial/%.md,site/tutorial/%.html,$(SITE_TUTORIAL_SRC))
SITE_DOCS_HTML := site/docs/architecture.html

# Source files
ARCH_MD := docs/architecture/aria-reference-architecture.md
SLIDES_MD := docs/slides/aria-deck.md
PANDOC_REF := docs/architecture/pandoc-reference.yaml

# Prefer Unicode-capable engines first (XeLaTeX/LuaLaTeX), then fallback to pdfLaTeX
PANDOC_PDF_ENGINE := $(shell if command -v xelatex >/dev/null 2>&1; then echo xelatex; elif command -v lualatex >/dev/null 2>&1; then echo lualatex; elif command -v pdflatex >/dev/null 2>&1; then echo pdflatex; else echo; fi)

# ── Default target ─────────────────────────────────────────
all: docs slides build site

# ── Help ───────────────────────────────────────────────────
help:
	@echo "ARIA Build Targets"
	@echo "══════════════════════════════════════════════════"
	@echo "  make all           Build everything"
	@echo "  make docs          Build architecture doc (PDF + DOCX)"
	@echo "  make slides        Build slide deck (PPTX + PDF + HTML)"
	@echo "  make architecture  Build reference architecture doc only"
	@echo "  make build         Build .NET projects"
	@echo "  make test          Run .NET tests"
	@echo "  make site          Build GitHub Pages HTML from markdown sources"
	@echo "  make watch-site    Rebuild site on markdown changes (requires inotify-tools)"
	@echo "  make aria-npm-pack Build npm tarball from src/aria-cli/npm"
	@echo "  make aria-npm-pack-dry-run Validate npm tarball from src/aria-cli/npm"
	@echo "  make clean         Remove build artifacts"
	@echo ""
	@echo "Individual targets:"
	@echo "  make docx          Architecture doc as Word"
	@echo "  make pdf           Architecture doc as PDF"
	@echo "  make pptx          Slide deck as PowerPoint"
	@echo "  make slides-pdf    Slide deck as PDF"
	@echo "  make slides-html   Slide deck as HTML"

# ── Create output directories ──────────────────────────────
$(DOCS_OUT) $(SLIDES_OUT):
	@mkdir -p $@

# ── Architecture Document ──────────────────────────────────

docs: architecture
architecture: pdf docx

# PDF via Pandoc + LaTeX
pdf: $(DOCS_OUT)/aria-reference-architecture.pdf
$(DOCS_OUT)/aria-reference-architecture.pdf: $(ARCH_MD) | $(DOCS_OUT)
	@echo "Building architecture doc → PDF..."
	@if [ -z "$(PANDOC_PDF_ENGINE)" ]; then \
		echo "Error: no LaTeX PDF engine found (expected xelatex, lualatex, or pdflatex)."; \
		echo "Install TeX Live or pass a different --pdf-engine supported by pandoc."; \
		exit 1; \
	fi
	pandoc $(ARCH_MD) \
		--pdf-engine=$(PANDOC_PDF_ENGINE) \
		--toc --toc-depth=3 \
		--number-sections \
		-V geometry:margin=1in \
		-V fontsize=11pt \
		-V mainfont="Latin Modern Roman" \
		-V monofont="DejaVu Sans Mono" \
		-V colorlinks=true \
		-V linkcolor=purple \
		--highlight-style=tango \
		-o $@
	@echo "  using pandoc PDF engine: $(PANDOC_PDF_ENGINE)"
	@echo "  → $@"

# DOCX via Pandoc
docx: $(DOCS_OUT)/aria-reference-architecture.docx
$(DOCS_OUT)/aria-reference-architecture.docx: $(ARCH_MD) | $(DOCS_OUT)
	@echo "Building architecture doc → DOCX..."
	pandoc $(ARCH_MD) \
		--toc --toc-depth=3 \
		--number-sections \
		--highlight-style=tango \
		-o $@
	@echo "  → $@"

# ── Slide Deck ─────────────────────────────────────────────

slides: pptx slides-pdf slides-html

# PPTX via Marp
pptx: $(SLIDES_OUT)/aria-deck.pptx
$(SLIDES_OUT)/aria-deck.pptx: $(SLIDES_MD) | $(SLIDES_OUT)
	@echo "Building slide deck → PPTX..."
	marp $(SLIDES_MD) \
		--pptx \
		--allow-local-files \
		-o $@
	@echo "  → $@"

# PDF via Marp
slides-pdf: $(SLIDES_OUT)/aria-deck.pdf
$(SLIDES_OUT)/aria-deck.pdf: $(SLIDES_MD) | $(SLIDES_OUT)
	@echo "Building slide deck → PDF..."
	marp $(SLIDES_MD) \
		--pdf \
		--allow-local-files \
		-o $@
	@echo "  → $@"

# HTML via Marp (self-contained)
slides-html: $(SLIDES_OUT)/aria-deck.html
$(SLIDES_OUT)/aria-deck.html: $(SLIDES_MD) | $(SLIDES_OUT)
	@echo "Building slide deck → HTML..."
	marp $(SLIDES_MD) \
		--html \
		--allow-local-files \
		-o $@
	@echo "  → $@"

# ── GitHub Pages Site ──────────────────────────────────────

site: $(SITE_BRAND_OUT) $(SITE_FAVICON_OUT) $(SITE_ARCH_SVG_OUT) $(SITE_DOCS_HTML) site/tutorial/index.html $(SITE_TUTORIAL_HTML)
	@echo "Site build complete (site/docs/ and site/tutorial/)"

# Brand assets for logos/avatars used by site pages
site/brand/%: docs/brand/% | site/brand
	@echo "  [site] $< → $@"
	@cp $< $@

# Favicon asset for site pages
$(SITE_FAVICON_OUT): $(SITE_FAVICON_SRC)
	@echo "  [site] $< → $@"
	@cp $< $@

# Architecture SVG diagrams used by docs pages
site/docs/%.svg: docs/architecture/%.svg | site/docs
	@echo "  [site] $< → $@"
	@cp $< $@

# Architecture doc → HTML page
$(SITE_DOCS_HTML): docs/architecture/aria-reference-architecture.md $(SITE_TEMPLATE) $(SITE_LINK_FILTER) | site/docs
	@echo "  [site] $< → $@"
	@pandoc $< \
		--from markdown --to html5 \
		--standalone \
		--template $(SITE_TEMPLATE) \
		--lua-filter $(SITE_LINK_FILTER) \
		--variable root=../ \
		--toc --toc-depth=3 \
		--output $@

# Tutorial README → index
site/tutorial/index.html: tutorial/README.md $(SITE_TEMPLATE) $(SITE_LINK_FILTER) | site/tutorial
	@echo "  [site] $< → $@"
	@pandoc $< \
		--from markdown --to html5 \
		--standalone \
		--template $(SITE_TEMPLATE) \
		--lua-filter $(SITE_LINK_FILTER) \
		--variable root=../ \
		--metadata title="Tutorial" \
		--output $@

# Tutorial modules (pattern rule — all non-README tutorial pages)
site/tutorial/%.html: tutorial/%.md $(SITE_TEMPLATE) $(SITE_LINK_FILTER) | site/tutorial
	@echo "  [site] $< → $@"
	$(eval TITLE := $(shell echo '$*' | sed 's/^[0-9]*-//' | sed 's/-/ /g'))
	@pandoc $< \
		--from markdown --to html5 \
		--standalone \
		--template $(SITE_TEMPLATE) \
		--lua-filter $(SITE_LINK_FILTER) \
		--variable root=../ \
		--metadata title="$(TITLE)" \
		--toc \
		--output $@

# Create site output dirs on demand
site/docs site/tutorial site/brand:
	@mkdir -p $@

# Watch markdown sources and rebuild site on any change (requires inotify-tools)
watch-site:
	@command -v inotifywait >/dev/null 2>&1 || { \
		echo "inotifywait not found. Install with: sudo apt-get install inotify-tools"; \
		exit 1; \
	}
	@echo "Watching docs/ and tutorial/ for changes (Ctrl-C to stop)..."
	@while true; do \
		inotifywait -q -e close_write,create,delete,moved_to \
			docs/architecture/ tutorial/ site/template.html && \
		$(MAKE) site; \
	done

# ── .NET Build ─────────────────────────────────────────────

build:
	@echo "Building sample agent..."
	dotnet build src/sample-agent/Oasf.Sample.sln --verbosity quiet
	@echo "Building ARIA CLI..."
	dotnet build src/aria-cli/Aria.Cli.csproj --verbosity quiet
	@echo "Build complete"

test:
	@echo "Running tests..."
	dotnet test src/sample-agent/Oasf.Sample.sln --verbosity quiet
	@echo "Tests complete"

# ── ARIA npm Package ──────────────────────────────────────

aria-npm-pack:
	@echo "Packing aria CLI npm package from src/aria-cli/npm..."
	cd src/aria-cli/npm && npm ci && npm pack
	@echo "npm pack complete"

aria-npm-pack-dry-run:
	@echo "Validating aria CLI npm package from src/aria-cli/npm..."
	cd src/aria-cli/npm && npm ci && npm run check:dist && npm pack --dry-run
	@echo "npm dry-run pack complete"

# ── Clean ──────────────────────────────────────────────────

clean:
	@echo "Cleaning build artifacts..."
	rm -rf $(OUT_DIR)
	rm -rf site/docs site/tutorial
	find src -name bin -type d -exec rm -rf {} + 2>/dev/null || true
	find src -name obj -type d -exec rm -rf {} + 2>/dev/null || true
	@echo "Clean complete"
