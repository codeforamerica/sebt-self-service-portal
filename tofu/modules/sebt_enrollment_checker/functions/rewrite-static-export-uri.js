// The static export writes flat files, so a direct request for /check asks S3
// for a key named "check" and misses. main.tf's custom_error_response blocks
// then serve index.html, which the app hydrates as the landing page — so deep
// links and refreshes show the wrong page, and a typo does the same with a 200,
// leaving 404.html unreachable. Only in-app navigation escaped this, because
// the client router never requests HTML.
//
//   /check      -> /check.html     real route, served
//   /check/     -> /check.html     trailing slash, same page
//   /foo        -> /foo.html       no such object, falls through to 404.html
//   /           -> /               default_root_object serves index.html
//   /config.js  -> /config.js      already has an extension, left alone
function handler(event) {
  var request = event.request;
  var uri = request.uri;

  // CloudFront's default_root_object serves this.
  if (uri === '/') {
    return request;
  }

  // '/example/' and '/example' address the same page.
  if (uri.charAt(uri.length - 1) === '/') {
    uri = uri.slice(0, -1); // removes the trailing /
  }
  // last segment '/example' becomes 'example'
  var lastSegment = uri.slice(uri.lastIndexOf('/') + 1);

  if (lastSegment.indexOf('.') === -1) {
    request.uri = uri + '.html';
  }

  return request;
}
