'use client'

// Skeleton loading state for the dashboard
// Provides visual structure while data is loading

function SkeletonBox({
  width = '100%',
  height = '1rem',
  className = ''
}: {
  width?: string
  height?: string
  className?: string
}) {
  return (
    <div
      className={`bg-base-lighter radius-sm ${className}`}
      style={{ width, height, animation: 'pulse 1.5s ease-in-out infinite' }}
      aria-hidden="true"
    />
  )
}

function UserProfileCardSkeleton() {
  return (
    <div className="usa-card__container margin-bottom-3">
      <div className="usa-card__body">
        <div className="display-flex flex-align-center flex-gap-2">
          <SkeletonBox
            width="80px"
            height="24px"
          />
          <SkeletonBox
            width="200px"
            height="32px"
          />
        </div>
      </div>
    </div>
  )
}

function ActionButtonsSkeleton() {
  return (
    <div className="margin-bottom-4">
      <ul className="usa-button-group">
        {[1, 2, 3, 4].map((i) => (
          <li
            key={i}
            className="usa-button-group__item"
          >
            <SkeletonBox
              width="180px"
              height="44px"
              className="radius-md"
            />
          </li>
        ))}
      </ul>
    </div>
  )
}

function HouseholdSummarySkeleton() {
  return (
    <section className="margin-bottom-4">
      <SkeletonBox
        width="200px"
        height="28px"
        className="margin-bottom-2"
      />
      <div className="usa-card__container">
        <div className="usa-card__body">
          <dl className="margin-0">
            {[1, 2, 3].map((i) => (
              <div
                key={i}
                className="margin-bottom-2"
              >
                <SkeletonBox
                  width="100px"
                  height="16px"
                  className="margin-bottom-1"
                />
                <SkeletonBox
                  width="180px"
                  height="20px"
                />
              </div>
            ))}
          </dl>
        </div>
      </div>
    </section>
  )
}

function EnrolledChildrenSkeleton() {
  return (
    <section className="margin-bottom-4">
      <SkeletonBox
        width="180px"
        height="28px"
        className="margin-bottom-1"
      />
      <SkeletonBox
        width="300px"
        height="20px"
        className="margin-bottom-3"
      />
      <div className="usa-accordion usa-accordion--bordered">
        {[1, 2].map((i) => (
          <div
            key={i}
            className="usa-accordion__container"
          >
            <div className="usa-accordion__heading">
              <SkeletonBox
                width="160px"
                height="44px"
              />
            </div>
          </div>
        ))}
      </div>
    </section>
  )
}

function HelpSectionSkeleton() {
  return (
    <section className="margin-bottom-4">
      <div className="usa-accordion">
        <div className="usa-accordion__container">
          <div className="usa-accordion__heading">
            <SkeletonBox
              width="200px"
              height="44px"
            />
          </div>
        </div>
      </div>
    </section>
  )
}

function ApplicationsSectionSkeleton() {
  return (
    <section className="margin-top-4">
      <SkeletonBox
        width="160px"
        height="28px"
        className="margin-bottom-1"
      />
      <SkeletonBox
        width="280px"
        height="20px"
        className="margin-bottom-3"
      />
      <div className="usa-card__container">
        <div className="usa-card__body">
          <dl className="margin-0">
            {[1, 2, 3, 4].map((i) => (
              <div
                key={i}
                className="margin-bottom-2"
              >
                <SkeletonBox
                  width="120px"
                  height="16px"
                  className="margin-bottom-1"
                />
                <SkeletonBox
                  width="160px"
                  height="20px"
                />
              </div>
            ))}
          </dl>
        </div>
      </div>
    </section>
  )
}

export function DashboardSkeleton() {
  return (
    <div
      role="status"
      aria-label="Loading dashboard"
    >
      <span className="usa-sr-only">Loading dashboard</span>
      <style>
        {`
          @keyframes pulse {
            0%, 100% { opacity: 1; }
            50% { opacity: 0.5; }
          }
        `}
      </style>
      <UserProfileCardSkeleton />
      <ActionButtonsSkeleton />
      <HouseholdSummarySkeleton />
      <EnrolledChildrenSkeleton />
      <HelpSectionSkeleton />
      <ApplicationsSectionSkeleton />
    </div>
  )
}
