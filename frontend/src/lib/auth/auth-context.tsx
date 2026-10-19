"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import type { AuthUser } from "@/types/domain";
import { getToken, setToken } from "@/lib/api/client";
import { preuzmiPismoNaloga } from "@/lib/pismo/pismo";
import { accountApi, authApi } from "@/lib/api/endpoints";
import type {
  LoginRequest,
  ProfileInput,
  RegisterRequest,
  RegisterResponse,
} from "@/lib/api/contracts";

interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  isAdmin: boolean;
  login: (body: LoginRequest) => Promise<AuthUser>;
  /** Registration does not log in — it returns the pending-confirmation result. */
  register: (body: RegisterRequest) => Promise<RegisterResponse>;
  /** Confirm the emailed token; on success the user is logged in. */
  confirmEmail: (token: string) => Promise<AuthUser>;
  loginWithGoogle: (idToken: string) => Promise<AuthUser>;
  loginWithFacebook: (accessToken: string) => Promise<AuthUser>;
  logout: () => void;
  /** Mijenja ime i telefon i odmah osvježava kontekst, da zaglavlje ne pokazuje staro ime. */
  azurirajProfil: (body: ProfileInput) => Promise<AuthUser>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  // Rehydrate the session from the stored token on first load.
  useEffect(() => {
    const token = getToken();
    if (!token) {
      setLoading(false);
      return;
    }
    authApi
      .me()
      .then(setUser)
      .catch(() => setToken(null))
      .finally(() => setLoading(false));
  }, []);

  const login = useCallback(async (body: LoginRequest) => {
    const result = await authApi.login(body);
    setToken(result.token);
    setUser(result.user);
    preuzmiPismoNaloga(result.user.pismo);
    return result.user;
  }, []);

  const register = useCallback(
    (body: RegisterRequest) => authApi.register(body), // no login — confirmation required first
    [],
  );

  const confirmEmail = useCallback(async (token: string) => {
    const result = await authApi.confirmEmail(token);
    setToken(result.token);
    setUser(result.user);
    return result.user;
  }, []);

  const loginWithGoogle = useCallback(async (idToken: string) => {
    const result = await authApi.google(idToken);
    setToken(result.token);
    setUser(result.user);
    preuzmiPismoNaloga(result.user.pismo);
    return result.user;
  }, []);

  const loginWithFacebook = useCallback(async (accessToken: string) => {
    const result = await authApi.facebook(accessToken);
    setToken(result.token);
    setUser(result.user);
    preuzmiPismoNaloga(result.user.pismo);
    return result.user;
  }, []);

  const logout = useCallback(() => {
    setToken(null);
    setUser(null);
  }, []);

  const azurirajProfil = useCallback(async (body: ProfileInput) => {
    const azuriran = await accountApi.azurirajProfil(body);
    setUser(azuriran);
    return azuriran;
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      loading,
      isAdmin: user?.role === "admin",
      login,
      register,
      confirmEmail,
      loginWithGoogle,
      loginWithFacebook,
      logout,
      azurirajProfil,
    }),
    [
      user,
      loading,
      login,
      register,
      confirmEmail,
      loginWithGoogle,
      loginWithFacebook,
      logout,
      azurirajProfil,
    ],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
