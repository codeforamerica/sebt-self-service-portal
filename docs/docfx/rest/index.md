---
description: The portal's HTTP endpoints, generated from the API's own OpenAPI document.
keywords: swagger, openapi, rest, endpoints, http, api reference, curl
---

# REST API Reference

The HTTP surface of `SEBT.Portal.Api`, serving both the portal and the enrollment checker. This page is generated
from the API's own OpenAPI document, so it describes the endpoints as the code exposes them rather than as anyone
remembers them. This is the wire contract; for the C# types behind it, see the
[.NET API reference](../api/index.md).

Authentication is not described below. The security scheme is contributed by the state connector plugin through
`IStateAuthenticationService`, and this site is state-neutral and builds with no connector loaded, so the document
carries no security definitions even though most of these endpoints require a bearer token. Which endpoints are
protected is a property of the deployment rather than of this document.

[Download the OpenAPI document](portal.openapi.json) (OpenAPI 3.0.1, JSON). This is the same file the reference
below renders, for use with a client generator, an editor, or a request tool.

<style>
  /* RapiDoc ships sized for a full page: 100vh with its own scrollbar. Inside an article
     that produces a nested scroll region and clips everything past the fold, so the host
     is switched to flow with the document. The two rules its shadow root also needs are
     injected below, because a stylesheet cannot reach into shadow DOM.

     `!important` is required, not defensive: RapiDoc writes `height: 100vh` as an inline
     style onto its own parent element, and an inline declaration outranks a normal one.
     Without it the wrapper stays one viewport tall while the component grows past it, and
     the footer and prev/next links render on top of the reference. */
  .rest-reference,
  rapi-doc {
    display: block;
    height: auto !important;
    width: 100%;
  }
</style>

<!-- The wrapping div is load-bearing. `rapi-doc` is not a tag markdig recognizes, so an
     unwrapped element whose open tag spans several lines is escaped into a paragraph rather
     than passed through as raw HTML. A known block-level tag around it keeps the block raw. -->
<div class="rest-reference">
<rapi-doc
  spec-url="portal.openapi.json"
  render-style="view"
  schema-style="table"
  show-header="false"
  show-info="false"
  allow-try="false"
  allow-authentication="false"
  allow-server-selection="false"
  allow-spec-url-load="false"
  allow-spec-file-load="false"
  allow-search="true"
  schema-description-expanded="true"
  default-schema-tab="schema"
  font-size="large"
  regular-font="system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
  mono-font="ui-monospace, SFMono-Regular, 'SF Mono', Menlo, Consolas, 'Liberation Mono', monospace"
></rapi-doc>
</div>

<script type="module" src="rapidoc-min.js"></script>
<script>
  (function () {
    // docfx's search appends `?q=<query>` to the result href, and does it after the fragment
    // rather than before, so arriving from a search for "mailing address" gives a hash of
    // `put-/api/household/address?q=mailing address`. RapiDoc matches its section ids exactly,
    // so the query string has to come off before it reads the hash. This runs ahead of RapiDoc
    // because a classic inline script executes during parsing and the module below is deferred.
    if (window.location.hash.indexOf('?') !== -1) {
      var cleanHash = window.location.hash.replace(/\?.*$/, '');
      window.history.replaceState(null, '', window.location.pathname + window.location.search + cleanHash);
    }

    // RapiDoc themes through attributes rather than CSS variables, so it cannot inherit the
    // site's palette the way the rest of the template does. The values are read back out of
    // the stylesheet rather than repeated here: main.css maps each of these to a USWDS token
    // for both themes, so the reference gets the contrast-tested, brand-matching colors the
    // rest of the site uses, and a retheme of main.css carries over with no edit here.
    var COLOR_ATTRS = {
      'bg-color': '--bs-body-bg',
      'text-color': '--bs-body-color',
      'primary-color': '--bs-link-color',
      'nav-bg-color': '--bs-tertiary-bg',
    };

    // These elements live inside RapiDoc's shadow root, which no external stylesheet can
    // select, so the rules have to be injected once the component upgrades.
    var SHADOW_STYLES = [
      // Without these the article clips at one viewport and expanding an operation scrolls
      // inside the component instead of growing the page.
      '.body { height: auto !important; overflow: visible !important; }',
      '.main-content { height: auto !important; overflow: visible !important; }',
      // The endpoint row is a flex line of method, path, and summary. RapiDoc lets the path
      // shrink (`flex: 0 1 auto`) and breaks it mid-word, so a long summary squeezes
      // `/api/household/address` down to a few characters per line. Pinning the path to its
      // content width and breaking it normally gives the summary the leftover space instead.
      '.endpoint-head .path { flex: 0 0 auto !important; word-break: normal !important; }',
      '.endpoint-head .descr { flex: 1 1 auto !important; }',
    ].join('');

    function applyTheme(el) {
      var styles = getComputedStyle(document.documentElement);

      // Which mode is read from the background rather than from `data-bs-theme`, because that
      // attribute also carries "auto". Deciding from the colour actually in force keeps
      // RapiDoc's own derived shades on the same side as the page behind them.
      el.setAttribute('theme', isDark(styles.getPropertyValue(COLOR_ATTRS['bg-color'])) ? 'dark' : 'light');

      Object.keys(COLOR_ATTRS).forEach(function (attr) {
        var value = styles.getPropertyValue(COLOR_ATTRS[attr]).trim();
        if (value) {
          el.setAttribute(attr, value);
        }
      });
    }

    /**
     * Whether a colour is dark, by relative luminance.
     *
     * Both notations have to be handled. A custom property keeps whatever form the stylesheet
     * wrote, so `--bs-body-bg` arrives as `#111819`, while anything read off a rendered element
     * arrives as `rgb(...)`.
     */
    function isDark(color) {
      var rgb = toRgb(color);
      if (!rgb) {
        return false;
      }
      var channels = rgb.map(function (part) {
        var channel = part / 255;
        return channel <= 0.04045 ? channel / 12.92 : Math.pow((channel + 0.055) / 1.055, 2.4);
      });
      return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2] < 0.5;
    }

    function toRgb(color) {
      var value = (color || '').trim();

      var hex = value.match(/^#([0-9a-f]{3}|[0-9a-f]{6})$/i);
      if (hex) {
        var digits = hex[1];
        if (digits.length === 3) {
          digits = digits[0] + digits[0] + digits[1] + digits[1] + digits[2] + digits[2];
        }
        return [
          parseInt(digits.slice(0, 2), 16),
          parseInt(digits.slice(2, 4), 16),
          parseInt(digits.slice(4, 6), 16),
        ];
      }

      var parts = value.match(/-?\d+(\.\d+)?/g);
      return parts && parts.length >= 3 ? parts.slice(0, 3).map(Number) : null;
    }

    function injectFlowStyles(el) {
      if (!el.shadowRoot || el.shadowRoot.getElementById('rest-flow-styles')) {
        return;
      }
      var style = document.createElement('style');
      style.id = 'rest-flow-styles';
      style.textContent = SHADOW_STYLES;
      el.shadowRoot.appendChild(style);
    }

    var el = document.querySelector('rapi-doc');
    if (!el) {
      return;
    }

    applyTheme(el);
    new MutationObserver(function () {
      applyTheme(el);
    }).observe(document.documentElement, { attributes: true, attributeFilter: ['data-bs-theme'] });

    // The shadow root only exists once the custom element upgrades, which happens when the
    // module script finishes loading. Waiting on the definition rather than polling keeps this
    // correct regardless of how long that 800KB bundle takes to parse. `spec-loaded` fires
    // after RapiDoc renders, and re-running then covers any rebuild of its shadow tree.
    customElements.whenDefined('rapi-doc').then(function () {
      injectFlowStyles(el);
      applyTheme(el);
    });
    el.addEventListener('spec-loaded', function () {
      injectFlowStyles(el);
    });
  })();
</script>
