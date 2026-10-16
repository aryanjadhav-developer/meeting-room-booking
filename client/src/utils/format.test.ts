import { describe, it, expect } from 'vitest';
import { capacityText, purposeText } from './format';
import type { Booking, Cabin } from '../api/types';

describe('format utilities', () => {
  describe('capacityText', () => {
    it('uses capacityLabel when provided', () => {
      const cabin: Pick<Cabin, 'capacity' | 'capacityLabel'> = {
        capacity: 10,
        capacityLabel: '8–10',
      };
      expect(capacityText(cabin)).toBe('8–10 people');
    });

    it('falls back to numeric capacity when capacityLabel is empty or undefined', () => {
      const cabin: Pick<Cabin, 'capacity' | 'capacityLabel'> = {
        capacity: 6,
        capacityLabel: null,
      };
      expect(capacityText(cabin)).toBe('6 people');
    });
  });

  describe('purposeText', () => {
    it('returns purpose directly for standard purposes', () => {
      const booking: Pick<Booking, 'purpose' | 'purposeDetail'> = {
        purpose: 'Meeting',
        purposeDetail: null,
      };
      expect(purposeText(booking)).toBe('Meeting');
    });

    it('formats "Other" with purposeDetail when detail is present', () => {
      const booking: Pick<Booking, 'purpose' | 'purposeDetail'> = {
        purpose: 'Other',
        purposeDetail: 'Vendor demo',
      };
      expect(purposeText(booking)).toBe('Other – Vendor demo');
    });

    it('returns "Other" when purpose is Other but detail is absent', () => {
      const booking: Pick<Booking, 'purpose' | 'purposeDetail'> = {
        purpose: 'Other',
        purposeDetail: null,
      };
      expect(purposeText(booking)).toBe('Other');
    });
  });
});
