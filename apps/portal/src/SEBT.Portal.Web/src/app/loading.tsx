export default function Loading() {
  return (
    <section
      className="usa-section"
      aria-busy="true"
      aria-label="Loading page"
    >
      <div className="grid-container">
        <div
          className="display-flex flex-column flex-align-center padding-4"
          role="status"
        >
          <div className="usa-spinner" />
          <p className="margin-top-2 text-base">Loading...</p>
        </div>
      </div>
    </section>
  )
}
