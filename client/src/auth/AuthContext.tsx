import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { useQueryClient } from "@tanstack/react-query";
import { api, setUnauthorizedHandler, tokenStore } from "../api/client";
import type { AuthResponse, User } from "../api/types";

interface AuthState {
  user: User | null;
  token: string | null;
  loading: boolean;
  isAdmin: boolean;
  login(email: string, password: string): Promise<void>;
  register(name: string, email: string, password: string): Promise<void>;
  logout(): void;
  /** Why the user was signed out by the server (e.g. account deactivated), shown on the login page. */
  signOutReason: string | null;
}

/** How often an idle open page re-checks the account, so deactivation/role changes reach it without any click. */
const ACCOUNT_RECHECK_MS = 60_000;

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [token, setToken] = useState<string | null>(() => tokenStore.get());
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState<boolean>(() => !!tokenStore.get());

  const [signOutReason, setSignOutReason] = useState<string | null>(null);

  const clearSession = useCallback(() => {
    tokenStore.set(null);
    setToken(null);
    setUser(null);
    queryClient.clear();
  }, [queryClient]);

  const logout = useCallback(() => {
    setSignOutReason(null);
    clearSession();
  }, [clearSession]);

  // The server rejected our token (expired, account deactivated, ...).
  const expire = useCallback(
    (reason: string) => {
      setSignOutReason(reason);
      clearSession();
    },
    [clearSession]
  );

  useEffect(() => setUnauthorizedHandler(expire), [expire]);

  // Restore the session on page load. A 401 has already called expire() with the reason.
  useEffect(() => {
    if (!token || user) return;
    api
      .me()
      .then(setUser)
      .catch(clearSession)
      .finally(() => setLoading(false));
  }, [token, user, clearSession]);

  // Keep the signed-in user fresh: picks up role changes and signs out deactivated accounts.
  useEffect(() => {
    if (!user) return;
    const t = setInterval(() => {
      api
        .me()
        .then(setUser)
        .catch(() => {
          /* 401 handled by expire() */
        });
    }, ACCOUNT_RECHECK_MS);
    return () => clearInterval(t);
  }, [user]);

  const accept = (res: AuthResponse) => {
    tokenStore.set(res.token);
    setToken(res.token);
    setUser(res.user);
    setSignOutReason(null);
    setLoading(false);
  };

  const value = useMemo<AuthState>(
    () => ({
      user,
      token,
      loading,
      isAdmin: user?.role === "Admin",
      login: async (email, password) => accept(await api.login(email, password)),
      register: async (name, email, password) => accept(await api.register(name, email, password)),
      logout,
      signOutReason,
    }),
    [user, token, loading, logout, signOutReason]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
