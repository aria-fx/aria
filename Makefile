# ─────────────────────────────────────────────────────────────
# ARIA — Makefile
# Build documents, slides, and .NET projects
# ─────────────────────────────────────────────────────────────

.PHONY: all docs slides architecture build test clean help

# Output directories
OUT_DIR := out
DOCS_OUT := $(OUT_DIR)/docs
SLIDES_OUT := $(OUT_DIR)/slides

# Source files
ARCH_MD := docs/architecture/aria-reference-architecture.md
SLIDES_MD := docs/slides/aria-deck.md
PANDOC_REF := docs/architecture/pandoc-reference.yaml

# Prefer Unicode-capable engines first (XeLaTeX/LuaLaTeX), then fallback to pdfLaTeX
PANDOC_PDF_ENGINE := $(shell if command -v xelatex >/dev/null 2>&1; then echo xelatex; elif command -v lualatex >/dev/null 2>&1; then echo lualatex; elif command -v pdflatex >/dev/null 2>&1; then echo pdflatex; else echo; fi)

# ── Default target ─────────────────────────────────────────
all: docs slides build

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

# ── Clean ──────────────────────────────────────────────────

clean:
	@echo "Cleaning build artifacts..."
	rm -rf $(OUT_DIR)
	find src -name bin -type d -exec rm -rf {} + 2>/dev/null || true
	find src -name obj -type d -exec rm -rf {} + 2>/dev/null || true
	@echo "Clean complete"
