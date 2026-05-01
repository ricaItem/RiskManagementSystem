import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { login } from "../api";

export default function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const navigate = useNavigate();

  const onSubmit = async (event) => {
    event.preventDefault();
    setError("");
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
    }
  };

  return (
    <main className="container">
      <h1>Login</h1>
      <form onSubmit={onSubmit} className="panel">
        <label>Email</label>
        <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        <label>Password</label>
        <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        {error && <p className="error">{error}</p>}
        <button type="submit">Continue</button>
      </form>
    </main>
  );
}
