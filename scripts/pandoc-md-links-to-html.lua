-- Rewrite local markdown links to .html for generated static pages.
-- Keeps external links and anchors unchanged.

function Link(el)
  local target = el.target

  -- Leave absolute URLs, mailto links, and bare anchors unchanged.
  if target:match("^https?://") or target:match("^mailto:") or target:match("^#") then
    return el
  end

  -- Preserve fragment after extension rewrite, e.g. README.md#x -> README.html#x
  local base, fragment = target:match("^([^#]+)(#.*)$")
  if base == nil then
    base = target
    fragment = ""
  end

  -- Rewrite only markdown files.
  if base:match("%.md$") or base:match("%.markdown$") then
    base = base:gsub("%.markdown$", ".html"):gsub("%.md$", ".html")
    el.target = base .. fragment
  end

  return el
end
