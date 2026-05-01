import { describe, expect, it } from 'vitest'

import {
  IdProofingStatus,
  isIdProofedForAnalytics,
  isIdProofingStatusCompleted,
  needsIdProofingFlowAfterOtp
} from './idProofingStatus'

describe('idProofingStatus', () => {
  describe('isIdProofingStatusCompleted', () => {
    it('is true only for Completed (2)', () => {
      expect(isIdProofingStatusCompleted(IdProofingStatus.Completed)).toBe(true)
      expect(isIdProofingStatusCompleted(IdProofingStatus.NotStarted)).toBe(false)
      expect(isIdProofingStatusCompleted(IdProofingStatus.InProgress)).toBe(false)
      expect(isIdProofingStatusCompleted(IdProofingStatus.Failed)).toBe(false)
      expect(isIdProofingStatusCompleted(IdProofingStatus.Expired)).toBe(false)
      expect(isIdProofingStatusCompleted(null)).toBe(false)
      expect(isIdProofingStatusCompleted(undefined)).toBe(false)
    })
  })

  describe('needsIdProofingFlowAfterOtp', () => {
    it('is false only when status is Completed', () => {
      expect(needsIdProofingFlowAfterOtp(IdProofingStatus.Completed)).toBe(false)
      expect(needsIdProofingFlowAfterOtp(IdProofingStatus.NotStarted)).toBe(true)
      expect(needsIdProofingFlowAfterOtp(IdProofingStatus.InProgress)).toBe(true)
      expect(needsIdProofingFlowAfterOtp(IdProofingStatus.Failed)).toBe(true)
      expect(needsIdProofingFlowAfterOtp(IdProofingStatus.Expired)).toBe(true)
      expect(needsIdProofingFlowAfterOtp(null)).toBe(true)
      expect(needsIdProofingFlowAfterOtp(undefined)).toBe(true)
    })
  })

  describe('isIdProofedForAnalytics', () => {
    it('is true when status is Completed', () => {
      expect(isIdProofedForAnalytics(IdProofingStatus.Completed, null)).toBe(true)
    })

    it('is true when idProofingCompletedAt is set even if status is not Completed', () => {
      expect(isIdProofedForAnalytics(IdProofingStatus.NotStarted, 1_735_689_600)).toBe(true)
    })

    it('is false when neither completion signal is present', () => {
      expect(isIdProofedForAnalytics(IdProofingStatus.InProgress, null)).toBe(false)
      expect(isIdProofedForAnalytics(null, null)).toBe(false)
    })

    it('treats 0 timestamp as not proofed', () => {
      expect(isIdProofedForAnalytics(IdProofingStatus.NotStarted, 0)).toBe(false)
    })
  })
})
