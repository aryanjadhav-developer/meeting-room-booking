import type { Booking, Cabin } from "../api/types";

/** "9+ people", "3-4 people", or "6 people" when no label is set. */
export const capacityText = (c: Pick<Cabin, "capacity" | "capacityLabel">) =>
  `${c.capacityLabel || c.capacity} people`;

/** "Discussion", or "Other – Vendor demo" when a custom purpose was given. */
export const purposeText = (b: Pick<Booking, "purpose" | "purposeDetail">) =>
  b.purpose === "Other" && b.purposeDetail ? `Other – ${b.purposeDetail}` : b.purpose;
