/**
 * Hammer-and-wrench maintenance icon styled after common outage page patterns.
 */
export function MaintenanceIcon() {
  return (
    <svg
      className="display-block margin-x-auto margin-bottom-4"
      width="96"
      height="96"
      viewBox="0 0 96 96"
      aria-hidden="true"
      focusable="false"
    >
      <g transform="translate(48 48)">
        <g transform="rotate(-35)">
          <rect
            x="-6"
            y="-34"
            width="12"
            height="34"
            rx="2"
            fill="#5c5c5c"
          />
          <rect
            x="-14"
            y="-34"
            width="28"
            height="10"
            rx="2"
            fill="#5c5c5c"
          />
          <rect
            x="-6"
            y="4"
            width="12"
            height="24"
            rx="2"
            fill="#face00"
          />
        </g>
        <g transform="rotate(35)">
          <path
            d="M-5 28 L-5 -8 C-5 -18 5 -24 14 -18 L22 -10 C28 -4 22 6 14 6 L-5 6 Z"
            fill="#9ca3af"
          />
          <circle
            cx="14"
            cy="-18"
            r="5"
            fill="#9ca3af"
          />
        </g>
      </g>
    </svg>
  )
}
