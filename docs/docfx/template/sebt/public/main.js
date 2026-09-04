/**
 * Template overrides for the `modern` docfx template.
 *
 * The only hook this docfx version exposes for our purpose is `start`, which
 * runs once at page startup. `postRender` does not exist in 2.78, so anything
 * that touches rendered markup waits for DOMContentLoaded here.
 *
 * Two things happen: the footer gains a build stamp, and a version picker
 * appears when more than one release has been published. Both read JSON at
 * runtime rather than being baked into pages, so a rebuild is not needed to
 * correct them, and both fail quietly, because a missing build stamp should
 * never take the documentation down with it.
 */

/** Resolves a site-root-relative path from a page at any depth. */
function siteRoot() {
  const rel = document.querySelector('meta[name="docfx:rel"]');
  return rel ? rel.getAttribute('content') : '';
}

async function loadJson(name) {
  try {
    const response = await fetch(`${siteRoot()}${name}`);
    return response.ok ? await response.json() : null;
  } catch {
    return null;
  }
}

function renderBuildStamp(footer, build) {
  const stamp = document.createElement('div');
  stamp.className = 'build-stamp';

  const commit = build.dirty ? `${build.commit} plus uncommitted changes` : build.commit;
  stamp.textContent = `Version ${build.version} · commit ${commit} · built ${build.builtAt.slice(0, 10)}`;
  footer.querySelector('.container-xxl, .container, div')?.appendChild(stamp) ?? footer.appendChild(stamp);
}

function renderVersionPicker(versions) {
  // One release is not a choice, so the picker stays out of the way until a
  // second one exists.
  if (!Array.isArray(versions) || versions.length < 2) {
    return;
  }

  const navbar = document.querySelector('#navbar, .navbar-nav');
  if (!navbar) {
    return;
  }

  const current = versions.find((v) => v.current) ?? versions[0];
  const picker = document.createElement('select');
  picker.className = 'version-picker form-select form-select-sm';
  picker.setAttribute('aria-label', 'Documentation version');

  for (const entry of versions) {
    const option = document.createElement('option');
    option.value = entry.url;
    option.textContent = entry.label;
    option.selected = entry === current;
    picker.appendChild(option);
  }

  picker.addEventListener('change', () => {
    window.location.href = picker.value;
  });
  navbar.appendChild(picker);
}

async function decorate() {
  const footer = document.querySelector('footer');
  const [build, versions] = await Promise.all([loadJson('version.json'), loadJson('versions.json')]);

  if (footer && build) {
    renderBuildStamp(footer, build);
  }
  renderVersionPicker(versions);
}

export default {
  start: () => {
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', decorate);
    } else {
      decorate();
    }
  },
};
