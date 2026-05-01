import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { login } from "../api";

export default function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const navigate = useNavigate();

  const onSubmit = async (event) => {
    event.preventDefault();
    setError("");
    setIsSubmitting(true);
    try {
      const data = await login(email, password);
      if (data.requiresTwoFactor) {
        sessionStorage.setItem("twoFactorToken", data.twoFactorToken);
        navigate("/2fa/verify");
        return;
      }
      localStorage.setItem("accessToken", data.accessToken);
      navigate("/2fa/setup");
    } catch (err) {
      setError(err.message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="container">
      <h1 className="page-title">Sign in to your account</h1>
      <p className="page-subtitle">Use your email and password. If 2FA is enabled, we will ask for your authenticator code next.</p>
      <form onSubmit={onSubmit} className="panel">
        <label>Email</label>
        <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        <label>Password</label>
        <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        {error && <p className="status error">{error}</p>}
        <button type="submit" className="btn-primary" disabled={isSubmitting}>{isSubmitting ? "Signing in..." : "Continue"}</button>
      </form>
    </main>
  );
}
