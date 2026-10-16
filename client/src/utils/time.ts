import dayjs, { type Dayjs } from "dayjs";
import utc from "dayjs/plugin/utc";
import timezone from "dayjs/plugin/timezone";
import type { Booking, BookingPhase, BookingRules } from "../api/types";

dayjs.extend(utc);
dayjs.extend(timezone);

/** All times are shown in the office time zone, whatever the viewer's device is set to. */
let officeTz = "Asia/Kolkata";
export const setOfficeTz = (tz: string) => {
  officeTz = tz;
};

export const office = (value?: string | number | Date | Dayjs) => dayjs(value).tz(officeTz);

/** A local office date ("2026-10-14") and time ("09:00" or "09:00:00") as an instant. */
export const officeAt = (date: string, time: string) =>
  dayjs.tz(`${date} ${time.slice(0, 5)}`, officeTz);

export const todayStr = () => office().format("YYYY-MM-DD");

export const fmtTime = (iso: string | Dayjs) => office(iso).format("HH:mm");
export const fmtDate = (iso: string | Dayjs) => office(iso).format("ddd, D MMM");
export const fmtRange = (b: Pick<Booking, "startTime" | "endTime">) =>
  `${fmtTime(b.startTime)}–${fmtTime(b.endTime)}`;

export function fmtDuration(minutes: number) {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  if (!h) return `${m} min`;
  return m ? `${h}h ${m}m` : `${h}h`;
}

/** UTC ISO bounds of a whole local office day. */
export function dayBounds(date: string): [string, string] {
  const start = officeAt(date, "00:00");
  return [start.toISOString(), start.add(1, "day").toISOString()];
}

export function officeWindow(date: string, rules: BookingRules): [Dayjs, Dayjs] {
  return [officeAt(date, rules.officeStart), officeAt(date, rules.officeEnd)];
}

/** Phase recomputed on the client so status flips (upcoming → in progress → done) without a reload. */
export function phaseOf(b: Booking, now: Dayjs): BookingPhase {
  if (b.status === "Cancelled") return "Cancelled";
  if (b.status === "NoShow") return "NoShow";
  if (now.isBefore(b.startTime)) return "Upcoming";
  if (now.isBefore(b.endTime)) return "InProgress";
  return "Completed";
}

export const overlaps = (aStart: Dayjs, aEnd: Dayjs, b: Pick<Booking, "startTime" | "endTime">) =>
  aStart.isBefore(b.endTime) && aEnd.isAfter(b.startTime);

export function minutesUntil(iso: string, now: Dayjs) {
  return Math.max(0, Math.ceil(dayjs(iso).diff(now, "minute", true)));
}
