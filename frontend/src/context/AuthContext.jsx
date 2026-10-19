import { createContext, useContext, useState } from "react";
import client from "../api/client";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => {
    const raw = localStorage.getItem("veriqo_user");
    return raw ? JSON.parse(raw) : null;
  });

  const persist = (data) => {
    localStorage.setItem("veriqo_token", data.token);
    const u = { email: data.email, role: data.role, profileComplete: data.profileComplete };
    localStorage.setItem("veriqo_user", JSON.stringify(u));
    setUser(u);
    return u;
  };

  const login = async (email, password) => {
    const { data } = await client.post("/auth/login", { email, password });
    return persist(data);
  };

  // Registration no longer logs in directly — it triggers email OTP verification.
  const register = async (email, password) => {
    const { data } = await client.post("/auth/register", { email, password });
    return data; // { requiresVerification, email, emailSent, devOtp }
  };

  const verifyOtp = async (email, otp) => {
    const { data } = await client.post("/auth/verify-otp", { email, otp });
    return persist(data);
  };

  const resendOtp = async (email) => {
    const { data } = await client.post("/auth/resend-otp", { email });
    return data; // { emailSent, devOtp }
  };

  const googleLogin = async (credential) => {
    const { data } = await client.post("/auth/google", { credential });
    return persist(data);
  };

  const logout = () => {
    localStorage.removeItem("veriqo_token");
    localStorage.removeItem("veriqo_user");
    setUser(null);
  };

  const refreshProfileStatus = (complete) => {
    setUser((prev) => {
      if (!prev) return prev;
      const next = { ...prev, profileComplete: complete };
      localStorage.setItem("veriqo_user", JSON.stringify(next));
      return next;
    });
  };

  return (
    <AuthContext.Provider value={{ user, login, register, verifyOtp, resendOtp, googleLogin, logout, refreshProfileStatus }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
