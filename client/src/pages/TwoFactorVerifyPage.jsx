import { useState } from "react";
import { verify2fa } from "../api";

export default function TwoFactorVerifyPage() {
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [ok, setOk] = useState("");

  const onVerify = async () => {
    setError("");
    setOk("");
    try {
      const twoFactorToken = sessionStorage.getItem("twoFactorToken");
      const data = await verify2fa(code, twoFactorToken);
      localStorage.setItem("accessToken", data.accessToken);
      sessionStorage.removeItem("twoFactorToken");
      setOk("Verification complete. JWT issued.");
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <main className="container">
      <h1>2FA Verification</h1>
      <div className="panel">
        <label>Authenticator code</label>
        <input value={code} onChange={(e) => setCode(e.target.value)} maxLength={6} />
        <button onClick={onVerify}>Verify</button>
        {ok && <p className="ok">{ok}</p>}
        {error && <p className="error">{error}</p>}
      </div>
    </main>
  );
}
