import { Chip } from "@mui/material";
import type { BookingPhase } from "../api/types";

const LOOK: Record<
  BookingPhase,
  { label: string; color: "default" | "primary" | "success" | "warning" | "error" }
> = {
  Upcoming: { label: "Upcoming", color: "primary" },
  InProgress: { label: "In progress", color: "success" },
  Completed: { label: "Completed", color: "default" },
  Cancelled: { label: "Cancelled", color: "error" },
  NoShow: { label: "No-show", color: "warning" },
};

export function PhaseChip({ phase }: { phase: BookingPhase }) {
  const look = LOOK[phase];
  return (
    <Chip
      size="small"
      label={look.label}
      color={look.color}
      variant={phase === "Completed" ? "outlined" : "filled"}
    />
  );
}
