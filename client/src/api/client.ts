import type {
  AuthResponse,
  Booking,
  BookingInput,
  BookingRules,
  Cabin,
  CabinStatus,
  UpsertCabin,
  User,
  UserRole,
  UtilizationReport,
} from "./types";

const TOKEN_KEY = "cabin-booking.token";

export const tokenStore = {
  get(): string | null {
    try {
      return localStorage.getItem(TOKEN_KEY);
    } catch {
      return null;
    }
  },
  set(token: string | null) {
    try {
      if (token) localStorage.setItem(TOKEN_KEY, token);
      else localStorage.removeItem(TOKEN_KEY);
    } catch {
      /* storage unavailable: session-only login */
    }
  },
};

/** Error carrying the server's ProblemDetails message. */
export class ApiError extends Error {
  constructor(
    public status: number,
    message: string
  ) {
    super(message);
  }
}

let onUnauthorized: ((reason: string) => void) | null = null;
/** Called when the server rejects the session (expired token, account deactivated, ...), with the reason to show. */
export const setUnauthorizedHandler = (fn: (reason: string) => void) => {
  onUnauthorized = fn;
};

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const token = tokenStore.get();
  const res = await fetch(url, {
    method,
    headers: {
      ...(body !== undefined ? { "Content-Type": "application/json" } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  // Login/register failures are form errors, not a lost session.
  if (res.status === 401 && url !== "/api/auth/login" && url !== "/api/auth/register") {
    let reason = "Your session has expired. Please sign in again.";
    try {
      reason = (await res.json()).detail || reason;
    } catch {
      /* empty body */
    }
    onUnauthorized?.(reason);
    throw new ApiError(401, reason);
  }
  if (!res.ok) {
    let message = `Request failed (${res.status})`;
    try {
      const problem = await res.json();
      message = problem.detail || problem.title || message;
    } catch {
      /* not JSON */
    }
    throw new ApiError(res.status, message);
  }
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

const qs = (params: Record<string, string | number | boolean | undefined | null>) =>
  new URLSearchParams(
    Object.entries(params)
      .filter(([, v]) => v !== undefined && v !== null)
      .map(([k, v]) => [k, String(v)])
  ).toString();

export const api = {
  // auth
  login: (email: string, password: string) =>
    request<AuthResponse>("POST", "/api/auth/login", { email, password }),
  register: (name: string, email: string, password: string) =>
    request<AuthResponse>("POST", "/api/auth/register", { name, email, password }),
  me: () => request<User>("GET", "/api/auth/me"),

  // config
  rules: () => request<BookingRules>("GET", "/api/config/rules"),

  // cabins
  cabins: (includeInactive = false) =>
    request<Cabin[]>("GET", `/api/cabins?${qs({ includeInactive })}`),
  cabinStatus: () => request<CabinStatus[]>("GET", "/api/cabins/status"),
  createCabin: (c: UpsertCabin) => request<Cabin>("POST", "/api/cabins", c),
  updateCabin: (id: number, c: UpsertCabin) => request<Cabin>("PUT", `/api/cabins/${id}`, c),

  // bookings
  bookings: (from: string, to: string, cabinId?: number) =>
    request<Booking[]>("GET", `/api/bookings?${qs({ from, to, cabinId })}`),
  myBookings: (includePast = false) =>
    request<Booking[]>("GET", `/api/bookings/mine?${qs({ includePast })}`),
  createBooking: (b: BookingInput) => request<Booking>("POST", "/api/bookings", b),
  quickBook: (cabinId: number, durationMinutes: number, title?: string) =>
    request<Booking>("POST", "/api/bookings/quick", { cabinId, durationMinutes, title }),
  updateBooking: (id: string, b: BookingInput) => request<Booking>("PUT", `/api/bookings/${id}`, b),
  extendBooking: (id: string, minutes: number) =>
    request<Booking>("POST", `/api/bookings/${id}/extend`, { minutes }),
  endBooking: (id: string) => request<Booking>("POST", `/api/bookings/${id}/end`),
  checkIn: (id: string) => request<Booking>("POST", `/api/bookings/${id}/check-in`),
  cancelBooking: (id: string) => request<void>("DELETE", `/api/bookings/${id}`),

  // admin
  users: () => request<User[]>("GET", "/api/users"),
  updateUser: (id: string, role: UserRole, isActive: boolean) =>
    request<User>("PUT", `/api/users/${id}`, { role, isActive }),
  utilization: (from: string, to: string) =>
    request<UtilizationReport>("GET", `/api/reports/utilization?${qs({ from, to })}`),
};
