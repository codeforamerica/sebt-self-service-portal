# Vendor-agnostic, Privacy-aware, Data Layer

## Problem Statement / Intent

We need  a **robust, application-level data layer** that:

- Acts as the **single source of truth** for page, user, and event
  metadata
- Supports **privacy-aware, scoped access** to data
- Emits **DOM events** when data changes for analytics / tag managers
- Is **framework-agnostic** and works in plain browser environments

This implementation is intended to support web analytics (i.e. Google
Analytics, Amplitude), and downstream consumers without coupling the
application to any specific vendor SDK.


------------------------------------------------------------------------

## High-Level Architecture

The Data Layer object **must**:

- Own the data structure and expose a corresponding API
- Enforce read access rules
- Emit events when data mutates
- Store privacy / scope metadata separately from data
- Emit `CustomEvent`s on `document` allowing loose coupling to tooling


The Data Layer API **must** be bound to the root data structure and
*should be* exposed via a window object named `digitalData`.

**Note:** The proposals outlined are based upon the recommendations
contained within the [W3C Customer Experience Digital Data Report](https://www.w3.org/2013/12/ceddl-201312.pdf)

------------------------------------------------------------------------

## Data Structure
The root data structure is detailed below:

		{
		  page: {
		    category: {},
		    attribute: {}
		  },
		  user: {
		    profile: {}
		  },
		  event: [],
		  privacy: {
		    accessCategories: [...]
		  },
		  initialized: true
		}


### Data Elements

#### Page Data

Page data elements represent context and information about the current
page / view and populate the `<root>.page` object. They are simple
key-value pairs, where the value can be of any type (including
objects.) Within the page data object, there are two sub-objects that
may be optionally populated (depending on implementation needs):

- Attributes
- Category

By default there are no privacy restrictions on page data elements.

#### User Data

User data elements describe attributes about the current
user/session. User data is referenced in the data
layer from within the `<root>.user` path; this object contains a
`profile` sub-object. This can be used to capture and distinguish from
i.e. information provided via the application and that provided by 2nd
or 3rd party systems. Given the potential sensitivity involved, all user data
is privacy restricted by default, so reporting to eg. a web analytics
scope must be explicitly granted.

### Events

Events are what happens in the product; these could be triggered by
user actions or by the application itself. Events can also contain
event data which should reference the elements stored elsewhere within
the data layer and are captured as a collection of event objects
within the  `<root>.event[]` array. Each event object has the
following structure:


		{
		  eventName: string,
		  eventData: object,
		  timeStamp: number,
		  scope: string[]
		}


### Data Scoping

To support both privacy and transparency, privacy scoping is enforced
globally on the Data Layer object, allowing  for the restriction of
access to any element(s). The relationship between scopes and
applications *should be* stored within the
`<root>.privacy.accessCategories` object. Every data element may have
an associated `_scope` array, defining read access. If a scope is
defined for an element, it **must** only be readable by tooling that
is assigned that scope. If no scope is defined on an element, the
scope of its parent element is checked; if no scope is found the
element is publicly readable.

Scopes are strings such as:

- default
- analytics

The default scope is reserved for the application itself.
## Data Layer API
The interface for the data layer object itself consists of a set of
functions for reading/writing data elements and for tracking
events. These functions should be bound to the root data structure to
produce the public API.


### Constructor
The constructor initializes the data layer and binds it to `window[root]`. If
`bootstrap` is provided it should be parsed as JSON to populate the
initial state. Upon success it must emit `DataLayer:Initialized`.

		DataLayer(root: string, bootstrap?: { text?: string })

### Page Load Tracking

Page load/view events are distinct from general events and must be
tracked via a dedicated method:

		DataLayer#pageLoad(data?: Record<string, unknown>): void

Calling this method signals that a page view has occurred. Optionally,
a map of data elements (key-value pairs of any type) can be passed to
annotate the page view with additional context (e.g. page name, section).
This method must be exposed publicly on the root data structure:

		<root>.pageLoad(data?)

At the DOM bridge level, a `<root>:PageViewed` DOM event is emitted
(distinct from `<root>:EventTracked`), which the bridge maps to the
vendor's dedicated page view tracking mechanism — allowing vendors to
apply their own page view enrichment, session stitching, and default
property decoration. See [DOM Bridge & Sample Integration](#dom-bridge--sample-integration)
for an example.

### Event Tracking

All non-page-load events are tracked via `trackEvent`:

		DataLayer#trackEvent(name: string, data?: Record<string, unknown>): void

Passing in an event name will append an event object to the
`<root>.event` array. Optionally, event data (of any type) can be
included along with the event. This method must be exposed publicly on
the root data structure:

		<root>.trackEvent(eventName, eventData?)

### Interacting with Data Elements

#### Reading Data
All data elements can be read using the public `getElement` method
with the following signature:

		DataLayer#getElement(path: string, scope?: string, defaultValue?: unknown): unknown

This method should return the value of the element at the given
path (relative to the root object i.e. `page.name` or
`user.isAuthenticated`) if-and-only-if the provided `scope` would
grant access. If the value does not exist, or read access is not
granted, the method should return the `defaultValue`, if provided.


### Writing Data
The public API consists of multiple convenience functions that
obey the same contract and share the same signature:

		_setElement(path: string, value: unknown, scope?: string | string[]): void


This functions stores `value` at a specified `path` relative to the
data layer `<root>`. One or more `scope`s may be optionally specified
and so the function should accept a string or an array of strings.


**NOTE**: The convience methods that operate on user data elements
(`setUserData` and `setUserProfile`) **must** include the `default` scope
when storing data, even if no scope is provided.

The functions that comprise the public write API are:

		DataLayer#setUserData(path: string, value: unknown, scope?: string | string[]): void
		DataLayer#setUserProfile(path: string, value: unknown, scope?: string | string[]): void
		DataLayer#setPageData(path: string, value: unknown, scope?: string | string[]): void
		DataLayer#setPageCategory(path: string, value: unknown, scope?: string | string[]): void
		DataLayer#setPageAttribute(path: string, value: unknown, scope?: string | string[]): void

Along with `getElement` these functions must be exposed via the root
data structure:

*Read*

		<root>.get(path, scope, defaultValue?)

*Write*

		<root>.user.set(path, value, scope?)
        <root>.user.profile.set(path, value, scope?)
        <root>.page.set(path, value, scope?)
        <root>.page.category.set(path, value, scope?)
        <root>.page.attribute.set(path, value, scope?)

## Integrations
In order to facilite integration with other 1st and 3rd party tools,
the datalayer must emit DOM events that others tools can listen for
and respond to accordingly. All mutations of the data layer should
fire a corresponding event, with additional context, where relevant.

### DOM Events
The Data Layer object should emit DOM `CustomEvents` on `document` to
ensure other 1st and 3rd party tooling (i.e. analytics integrations)
can interact with the data layer in an asynchronous and
fully-decoupled manner. With the exception of the global
`DataLayer:Initialized` event, data layer events should be
"namespaced" to the root data structure.

	| Event Name                | Fired When                                      |
	|---------------------------|-------------------------------------------------|
	| `DataLayer:Initialized`   | Data Layer is ready (`<root>.initialized=true`) |
	| `<root>:PageViewed`       | Page load tracked (`pageLoad()` called)         |
	| `<root>:PageElementSet`   | Page data set                                   |
	| `<root>:PageAttributeSet` | Page attribute set                              |
	| `<root>:PageCategorySet`  | Page category set                               |
	| `<root>:UserElementSet`   | User data set                                   |
	| `<root>:UserProfileSet`   | User profile set                                |
	| `<root>:EventTracked`     | Event tracked                                   |

All events bubble and may include a `detail` payload with additional context.

### DOM Bridge & Sample Integration
For most i.e. web analytics tools, the integration is as simple as
following the vendor instructions and adding a bridge to listen for
the DOM events and pass the data along accordingly. For example below
represents a basic integration with Mixpanel. Assuming the Mixpanel
library was loaded, this function would execute at the end of the
`<HEAD>` of the document, per their recommendations.

		function initMixpanelBridge() {
		  if (mixpanel && mixpanel.init && mixpanel.track) {
			// Initialize mixpanel — disable auto page view tracking
			// since we drive it explicitly via the data layer
			mixpanel.init("PROJECT_TOKEN", {
			  autocapture: true,
			  track_pageview: false,
			  record_sessions_percent: 100
			});

			function attachBridge(dataLayer) {
			  // Listen for PAGE_VIEWED and delegate to mixpanel's dedicated
			  // page view method, passing along any additional data elements
			  window.addEventListener(
				dataLayer.eventTypes.PAGE_VIEWED,
				(event) => {
				  if (window.mixpanel) {
					mixpanel.track_pageview(event.detail?.data);
				  }
				}
			  );

			  // Listen for EVENT_TRACKED and pass analytics-scoped
			  // events to mixpanel (precision tracking)
			  window.addEventListener(
				dataLayer.eventTypes.EVENT_TRACKED,
				(event) => {
				  if (
					event.scope.includes("analytics") &&
					  window.mixpanel
				  ) {
					mixpanel.track(event.detail?.name, event.detail?.data);
				  }
				}
			  );
			}

			// If the data layer is initialized, attach the bridge immediately;
			// otherwise wait for the DataLayer:Initialized event
			if (window.digitalData && window.digitalData.initialized) {
			  attachBridge(window.digitalData);
			} else {
			  window.addEventListener("DataLayer:Initialized", (event) => {
				if (event.detail?.rootElement) {
				  attachBridge(window[event.detail.rootElement]);
				}
			  });
			}
		  }
		}
