#!/usr/bin/env bash
set -euo pipefail

echo "═══════════════════════════════════════════════════════════"
echo "  ARIA Development Environment Setup"
echo "═══════════════════════════════════════════════════════════"

# ── Pandoc ──────────────────────────────────────────────────
echo "Installing Pandoc..."
sudo apt-get update -qq
sudo apt-get install -y -qq pandoc > /dev/null 2>&1
echo "  Pandoc $(pandoc --version | head -1) installed"

# ── LaTeX (for Pandoc PDF output) ──────────────────────────
echo "Installing TeX Live (minimal for PDF generation)..."
sudo apt-get update -qq
sudo apt-get install -y -qq texlive-latex-recommended texlive-latex-extra \
  texlive-fonts-recommended texlive-fonts-extra texlive-xetex lmodern librsvg2-bin > /dev/null 2>&1
echo "  TeX Live installed"

# ── Marp CLI ────────────────────────────────────────────────
echo "Installing Marp CLI..."
npm install -g @marp-team/marp-cli
echo "  Marp CLI $(marp --version) installed"

# ── ORAS CLI (OCI artifact operations) ─────────────────────
echo "Installing ORAS CLI..."
ORAS_VERSION="1.2.2"
curl -sLO "https://github.com/oras-project/oras/releases/download/v${ORAS_VERSION}/oras_${ORAS_VERSION}_linux_amd64.tar.gz"
tar -xzf "oras_${ORAS_VERSION}_linux_amd64.tar.gz" -C /tmp oras
sudo mv /tmp/oras /usr/local/bin/
rm "oras_${ORAS_VERSION}_linux_amd64.tar.gz"
echo "  ORAS $(oras version | head -1) installed"

# ── .NET restore ────────────────────────────────────────────
echo "Restoring .NET projects..."
if [ -f "src/sample-agent/Oasf.Sample.sln" ]; then
  dotnet restore src/sample-agent/Oasf.Sample.sln --verbosity quiet || true
fi
if [ -f "src/aria-cli/Aria.Cli.csproj" ]; then
  dotnet restore src/aria-cli/Aria.Cli.csproj --verbosity quiet || true
fi

# ── Terraform init ──────────────────────────────────────────
echo "Initializing Terraform (skip provider download)..."
cd src/terraform
terraform init -backend=false > /dev/null 2>&1 || true
cd ..

# ── Summary ─────────────────────────────────────────────────
echo ""
echo "═══════════════════════════════════════════════════════════"
echo "  ARIA environment ready!"
echo ""
echo "  Tools installed:"
echo "    pandoc     $(pandoc --version | head -1 | awk '{print $2}')"
echo "    marp       $(marp --version 2>/dev/null || echo 'installed')"
echo "    terraform  $(terraform version -json 2>/dev/null | grep -o '"[0-9.]*"' | head -1 || echo 'installed')"
echo "    dotnet     $(dotnet --version)"
echo "    oras       $(oras version 2>/dev/null | head -1 || echo 'installed')"
echo "    node       $(node --version)"
echo ""
echo "  Quick start:"
echo "    make docs      # Build all documents (PDF + PPTX + DOCX)"
echo "    make slides    # Build slide deck only"
echo "    make build     # Build .NET projects"
echo "    make all       # Everything"
echo "═══════════════════════════════════════════════════════════"
