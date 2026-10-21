export type UserRole = "Employee" | "Admin";
export type BookingPurpose = "Meeting" | "Discussion" | "Interview" | "Call" | "Other";
export type BookingStatus = "Confirmed" | "Cancelled" | "NoShow";
export type BookingPhase = "Upcoming" | "InProgress" | "Completed" | "Cancelled" | "NoShow";

export const PURPOSES: BookingPurpose[] = ["Meeting", "Discussion", "Interview", "Call", "Other"];

export interface User {
  id: string;
  name: string;
  email: string;
  role: UserRole;
  isActive: boolean;
}

export interface DirectoryUser {
  id: string;
  name: string;
  email: string;
}

export interface Project {
  id: number;
  name: string;
  memberUserIds: string[];
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: User;
}

export interface Cabin {
  id: number;
  name: string;
  description: string | null;
  capacity: number;
  amenities: string[];
  color: string;
  isActive: boolean;
  capacityLabel: string | null;
}

export interface UpsertCabin {
  name: string;
  description: string | null;
  capacity: number;
  amenities: string[];
  color: string;
  isActive: boolean;
  capacityLabel: string | null;
}

export interface Booking {
  id: string;
  cabinId: number;
  cabinName: string;
  bookedByUserId: string;
  bookedByName: string;
  bookedByEmail: string;
  title: string;
  purpose: BookingPurpose;
  purposeDetail: string | null;
  notes: string | null;
  startTime: string;
  endTime: string;
  durationMinutes: number;
  status: BookingStatus;
  phase: BookingPhase;
  checkedInAt: string | null;
  createdAt: string;
}

export interface CabinStatus {
  cabin: Cabin;
  isOccupied: boolean;
  current: Booking | null;
  next: Booking | null;
}

export interface BookingInput {
  cabinId: number;
  title: string;
  purpose: BookingPurpose;
  purposeDetail: string | null;
  notes: string | null;
  startTime: string;
  endTime: string;
  attendeeEmails?: string[];
}

export interface BookingRules {
  timeZoneId: string;
  officeStart: string; // "09:00:00"
  officeEnd: string;
  workingDays: string[];
  slotMinutes: number;
  minDurationMinutes: number;
  maxDurationMinutes: number;
  maxDaysAhead: number;
  checkInOpensMinutesBefore: number;
  autoReleaseEnabled: boolean;
  checkInGraceMinutes: number;
  projects: string[];
}

export interface CabinUtilization {
  cabinId: number;
  cabinName: string;
  bookingCount: number;
  bookedHours: number;
  availableHours: number;
  utilizationPercent: number;
  noShowCount: number;
  cancelledCount: number;
}

export interface UtilizationReport {
  from: string;
  to: string;
  cabins: CabinUtilization[];
  hourlyLoad: { hour: number; bookingCount: number }[];
  topUsers: {
    userId: string;
    name: string;
    email: string;
    bookingCount: number;
    bookedHours: number;
  }[];
}

// 0=Sun 1=Mon 2=Tue 3=Wed 4=Thu 5=Fri 6=Sat
export const DAY_LABELS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"] as const;

export interface RecurringBooking {
  id: string;
  cabinId: number;
  cabinName: string;
  title: string;
  purpose: BookingPurpose;
  purposeDetail: string | null;
  notes: string | null;
  startDate: string;   // YYYY-MM-DD
  endDate: string;     // YYYY-MM-DD
  startMinuteOfDay: number;  // minutes since midnight in office TZ
  durationMinutes: number;
  daysOfWeek: number[];      // DayOfWeek ints
  attendeeEmails: string[];
  isActive: boolean;
  createdAt: string;
}

export interface CreateRecurringBookingRequest {
  cabinId: number;
  title: string;
  purpose: BookingPurpose;
  purposeDetail: string | null;
  notes: string | null;
  startDate: string;
  endDate: string;
  startMinuteOfDay: number;
  durationMinutes: number;
  daysOfWeek: number[];
  attendeeEmails: string[];
}

export interface UpdateRecurringBookingRequest {
  cabinId: number;
  title: string;
  purpose: BookingPurpose;
  purposeDetail: string | null;
  notes: string | null;
  endDate: string;
  startMinuteOfDay: number;
  durationMinutes: number;
  daysOfWeek: number[];
  attendeeEmails: string[];
  isActive: boolean;
}

export interface AuditEntry {
  id: string;
  userId: string | null;
  userName: string;
  action: string;
  entityType: string;
  entityId: string;
  details: string;
  timestamp: string;
}

export interface UpdateSettingsRequest {
  officeStart: string;
  officeEnd: string;
  workingDays: string[];
  slotMinutes: number;
  minDurationMinutes: number;
  maxDurationMinutes: number;
  maxDaysAhead: number;
  checkInOpensMinutesBefore: number;
  autoReleaseEnabled: boolean;
  checkInGraceMinutes: number;
  projects: string[];
}
