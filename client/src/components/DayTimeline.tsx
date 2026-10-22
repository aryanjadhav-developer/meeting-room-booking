import { useState, type MouseEvent } from "react";
import { alpha, Box, Paper, Tooltip, Typography } from "@mui/material";
import type { Dayjs } from "dayjs";
import type { Booking, BookingRules, Cabin } from "../api/types";
import { capacityText } from "../utils/format";
import { fmtRange, officeWindow, phaseOf } from "../utils/time";

interface Props {
  date: string;
  rules: BookingRules;
  cabins: Cabin[];
  bookings: Booking[];
  now: Dayjs;
  isWorkingDay?: boolean;
  onSlotClick(cabinId: number, start: string): void;
  onBookingClick(b: Booking): void;
}

const LABEL_WIDTH = 150;
const ROW_HEIGHT = 72;

/** Cabins as rows, office hours as columns. Click an empty slot to book it, click a block for details. */
export function DayTimeline({
  date,
  rules,
  cabins,
  bookings,
  now,
  isWorkingDay = true,
  onSlotClick,
  onBookingClick,
}: Props) {
  const [open, close] = officeWindow(date, rules);
  const totalMin = close.diff(open, "minute");
  const hours = Array.from({ length: Math.ceil(totalMin / 60) + 1 }, (_, i) =>
    open.add(i, "hour")
  ).filter((h) => !h.isAfter(close));
  const pct = (t: Dayjs | string) =>
    (Math.min(Math.max(open.diff(t, "minute", true) * -1, 0), totalMin) / totalMin) * 100;

  const [hover, setHover] = useState<{ cabinId: number; minute: number } | null>(null);

  const slotAt = (e: MouseEvent<HTMLDivElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const ratio = (e.clientX - rect.left) / rect.width;
    const minute = Math.floor((ratio * totalMin) / rules.slotMinutes) * rules.slotMinutes;
    return Math.min(Math.max(minute, 0), totalMin - rules.minDurationMinutes);
  };
  const isPast = (minute: number) => open.add(minute + rules.slotMinutes, "minute").isBefore(now);

  const nowInView = now.isAfter(open) && now.isBefore(close);

  return (
    <Paper variant="outlined" sx={{ overflowX: "auto" }}>
      <Box sx={{ minWidth: 900 }}>
        {/* hour header */}
        <Box sx={{ display: "flex", borderBottom: 1, borderColor: "divider", height: 32 }}>
          <Box sx={{ width: LABEL_WIDTH, flexShrink: 0 }} />
          <Box sx={{ position: "relative", flex: 1 }}>
            {hours.map((h) => (
              <Typography
                key={h.valueOf()}
                variant="caption"
                color="text.secondary"
                sx={{
                  position: "absolute",
                  top: 8,
                  left: `${pct(h)}%`,
                  transform: "translateX(-50%)",
                  whiteSpace: "nowrap",
                }}
              >
                {h.format("HH:mm")}
              </Typography>
            ))}
          </Box>
        </Box>

        {cabins.map((cabin) => {
          const rowBookings = bookings.filter(
            (b) => b.cabinId === cabin.id && b.status === "Confirmed"
          );
          return (
            <Box
              key={cabin.id}
              sx={{
                display: "flex",
                borderBottom: 1,
                borderColor: "divider",
                "&:last-of-type": { borderBottom: 0 },
              }}
            >
              <Box
                sx={{
                  width: LABEL_WIDTH,
                  flexShrink: 0,
                  px: 2,
                  py: 1.5,
                  borderRight: 1,
                  borderColor: "divider",
                  display: "flex",
                  alignItems: "center",
                  gap: 1.25,
                }}
              >
                <Box
                  sx={{
                    width: 10,
                    height: 10,
                    borderRadius: "50%",
                    bgcolor: cabin.color,
                    flexShrink: 0,
                  }}
                />
                <Box>
                  <Typography variant="subtitle2" sx={{ lineHeight: 1.2 }}>
                    {cabin.name}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {capacityText(cabin)}
                  </Typography>
                </Box>
              </Box>

              <Box
                sx={{
                  position: "relative",
                  flex: 1,
                  height: ROW_HEIGHT,
                  cursor: isWorkingDay ? "pointer" : "default",
                  backgroundImage: (theme) =>
                    `repeating-linear-gradient(to right, ${theme.palette.divider} 0 1px, transparent 1px ${100 / (totalMin / 60)}%)`,
                }}
                onMouseMove={(e) => setHover({ cabinId: cabin.id, minute: slotAt(e) })}
                onMouseLeave={() => setHover(null)}
                onClick={(e) => {
                  const minute = slotAt(e);
                  if (!isPast(minute))
                    onSlotClick(cabin.id, open.add(minute, "minute").format("HH:mm"));
                }}
              >
                {/* past shading */}
                {now.isAfter(open) && (
                  <Box
                    sx={{
                      position: "absolute",
                      inset: 0,
                      right: `${100 - pct(now)}%`,
                      bgcolor: "action.hover",
                      pointerEvents: "none",
                    }}
                  />
                )}

                {/* hover ghost slot */}
                {hover?.cabinId === cabin.id && !isPast(hover.minute) && (
                  <Box
                    sx={{
                      position: "absolute",
                      top: 6,
                      bottom: 6,
                      pointerEvents: "none",
                      borderRadius: 1,
                      left: `${(hover.minute / totalMin) * 100}%`,
                      width: `${(rules.slotMinutes / totalMin) * 100}%`,
                      border: "1.5px dashed",
                      borderColor: "primary.main",
                      bgcolor: (theme) => alpha(theme.palette.primary.main, 0.08),
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                    }}
                  >
                    <Typography variant="caption" color="primary">
                      + {open.add(hover.minute, "minute").format("HH:mm")}
                    </Typography>
                  </Box>
                )}

                {rowBookings.map((b) => {
                  const phase = phaseOf(b, now);
                  const left = pct(b.startTime);
                  const width = Math.max(pct(b.endTime) - left, 0.8);
                  return (
                    <Tooltip
                      key={b.id}
                      arrow
                      title={`${b.title} · ${b.bookedByName} · ${fmtRange(b)}`}
                    >
                      <Box
                        onClick={(e) => {
                          e.stopPropagation();
                          onBookingClick(b);
                        }}
                        onMouseMove={(e) => e.stopPropagation()}
                        sx={{
                          position: "absolute",
                          top: 6,
                          bottom: 6,
                          left: `${left}%`,
                          width: `${width}%`,
                          bgcolor: alpha(cabin.color, phase === "Completed" ? 0.25 : 0.85),
                          color: phase === "Completed" ? "text.secondary" : "#fff",
                          borderLeft: `4px solid ${cabin.color}`,
                          borderRadius: 1,
                          px: 1,
                          py: 0.5,
                          overflow: "hidden",
                          boxShadow:
                            phase === "InProgress"
                              ? `0 0 0 2px ${alpha(cabin.color, 0.4)}`
                              : "none",
                          "&:hover": { filter: "brightness(1.08)" },
                        }}
                      >
                        <Typography
                          variant="caption"
                          sx={{ fontWeight: 700, display: "block", lineHeight: 1.3 }}
                          noWrap
                        >
                          {b.title}
                        </Typography>
                        <Typography
                          variant="caption"
                          sx={{ display: "block", lineHeight: 1.3, opacity: 0.9 }}
                          noWrap
                        >
                          {b.bookedByName} · {fmtRange(b)}
                        </Typography>
                      </Box>
                    </Tooltip>
                  );
                })}

                {nowInView && (
                  <Box
                    sx={{
                      position: "absolute",
                      top: 0,
                      bottom: 0,
                      left: `${pct(now)}%`,
                      width: 2,
                      bgcolor: "error.main",
                      pointerEvents: "none",
                    }}
                  />
                )}
              </Box>
            </Box>
          );
        })}
      </Box>
    </Paper>
  );
}
