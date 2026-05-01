export interface LoadingInterstitialProps {
  title: string;
  message: string;
}

export function LoadingInterstitial({ title, message }: LoadingInterstitialProps) {
  return (
    <div
      className="padding-y-4"
      role="status"
      aria-live="polite"
      aria-busy="true"
    >
      <h1 className="font-sans-xl text-primary text-center margin-bottom-4">
        {title}
      </h1>
      <div className="border-1px border-primary-light bg-primary-lightest radius-md padding-3 margin-bottom-2">
        <p className="margin-0">{message}</p>
      </div>
    </div>
  );
}
