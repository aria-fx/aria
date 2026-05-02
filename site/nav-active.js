(function () {
  function normalize(pathname) {
    var p = pathname || "/";
    p = p.replace(/\/index\.html$/, "/");
    p = p.replace(/\.html$/, "");
    p = p.replace(/\/$/, "");
    return p || "/";
  }

  var current = normalize(window.location.pathname);
  var links = document.querySelectorAll(".side-nav a[href]");
  var best = null;
  var bestLen = -1;

  links.forEach(function (link) {
    var target = new URL(link.getAttribute("href"), window.location.href);
    var candidate = normalize(target.pathname);

    var isMatch = current === candidate || (candidate !== "/" && current.indexOf(candidate + "/") === 0);
    if (isMatch && candidate.length > bestLen) {
      best = link;
      bestLen = candidate.length;
    }
  });

  if (best) {
    best.classList.add("active");
    best.setAttribute("aria-current", "page");

    var heading = best.previousElementSibling;
    while (heading && !heading.classList.contains("side-nav-title")) {
      heading = heading.previousElementSibling;
    }

    if (heading) {
      heading.classList.add("active-group");
    }
  }
})();
