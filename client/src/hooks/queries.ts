import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api/client";
import { dayBounds, office, setOfficeTz } from "../utils/time";

/** Ticks every `ms` so time-based UI (occupied/free, "ends in 12 min") stays current. */
export function useNow(ms = 30_000) {
  const [now, setNow] = useState(() => office());
  useEffect(() => {
    const t = setInterval(() => setNow(office()), ms);
    return () => clearInterval(t);
  }, [ms]);
  return now;
}

export function useRules() {
  return useQuery({
    queryKey: ["rules"],
    queryFn: async () => {
      const rules = await api.rules();
      setOfficeTz(rules.timeZoneId);
      return rules;
    },
    staleTime: Infinity,
  });
}

export const useCabins = (includeInactive = false) =>
  useQuery({ queryKey: ["cabins", includeInactive], queryFn: () => api.cabins(includeInactive) });

export const useCabinStatus = () =>
  useQuery({ queryKey: ["cabin-status"], queryFn: api.cabinStatus, refetchInterval: 60_000 });

export function useBookingsForDay(date: string) {
  return useQuery({
    queryKey: ["bookings", date],
    queryFn: () => {
      const [from, to] = dayBounds(date);
      return api.bookings(from, to);
    },
  });
}

export const useMyBookings = (includePast: boolean) =>
  useQuery({ queryKey: ["my-bookings", includePast], queryFn: () => api.myBookings(includePast) });

/** Mutation that refreshes all booking views on success (the SignalR echo would too, this just makes it instant). */
export function useBookingMutation<TArgs, TResult>(fn: (args: TArgs) => Promise<TResult>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["bookings"] });
      qc.invalidateQueries({ queryKey: ["cabin-status"] });
      qc.invalidateQueries({ queryKey: ["my-bookings"] });
    },
  });
}
