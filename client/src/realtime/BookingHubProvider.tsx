import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { tokenStore } from "../api/client";
import type { Booking } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { useNotify } from "../components/Notifier";
import { fmtTime } from "../utils/time";

export type HubStatus = "connecting" | "live" | "reconnecting" | "offline";

const HubStatusContext = createContext<HubStatus>("offline");
export const useHubStatus = () => useContext(HubStatusContext);

/**
 * Keeps a SignalR connection open while signed in. Every booking event refreshes the cached
 * queries, so the dashboard, timeline and "my bookings" update instantly on every screen.
 */
export function BookingHubProvider({ children }: { children: ReactNode }) {
  const { token, user } = useAuth();
  const userId = user?.id; // depend on the id only: the user object is refreshed periodically
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [status, setStatus] = useState<HubStatus>("offline");

  useEffect(() => {
    if (!token || !userId) return;

    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/bookings", { accessTokenFactory: () => tokenStore.get() ?? "" })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    const refresh = () => {
      queryClient.invalidateQueries({ queryKey: ["bookings"] });
      queryClient.invalidateQueries({ queryKey: ["cabin-status"] });
      queryClient.invalidateQueries({ queryKey: ["my-bookings"] });
    };

    const announce = (verb: string) => (b: Booking) => {
      refresh();
      if (b.bookedByUserId !== userId) {
        notify(
          `${b.bookedByName} ${verb} ${b.cabinName} · ${fmtTime(b.startTime)}–${fmtTime(b.endTime)}`,
          "info"
        );
      }
    };

    connection.on("BookingCreated", announce("booked"));
    connection.on("BookingUpdated", () => refresh());
    connection.on("BookingCancelled", announce("freed up"));
    connection.on("CabinsChanged", () => {
      queryClient.invalidateQueries({ queryKey: ["cabins"] });
      refresh();
    });

    connection.onreconnecting(() => setStatus("reconnecting"));
    connection.onreconnected(() => {
      setStatus("live");
      refresh();
    }); // catch up on anything missed
    connection.onclose(() => setStatus("offline"));

    let disposed = false;
    let retry: ReturnType<typeof setTimeout> | undefined;
    const start = async () => {
      setStatus("connecting");
      try {
        await connection.start();
        if (!disposed) {
          setStatus("live");
          refresh();
        }
      } catch {
        if (!disposed) {
          setStatus("offline");
          retry = setTimeout(start, 5000);
        }
      }
    };
    start();

    return () => {
      disposed = true;
      clearTimeout(retry);
      if (connection.state !== HubConnectionState.Disconnected) connection.stop();
    };
  }, [token, userId, queryClient, notify]);

  return <HubStatusContext.Provider value={status}>{children}</HubStatusContext.Provider>;
}
