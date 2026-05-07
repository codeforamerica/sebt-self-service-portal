# DC i18n Manual-Fix Checklist

Files affected: 38. Walk page by page; the table per file shows every key the engineer wired but the CSV does not yet have a value for in DC. The "Proposed copy" column is the engineer's fallback string — verify with design/PM, then add to the sheet.

## `src/SEBT.Portal.Web/src/app/(authenticated)/cards/info/page.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 76 | `confirmInfo:coLoadedInfoTitle` | en,es | Getting a replacement SNAP or TANF EBT card |

## `src/SEBT.Portal.Web/src/app/(authenticated)/cards/replace/address/page.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 20 | `common:loading` | en,es | Loading... |
| 30 | `confirmInfo:addressUpdateTitle` | en,es | Update your mailing address |

## `src/SEBT.Portal.Web/src/app/(authenticated)/cards/replace/confirm/page.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 19 | `common:loading` | en,es | Loading... |

## `src/SEBT.Portal.Web/src/app/(authenticated)/cards/replace/page.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 18 | `common:loading` | en,es | Loading... |

## `src/SEBT.Portal.Web/src/app/(authenticated)/cards/request/confirm/page.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 20 | `common:loading` | en,es | Loading... |

## `src/SEBT.Portal.Web/src/app/(authenticated)/cards/request/page.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 29 | `common:loading` | en,es | Loading... |
| 37 | `confirmInfo:cardSelectionPageTitle` | en,es | Which card would you like to replace? |

## `src/SEBT.Portal.Web/src/app/(authenticated)/error.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 50 | `common:errorId` | en,es | Error ID:  |
| 76 | `common:errorLogInAgain` | en,es | Log in again |
| 43 | `common:errorPageBody` | en,es | We encountered an error loading this page. Please try again or log in again if the problem persists. |
| 33 | `common:errorSessionExpired` | en,es | Session expired |
| 39 | `common:errorSessionExpiredBody` | en,es | Your session has expired. Please log in again to continue. |
| 34 | `common:errorSomethingWentWrong` | en,es | Something went wrong |
| 69 | `common:errorTryAgain` | en,es | Try again |

## `src/SEBT.Portal.Web/src/app/(authenticated)/profile/address/(flow)/replacement-cards/page.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 29 | `confirmInfo:replacementCardsTitle` | en,es | _[empty — needs design/PM input]_ |

## `src/SEBT.Portal.Web/src/app/(authenticated)/profile/address/(flow)/replacement-cards/select/confirm/page.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 22 | `common:loading` | en,es | Loading... |

## `src/SEBT.Portal.Web/src/app/(authenticated)/profile/address/(flow)/replacement-cards/select/page.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 13 | `confirmInfo:cardSelectionTitle` | en,es | Which cards need to be replaced? |

## `src/SEBT.Portal.Web/src/app/(authenticated)/profile/address/info/page.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 80 | `dashboard:coLoadedAddressUpdateAction2` | es | _[empty — needs design/PM input]_ |
| 91 | `dashboard:coLoadedAddressUpdateAction3` | es | _[empty — needs design/PM input]_ |
| 71 | `dashboard:coLoadedAddressUpdateBody1` | es | _[empty — needs design/PM input]_ |
| 73 | `dashboard:coLoadedAddressUpdateBody2` | es | _[empty — needs design/PM input]_ |
| 84 | `dashboard:coLoadedAddressUpdateBody3` | es | _[empty — needs design/PM input]_ |
| 69 | `dashboard:coLoadedAddressUpdateTitle` | es | _[empty — needs design/PM input]_ |

## `src/SEBT.Portal.Web/src/app/(public)/callback/page.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 115 | `login:callbackSigningIn` | en,es | _[empty — needs design/PM input]_ |
| 100 | `step-upProcessing:body` | es | Do not exit the page. Checking to see if we have enough information. |
| 99 | `step-upProcessing:title` | es | Please wait... |

## `src/SEBT.Portal.Web/src/app/(public)/login/id-proofing/off-boarding/page.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 88 | `offBoarding:action2` | en,es | _[empty — needs design/PM input]_ |
| 49 | `stepUpFailure:body` | es | _[empty — needs design/PM input]_ |
| 48 | `stepUpFailure:title` | es | _[empty — needs design/PM input]_ |

## `src/SEBT.Portal.Web/src/app/error.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 35 | `common:errorId` | en,es | Error ID:  |
| 25 | `common:errorSomethingWentWrong` | en,es | Something went wrong |
| 44 | `common:errorTryAgain` | en,es | Try again |
| 28 | `common:errorUnexpectedBody` | en,es | An unexpected error occurred. Please try again or contact support if the problem persists. |

## `src/SEBT.Portal.Web/src/app/not-found.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 18 | `common:pageNotFound` | en,es | Page not found |
| 21 | `common:pageNotFoundBody` | en,es | The page you are looking for does not exist or has been moved. |
| 30 | `common:returnToHome` | en,es | Return to home |

## `src/SEBT.Portal.Web/src/components/BetaBanner.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 20 | `common:alertBeta` | en,es | _[empty — needs design/PM input]_ |

## `src/SEBT.Portal.Web/src/features/address/components/AddressAutocomplete/AddressAutocomplete.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 212 | `confirmInfo:autocompleteMultiUnit` | en,es | ({{count}} more entries) |
| 240 | `confirmInfo:autocompleteSuggestionsAvailable` | en,es | {{count}} suggestion available |

## `src/SEBT.Portal.Web/src/features/address/components/AddressForm/AddressForm.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 183 | `confirmInfo:addressUpdateError` | en,es | Something went wrong. Please try again. |
| 239 | `confirmInfo:formErrorSummary` | en,es | Please correct the errors below. |
| 248 | `confirmInfo:hintStreetAddressDc` | en,es | Include direction. NW, NE, SE, or SW. |
| 129 | `confirmInfo:postalCodeInvalid` | en,es | Enter a valid 5- or 9-digit ZIP code. |
| 166 | `confirmInfo:streetAddressInlineError` | en,es | Enter a street address shorter than 30 characters |

## `src/SEBT.Portal.Web/src/features/address/components/AddressNotFound/AddressNotFound.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 46 | `confirmInfo:blockedBody` | en,es | _[empty — needs design/PM input]_ |
| 41 | `confirmInfo:blockedTitle` | en,es | This address can't be used |

## `src/SEBT.Portal.Web/src/features/address/components/ReplacementCardPrompt/ReplacementCardPrompt.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 70 | `confirmInfo:replacementCardsCriteriaLost` | en,es | Or you no longer have them |
| 65 | `confirmInfo:replacementCardsCriteriaNotReceived` | en,es | You haven't received them in the mail after two weeks |
| 35 | `confirmInfo:selectOneError` | en,es | Please select an option. |
| 79 | `confirmInfo:snapTanfCallout` | en,es | If your child is eligible for DC SUN Bucks through SNAP or TANF participation, they will not rece… |

## `src/SEBT.Portal.Web/src/features/address/components/SuggestedAddress/SuggestedAddress.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 212 | `common:loading` | en,es | Loading... |
| 83 | `confirmInfo:addressUpdateError` | en,es | Something went wrong. Please try again. |

## `src/SEBT.Portal.Web/src/features/auth/components/AuthGuard/AuthGuard.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 44 | `step-upProcessing:body` | es | Do not exit the page. Checking to see if we have enough information. |
| 43 | `step-upProcessing:title` | es | Please wait... |

## `src/SEBT.Portal.Web/src/features/auth/components/IalGuard/IalGuard.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 139 | `common:ialGuardChallengeTitle` | en,es | To keep your account safe, we need to confirm it’s really you |
| 89 | `common:ialGuardCheckingTitle` | en,es | Please wait… |
| 90 | `step-upProcessing:body` | es | _[empty — needs design/PM input]_ |
| 160 | `stepUpDisclaimer:action` | es | _[empty — needs design/PM input]_ |
| 144 | `stepUpDisclaimer:body` | es | _[empty — needs design/PM input]_ |

## `src/SEBT.Portal.Web/src/features/auth/components/doc-verify/DocVerifyInterstitial.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 50 | `idProofing:interstitialActionEnterId` | en,es | Enter an ID number |
| 80 | `idProofing:interstitialContactUsLink` | en,es | Need help? Contact us. |
| 36 | `idProofing:interstitialIdTypeDriversLicense` | en,es | driver's license |
| 37 | `idProofing:interstitialIdTypeForeignPassport` | en,es | foreign passport |
| 38 | `idProofing:interstitialIdTypeOtherPhotoId` | en,es | or another photo ID |
| 58 | `idProofing:interstitialLoading` | en,es | Loading... |

## `src/SEBT.Portal.Web/src/features/auth/components/doc-verify/DocVerifyPage.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 232 | `idProofing:docVerifyResubmitError` | en,es | We couldn't start a retry. Please try again in a moment. |
| 131 | `idProofing:docVerifyStartError` | en,es | Something went wrong starting document verification. Please try again. |

## `src/SEBT.Portal.Web/src/features/auth/components/doc-verify/DocVerifyResubmit.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 59 | `idProofing:resubmitActionTryAgain` | en,es | Try again |
| 44 | `idProofing:resubmitBody` | en,es | _[empty — needs design/PM input]_ |
| 40 | `idProofing:resubmitHeading` | en,es | Let's try that again |
| 56 | `idProofing:resubmitLoading` | en,es | Starting retry... |

## `src/SEBT.Portal.Web/src/features/auth/components/doc-verify/VerificationPending.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 104 | `idProofing:verificationPendingActionCheckStatus` | en,es | Check status |
| 69 | `idProofing:verificationPendingAriaLabel` | en,es | Verification status |
| 76 | `idProofing:verificationPendingBody` | en,es | This may take a moment. Please don't close this page. |
| 73 | `idProofing:verificationPendingHeading` | en,es | Verifying your document... |
| 85 | `idProofing:verificationPendingStatusLabel` | en,es | Checking verification status |
| 95 | `idProofing:verificationPendingStillCheckingBody` | en,es | Verification is taking longer than expected. You can check the status or try again later. |
| 92 | `idProofing:verificationPendingStillCheckingHeading` | en,es | We're still checking your document |

## `src/SEBT.Portal.Web/src/features/auth/components/id-proofing/IdProofingForm.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 279 | `idProofing:idProofingGenericError` | en,es | Something went wrong. Please try again. |
| 235 | `idProofing:idProofingStartError` | en,es | Unable to start document verification. Please try again. |

## `src/SEBT.Portal.Web/src/features/auth/components/off-boarding/OffBoardingPage.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 112 | `idProofing:offboardingActionApply` | en,es | Apply now |
| 94 | `idProofing:offboardingActionContact` | en,es | Contact us |
| 133 | `idProofing:offboardingContactUsLink` | en,es | Need help? Contact us. |
| 67 | `idProofing:offboardingHeading` | en,es | We're sorry, we aren't able to show your DC SUN Bucks information |

## `src/SEBT.Portal.Web/src/features/cards/components/CardSelection/CardSelection.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 64 | `common:loading` | en,es | Loading... |
| 82 | `confirmInfo:cardSelectionAllInCooldown` | en,es | All cards were recently replaced. Please try again later. |
| 70 | `confirmInfo:cardSelectionLoadError` | en,es | Unable to load household members. Please try again later. |
| 86 | `confirmInfo:cardSelectionNoChildren` | en,es | No children found in your household. |
| 108 | `confirmInfo:cardSelectionRequired` | en,es | Please select at least one card. |

## `src/SEBT.Portal.Web/src/features/cards/components/ConfirmAddress/ConfirmAddress.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 55 | `confirmInfo:selectOneError` | en,es | Please select an option. |

## `src/SEBT.Portal.Web/src/features/cards/components/ConfirmRequest/ConfirmRequest.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 145 | `common:loading` | en,es | Loading... |
| 43 | `confirmInfo:cardReplacementError` | en,es | There was an issue requesting your replacement card. Please try again later. |

## `src/SEBT.Portal.Web/src/features/household/components/ActionButtons/ActionButtons.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 68 | `dashboard:actionNavigationNavLabel` | en,es | Quick actions |
| 78 | `dashboard:actionNavigationSelfServiceUnavailable` | en,es | _[empty — needs design/PM input]_ |

## `src/SEBT.Portal.Web/src/features/household/components/CardStatusTimeline/CardStatusTimeline.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 112 | `dashboard:cardTableStatusMessageActive` | en | _[empty — needs design/PM input]_ |
| 117 | `dashboard:cardTableStatusMessageDeactivated` | en | _[empty — needs design/PM input]_ |
| 103 | `dashboard:cardTableStatusMessageMailed` | en | _[empty — needs design/PM input]_ |
| 97 | `dashboard:cardTableStatusMessageRequested1` | en | _[empty — needs design/PM input]_ |

## `src/SEBT.Portal.Web/src/features/household/components/DashboardAlerts/DashboardAlerts.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 107 | `dashboard:alertAddressUpdateFailedBody` | en,es | Please try again later or contact the Summer EBT Help Desk for assistance. |
| 102 | `dashboard:alertAddressUpdateFailedHeading` | en,es | There was an issue updating your mailing address. |
| 62 | `dashboard:alertAddressUpdatedBody` | en,es | Your address update has been recorded. |
| 60 | `dashboard:alertAddressUpdatedHeading` | en,es | Address update recorded |
| 131 | `dashboard:alertAddressVerificationBody` | en,es | Please verify your mailing address is up to date so you can receive your Summer EBT cards. |
| 88 | `dashboard:alertCardReplacedBody` | en,es | New cards usually arrive in your mailbox within 7-10 business days. |
| 84 | `dashboard:alertCardReplacedBodyWithAddress` | en,es | New cards usually arrive in your mailbox within 7-10 business days. Check back here in 1-2 busine… |
| 81 | `dashboard:alertCardReplacedHeading` | en,es | Your replacement card request has been recorded |
| 71 | `dashboard:alertCardsRequestedBody` | en,es | Your address update and card replacement request have been recorded. |
| 69 | `dashboard:alertCardsRequestedHeading` | en,es | Address update and card replacement recorded |
| 122 | `dashboard:alertContactUpdateFailedBody` | en,es | Please try again later. |
| 117 | `dashboard:alertContactUpdateFailedHeading` | en,es | There was an issue updating your contact preferences. |

## `src/SEBT.Portal.Web/src/features/household/components/DashboardContent/DashboardContent.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 103 | `dashboard:errorDescription` | en,es | There was an error loading your dashboard. Please try again later. |
| 101 | `dashboard:errorHeading` | en,es | Error loading dashboard |
| 67 | `dashboard:pageTitle` | en,es | SUN Bucks Dashboard |
| 76 | `step-upProcessing:body` | es | Do not exit the page. Checking to see if we have enough information. |
| 75 | `step-upProcessing:title` | es | Please wait... |

## `src/SEBT.Portal.Web/src/features/household/components/EbtEdgeSection/EbtEdgeSection.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 29 | `dashboard:alertEbtEdgeSectionHeading` | en,es | EBT Card Help |

## `src/SEBT.Portal.Web/src/features/household/components/HouseholdSummary/HouseholdSummary.tsx`

| Line | Key | DC locales | Proposed copy / what user sees |
|---:|---|---|---|
| 148 | `dashboard:profileTableCo-loadedAddress` | es | _[empty — needs design/PM input]_ |

