const API_BASE_URL = "https://localhost:5001/api/auth";

async function parse(res) {
  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    throw new Error(data.error || "Request failed");
  }
  return data;
}

export async function login(email, password) {
  const res = await fetch(`${API_BASE_URL}/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password })
  });
  return parse(res);
}

export async function enable2fa(token) {
  const res = await fetch(`${API_BASE_URL}/enable-2fa`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`
    }
  });
  return parse(res);
}

export async function verify2fa(code, twoFactorToken = null, token = null) {
  const headers = { "Content-Type": "application/json" };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const res = await fetch(`${API_BASE_URL}/verify-2fa`, {
    method: "POST",
    headers,
    body: JSON.stringify({ code, twoFactorToken })
  });
  return parse(res);
}
