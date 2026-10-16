import { describe, it, expect, beforeEach } from 'vitest';
import dayjs from 'dayjs';
import {
  setOfficeTz,
  office,
  officeAt,
  fmtDuration,
  phaseOf,
  overlaps,
  minutesUntil,
} from './time';
import type { Booking } from '../api/types';

describe('time utilities', () => {
  beforeEach(() => {
    setOfficeTz('Asia/Kolkata');
  });

  describe('fmtDuration', () => {
    it('formats minutes under an hour', () => {
      expect(fmtDuration(15)).toBe('15 min');
      expect(fmtDuration(45)).toBe('45 min');
    });

    it('formats exact hours', () => {
      expect(fmtDuration(60)).toBe('1h');
      expect(fmtDuration(120)).toBe('2h');
    });

    it('formats compound hours and minutes', () => {
      expect(fmtDuration(75)).toBe('1h 15m');
      expect(fmtDuration(150)).toBe('2h 30m');
    });
  });

  describe('office & officeAt', () => {
    it('parses a date and time in the configured office time zone', () => {
      const parsed = officeAt('2026-10-16', '10:30');
      expect(parsed.format('HH:mm')).toBe('10:30');
      expect(parsed.format('YYYY-MM-DD')).toBe('2026-10-16');

      const now = office('2026-10-16T05:00:00Z');
      expect(now.isValid()).toBe(true);
    });
  });

  describe('phaseOf', () => {
    const baseBooking: Booking = {
      id: 'b-1',
      cabinId: 1,
      cabinName: 'Cabin 1',
      bookedByUserId: 'u-1',
      bookedByName: 'Alice',
      bookedByEmail: 'alice@example.com',
      title: 'Sprint Planning',
      purpose: 'Meeting',
      purposeDetail: null,
      notes: null,
      startTime: '2026-10-16T10:00:00Z',
      endTime: '2026-10-16T11:00:00Z',
      durationMinutes: 60,
      status: 'Confirmed',
      phase: 'Upcoming',
      checkedInAt: null,
      createdAt: '2026-10-16T08:00:00Z',
    };

    it('returns Cancelled if status is Cancelled', () => {
      const b = { ...baseBooking, status: 'Cancelled' as const };
      expect(phaseOf(b, dayjs('2026-10-16T09:00:00Z'))).toBe('Cancelled');
    });

    it('returns NoShow if status is NoShow', () => {
      const b = { ...baseBooking, status: 'NoShow' as const };
      expect(phaseOf(b, dayjs('2026-10-16T10:30:00Z'))).toBe('NoShow');
    });

    it('returns Upcoming when clock is before start time', () => {
      expect(phaseOf(baseBooking, dayjs('2026-10-16T09:30:00Z'))).toBe('Upcoming');
    });

    it('returns InProgress when clock is between start and end time', () => {
      expect(phaseOf(baseBooking, dayjs('2026-10-16T10:30:00Z'))).toBe('InProgress');
    });

    it('returns Completed when clock is at or after end time', () => {
      expect(phaseOf(baseBooking, dayjs('2026-10-16T11:00:00Z'))).toBe('Completed');
      expect(phaseOf(baseBooking, dayjs('2026-10-16T12:00:00Z'))).toBe('Completed');
    });
  });

  describe('overlaps', () => {
    const b = {
      startTime: '2026-10-16T10:00:00Z',
      endTime: '2026-10-16T11:00:00Z',
    };

    it('detects direct overlap', () => {
      const start = dayjs('2026-10-16T10:30:00Z');
      const end = dayjs('2026-10-16T11:30:00Z');
      expect(overlaps(start, end, b)).toBe(true);
    });

    it('returns false for adjacent slots (no overlap on exact boundary)', () => {
      const beforeStart = dayjs('2026-10-16T09:00:00Z');
      const beforeEnd = dayjs('2026-10-16T10:00:00Z');
      expect(overlaps(beforeStart, beforeEnd, b)).toBe(false);

      const afterStart = dayjs('2026-10-16T11:00:00Z');
      const afterEnd = dayjs('2026-10-16T12:00:00Z');
      expect(overlaps(afterStart, afterEnd, b)).toBe(false);
    });

    it('detects enclosing interval', () => {
      const start = dayjs('2026-10-16T09:00:00Z');
      const end = dayjs('2026-10-16T12:00:00Z');
      expect(overlaps(start, end, b)).toBe(true);
    });
  });

  describe('minutesUntil', () => {
    it('calculates remaining minutes correctly', () => {
      const target = '2026-10-16T10:30:00Z';
      const now = dayjs('2026-10-16T10:00:00Z');
      expect(minutesUntil(target, now)).toBe(30);
    });

    it('clamps to zero when time has already passed', () => {
      const target = '2026-10-16T09:00:00Z';
      const now = dayjs('2026-10-16T10:00:00Z');
      expect(minutesUntil(target, now)).toBe(0);
    });
  });
});
